using UnityEngine;

namespace REHozy.Torch
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Torch/Torch Aim Pivot")]
    public sealed class TorchAimPivot : MonoBehaviour
    {
        [SerializeField] private Transform aimVisual;
        [SerializeField] private Transform tip;
        [SerializeField] private Vector3 aimRotationAxis = Vector3.right;
        [SerializeField] private float aimedDownAngle = 60f;
        [SerializeField] private float aimTiltSign = -1f;
        [SerializeField] private float aimDownThreshold = 0.92f;
        [SerializeField] private float aimLowerDuration = 0.9f;
        [SerializeField] private float aimRaiseDuration = 0.35f;

        private Quaternion _aimVisualBaseLocalRotation;
        private float _aimTilt01;

        public float AimTilt01 => _aimTilt01;
        public bool IsAimedDownEnough => _aimTilt01 >= aimDownThreshold;
        public TorchAimMode AimMode => IsAimedDownEnough ? TorchAimMode.AimedDown : TorchAimMode.Upright;
        public Transform Tip => tip != null ? tip : aimVisual != null ? aimVisual : transform;

        private void Reset()
        {
            aimVisual = transform.Find("AimGroup") ?? transform.Find("Mesh");
            tip = transform.Find("AimGroup/Tip") ?? transform.Find("Mesh/Tip") ?? transform.Find("Tip");
            CacheBaseRotation();
        }

        private void Awake()
        {
            if (aimVisual == null)
            {
                aimVisual = transform.Find("AimGroup") ?? transform.Find("Mesh");
            }

            if (tip == null)
            {
                tip = transform.Find("AimGroup/Tip") ?? transform.Find("Mesh/Tip") ?? transform.Find("Tip");
            }

            CacheBaseRotation();
        }

        public void UpdateAimTilt(bool wantLower, float deltaTime)
        {
            var target = wantLower ? 1f : 0f;
            var duration = wantLower ? aimLowerDuration : aimRaiseDuration;
            var speed = 1f / Mathf.Max(duration, 0.01f);
            _aimTilt01 = Mathf.MoveTowards(_aimTilt01, target, speed * deltaTime);
        }

        public void ResetAimTilt()
        {
            _aimTilt01 = 0f;
            ApplyVisualRotation();
        }

        private void LateUpdate()
        {
            ApplyVisualRotation();
        }

        private void CacheBaseRotation()
        {
            if (aimVisual != null)
            {
                _aimVisualBaseLocalRotation = aimVisual.localRotation;
            }
        }

        private void ApplyVisualRotation()
        {
            if (aimVisual == null)
            {
                return;
            }

            var axis = aimRotationAxis.sqrMagnitude > 0.0001f ? aimRotationAxis.normalized : Vector3.right;
            var angle = aimTiltSign * aimedDownAngle * _aimTilt01;
            aimVisual.localRotation = _aimVisualBaseLocalRotation * Quaternion.AngleAxis(angle, axis);
        }
    }
}
