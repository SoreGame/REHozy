using REHozy;
using REHozy.CarryableTools;
using UnityEngine;
using UnityEngine.Serialization;

namespace REHozy.Watering
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Watering/Watering Can Aim Pivot")]
    public sealed class WateringCanAimPivot : MonoBehaviour, ICarryableCarryRotationModifier
    {
        [Header("Root pitch — carrying")]
        [SerializeField] private float carryPitchAngle;
        [SerializeField] private Vector3 carryPitchAxis = Vector3.right;

        [Header("Root pitch — pouring")]
        [SerializeField] private float pourPitchAngle = 18f;
        [SerializeField] private Vector3 pourPitchAxis = Vector3.right;

        [Header("Yaw offset (vs camera / cursor forward)")]
        [Tooltip("Extra yaw after base facing. 90 = right, -90 = left, 180 = backward.")]
        [SerializeField] private float carryYawOffsetDegrees = 90f;
        [SerializeField] private Vector3 carryYawOffsetAxis = Vector3.up;

        [Header("Pour mesh tilt (PourVisual)")]
        [SerializeField] private Transform pourVisual;
        [SerializeField] private Transform tip;
        [SerializeField] private Vector3 pourRotationAxis = Vector3.right;
        [FormerlySerializedAs("pourAngle")]
        [SerializeField] private float pourMeshTiltAngle = 42f;
        [SerializeField] private float pourTiltSign = -1f;
        [SerializeField] private float pourLowerDuration = 0.45f;
        [SerializeField] private float pourRaiseDuration = 0.3f;

        private Quaternion _pourVisualBaseLocalRotation;
        private float _pourTilt01;
        private bool _legacyHierarchyMigrated;

        public Transform Tip => tip != null ? tip : pourVisual != null ? pourVisual : transform;

        public bool UsesYawPitchCarry => true;

        public Quaternion ApplyCarryRotationOffset(Quaternion rotation)
        {
            if (Mathf.Abs(carryYawOffsetDegrees) > 0.001f)
            {
                var yawAxis = carryYawOffsetAxis.sqrMagnitude > 0.0001f
                    ? carryYawOffsetAxis.normalized
                    : Vector3.up;
                rotation = rotation * Quaternion.AngleAxis(carryYawOffsetDegrees, yawAxis);
            }

            return rotation * GetPitchRotation();
        }

        private Quaternion GetPitchRotation()
        {
            var carryAxis = carryPitchAxis.sqrMagnitude > 0.0001f ? carryPitchAxis.normalized : Vector3.right;
            var pourAxis = pourPitchAxis.sqrMagnitude > 0.0001f ? pourPitchAxis.normalized : carryAxis;

            if (Mathf.Abs(carryPitchAngle) < 0.001f && Mathf.Abs(pourPitchAngle) < 0.001f)
            {
                return Quaternion.identity;
            }

            var carryPitch = Quaternion.AngleAxis(carryPitchAngle, carryAxis);
            var pourPitch = Quaternion.AngleAxis(pourPitchAngle, pourAxis);
            return Quaternion.Slerp(carryPitch, pourPitch, _pourTilt01);
        }

        private void Reset()
        {
            ResolveReferences();
            CacheBaseRotation();
        }

        private void OnValidate()
        {
            ResolveReferences();
            CacheBaseRotation();
        }

        private void ResolveReferences()
        {
            if (pourVisual == null)
            {
                pourVisual = transform.Find("PourVisual") ?? transform.Find("Mesh");
            }

            if (tip == null)
            {
                tip = transform.Find("PourVisual/Tip")
                    ?? transform.Find("Tip")
                    ?? transform.parent?.Find("Tip");
            }
        }

        private void Awake()
        {
            ResolveReferences();
            TryMigrateLegacyHierarchy();
            CacheBaseRotation();
        }

        public void UpdatePourTilt(bool wantPour, float deltaTime)
        {
            var prev = _pourTilt01;
            var target = wantPour ? 1f : 0f;
            var duration = wantPour ? pourLowerDuration : pourRaiseDuration;
            var speed = 1f / Mathf.Max(duration, 0.01f);
            _pourTilt01 = Mathf.MoveTowards(_pourTilt01, target, speed * deltaTime);

            // #region agent log
            if (transform.root.GetComponentInChildren<WateringCanToolActions>() != null
                && (Mathf.Abs(_pourTilt01 - prev) > 0.001f || Time.frameCount % 15 == 0))
            {
                var pitch = Mathf.Lerp(carryPitchAngle, pourPitchAngle, _pourTilt01);
                DebugAgentLog.Log(
                    "H-B",
                    "WateringCanAimPivot.cs:UpdatePourTilt",
                    "pour-pitch-update",
                    "{\"frame\":" + Time.frameCount +
                    ",\"pourTilt01\":" + _pourTilt01.ToString("F3") +
                    ",\"pitchAngle\":" + pitch.ToString("F2") +
                    ",\"wantPour\":" + (wantPour ? "true" : "false") + "}");
            }
            // #endregion
        }

        public void ResetPourTilt()
        {
            _pourTilt01 = 0f;
            ApplyVisualRotation();
        }

        private void LateUpdate()
        {
            ApplyVisualRotation();
        }

        private void CacheBaseRotation()
        {
            if (pourVisual != null)
            {
                _pourVisualBaseLocalRotation = pourVisual.localRotation;
            }
        }

        private void ApplyVisualRotation()
        {
            if (pourVisual == null)
            {
                return;
            }

            var axis = pourRotationAxis.sqrMagnitude > 0.0001f ? pourRotationAxis.normalized : Vector3.right;
            var angle = pourTiltSign * pourMeshTiltAngle * _pourTilt01;
            pourVisual.localRotation = _pourVisualBaseLocalRotation * Quaternion.AngleAxis(angle, axis);
        }

        private void TryMigrateLegacyHierarchy()
        {
            if (_legacyHierarchyMigrated || pourVisual != null)
            {
                return;
            }

            var pourPivot = transform;
            var root = pourPivot.parent;
            if (root == null)
            {
                return;
            }

            if (pourPivot.localPosition == Vector3.zero)
            {
                pourPivot.localPosition = new Vector3(0f, -0.08f, -0.05f);
            }

            var meshFilter = root.GetComponent<MeshFilter>();
            var meshRenderer = root.GetComponent<MeshRenderer>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                return;
            }

            var pourVisualGo = new GameObject("PourVisual");
            pourVisual = pourVisualGo.transform;
            pourVisual.SetParent(pourPivot, false);
            pourVisual.localPosition = Vector3.zero;
            pourVisual.localRotation = Quaternion.identity;

            var meshGo = new GameObject("Mesh");
            meshGo.transform.SetParent(pourVisual, false);
            meshGo.transform.localPosition = Vector3.zero;
            meshGo.transform.localRotation = Quaternion.identity;
            meshGo.transform.localScale = Vector3.one;
            meshGo.AddComponent<MeshFilter>().sharedMesh = meshFilter.sharedMesh;

            if (meshRenderer != null)
            {
                var migratedRenderer = meshGo.AddComponent<MeshRenderer>();
                migratedRenderer.sharedMaterials = meshRenderer.sharedMaterials;
                Destroy(meshRenderer);
            }

            Destroy(meshFilter);

            if (tip == null)
            {
                tip = root.Find("Tip");
            }

            if (tip != null && tip.parent != pourVisual)
            {
                tip.SetParent(pourVisual, true);
            }

            _legacyHierarchyMigrated = true;
            CacheBaseRotation();
        }
    }
}
