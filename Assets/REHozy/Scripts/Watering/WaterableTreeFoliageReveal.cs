using UnityEngine;

namespace REHozy.Watering
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Watering/Waterable Tree Foliage Reveal")]
    public sealed class WaterableTreeFoliageReveal : MonoBehaviour, IWaterable
    {
        private static readonly int RevealId = Shader.PropertyToID("_Reveal");

        [SerializeField] private Renderer foliageRenderer;
        [SerializeField] private float revealSpeed = 0.4f;

        private float _reveal01;
        private MaterialPropertyBlock _propertyBlock;

        public bool IsWateringComplete => _reveal01 >= 1f;

        private void Awake()
        {
            if (foliageRenderer == null)
            {
                foliageRenderer = GetComponentInChildren<Renderer>();
            }

            _propertyBlock = new MaterialPropertyBlock();
            ApplyReveal(0f);
        }

        private void OnEnable()
        {
            WaterableRegistry.Register(this);
        }

        private void OnDisable()
        {
            WaterableRegistry.Unregister(this);
        }

        public void TryWater(Vector3 waterPoint, float amount, float deltaTime)
        {
            if (_reveal01 >= 1f || foliageRenderer == null)
            {
                return;
            }

            _reveal01 = Mathf.Clamp01(_reveal01 + amount * revealSpeed * deltaTime);
            ApplyReveal(_reveal01);
        }

        private void ApplyReveal(float reveal)
        {
            foliageRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(RevealId, reveal);
            foliageRenderer.SetPropertyBlock(_propertyBlock);
        }
    }
}
