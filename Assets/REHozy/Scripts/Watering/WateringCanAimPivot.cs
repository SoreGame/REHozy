using UnityEngine;

namespace REHozy.Watering
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Watering/Watering Can Aim Pivot")]
    public sealed class WateringCanAimPivot : MonoBehaviour
    {
        [SerializeField] private Transform pourVisual;
        [SerializeField] private Transform tip;
        [SerializeField] private Vector3 pourRotationAxis = Vector3.right;
        [SerializeField] private float pourAngle = 42f;
        [SerializeField] private float pourTiltSign = -1f;
        [SerializeField] private float pourLowerDuration = 0.45f;
        [SerializeField] private float pourRaiseDuration = 0.3f;

        private Quaternion _pourVisualBaseLocalRotation;
        private float _pourTilt01;
        private bool _legacyHierarchyMigrated;

        public Transform Tip => tip != null ? tip : pourVisual != null ? pourVisual : transform;

        private void Reset()
        {
            pourVisual = transform.Find("PourVisual") ?? transform.Find("Mesh");
            tip = transform.Find("PourVisual/Tip")
                ?? transform.Find("Tip")
                ?? transform.parent?.Find("Tip");
            CacheBaseRotation();
        }

        private void Awake()
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

            TryMigrateLegacyHierarchy();
            CacheBaseRotation();
        }

        public void UpdatePourTilt(bool wantPour, float deltaTime)
        {
            var target = wantPour ? 1f : 0f;
            var duration = wantPour ? pourLowerDuration : pourRaiseDuration;
            var speed = 1f / Mathf.Max(duration, 0.01f);
            _pourTilt01 = Mathf.MoveTowards(_pourTilt01, target, speed * deltaTime);
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
            var angle = pourTiltSign * pourAngle * _pourTilt01;
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
