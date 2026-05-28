using UnityEngine;

namespace REHozy.Watering
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Watering/Waterable Bush Grow")]
    public sealed class WaterableBushGrow : MonoBehaviour, IWaterable
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 minScale = new(0.3f, 0.3f, 0.3f);
        [SerializeField] private Vector3 maxScale = Vector3.one;
        [SerializeField] private float growthSpeed = 0.35f;

        private float _growth01;

        private void Awake()
        {
            if (target == null)
            {
                target = transform;
            }

            target.localScale = minScale;
        }

        public void TryWater(Vector3 waterPoint, float amount, float deltaTime)
        {
            if (_growth01 >= 1f)
            {
                return;
            }

            _growth01 = Mathf.Clamp01(_growth01 + amount * growthSpeed * deltaTime);
            target.localScale = Vector3.Lerp(minScale, maxScale, _growth01);
        }
    }
}
