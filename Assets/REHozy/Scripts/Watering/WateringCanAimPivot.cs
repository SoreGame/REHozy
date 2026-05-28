using UnityEngine;

namespace REHozy.Watering
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Watering/Watering Can Aim Pivot")]
    public sealed class WateringCanAimPivot : MonoBehaviour
    {
        [SerializeField] private Transform tip;
        [SerializeField] private Vector3 pourRotationAxis = Vector3.right;
        [SerializeField] private float pourAngle = 55f;
        [SerializeField] private float pourLowerDuration = 0.45f;
        [SerializeField] private float pourRaiseDuration = 0.3f;

        private Quaternion _baseLocalRotation;
        private float _pourTilt01;

        public Transform Tip => tip != null ? tip : transform;

        private void Awake()
        {
            _baseLocalRotation = transform.localRotation;
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
            transform.localRotation = _baseLocalRotation;
        }

        private void LateUpdate()
        {
            if (_pourTilt01 <= 0.0001f)
            {
                transform.localRotation = _baseLocalRotation;
                return;
            }

            var axis = pourRotationAxis.sqrMagnitude > 0.0001f ? pourRotationAxis.normalized : Vector3.right;
            transform.localRotation = Quaternion.AngleAxis(pourAngle * _pourTilt01, axis) * _baseLocalRotation;
        }
    }
}
