using UnityEngine;

namespace REHozy.Watering
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Watering/Watering Can Aim Pivot")]
    public sealed class WateringCanAimPivot : MonoBehaviour
    {
        [SerializeField] private Transform tip;
        [SerializeField] private Vector3 pourRotationAxis = Vector3.right;
        [SerializeField] private float carryHoldAngle = 38f;
        [SerializeField] private float pourSpoutAngle = 20f;
        [SerializeField] private float pourLowerDuration = 0.45f;
        [SerializeField] private float pourRaiseDuration = 0.3f;

        private Quaternion _baseLocalRotation;
        private float _pourTilt01;

        public Transform Tip => tip != null ? tip : transform;

        private void Awake()
        {
            _baseLocalRotation = transform.localRotation;
            _pourTilt01 = 1f;
        }

        public void UpdatePourTilt(bool wantPour, float deltaTime)
        {
            // 1 = calm carry offset on pivot; 0 = spout follows carry driver aim (pour).
            var target = wantPour ? 0f : 1f;
            var duration = wantPour ? pourLowerDuration : pourRaiseDuration;
            var speed = 1f / Mathf.Max(duration, 0.01f);
            _pourTilt01 = Mathf.MoveTowards(_pourTilt01, target, speed * deltaTime);
        }

        public void ResetPourTilt()
        {
            _pourTilt01 = 1f;
        }

        private void LateUpdate()
        {
            if (_pourTilt01 <= 0.0001f)
            {
                transform.localRotation = _baseLocalRotation;
                return;
            }

            var axis = pourRotationAxis.sqrMagnitude > 0.0001f ? pourRotationAxis.normalized : Vector3.right;
            var angle = Mathf.Lerp(pourSpoutAngle, carryHoldAngle, _pourTilt01);
            transform.localRotation = Quaternion.AngleAxis(angle, axis) * _baseLocalRotation;
        }
    }
}
