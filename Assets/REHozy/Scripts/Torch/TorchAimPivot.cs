using UnityEngine;

namespace REHozy.Torch
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Torch/Torch Aim Pivot")]
    public sealed class TorchAimPivot : MonoBehaviour
    {
        [SerializeField] private Transform tip;
        [SerializeField] private Vector3 aimRotationAxis = Vector3.right;
        [SerializeField] private float aimedDownAngle = 60f;
        [SerializeField] private float aimDownThreshold = 0.92f;
        [SerializeField] private float aimLowerDuration = 0.9f;
        [SerializeField] private float aimRaiseDuration = 0.35f;

        private Quaternion _baseLocalRotation;
        private float _aimTilt01;

        public float AimTilt01 => _aimTilt01;
        public bool IsAimedDownEnough => _aimTilt01 >= aimDownThreshold;
        public TorchAimMode AimMode => IsAimedDownEnough ? TorchAimMode.AimedDown : TorchAimMode.Upright;
        public Transform Tip => tip != null ? tip : transform;

        private void Awake()
        {
            _baseLocalRotation = transform.localRotation;
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
        }

        private void LateUpdate()
        {
            ApplyVisualRotation();
        }

        private void ApplyVisualRotation()
        {
            if (transform.parent == null)
            {
                return;
            }

            var up = transform.up;
            if (up.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.FromToRotation(up, Vector3.up) * transform.rotation;
            }

            if (_aimTilt01 <= 0.0001f)
            {
                return;
            }

            var axis = aimRotationAxis.sqrMagnitude > 0.0001f ? aimRotationAxis.normalized : Vector3.right;
            var angle = aimedDownAngle * _aimTilt01;
            transform.localRotation = Quaternion.AngleAxis(angle, axis) * transform.localRotation;
        }
    }
}
