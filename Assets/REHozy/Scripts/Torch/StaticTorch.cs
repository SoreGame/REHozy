using UnityEngine;

namespace REHozy.Torch
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Torch/Static Torch")]
    public sealed class StaticTorch : MonoBehaviour
    {
        [SerializeField] private Transform flamePoint;
        [SerializeField] private TorchFlamePresenter flamePresenter;
        [SerializeField] private float igniteRadius = 1.1f;
        [SerializeField] private float igniteDuration = 1.5f;

        private float _igniteProgress;

        public bool IsLit => flamePresenter != null && flamePresenter.IsLit;
        public Transform FlamePoint => flamePoint != null ? flamePoint : transform;

        private void Reset()
        {
            flamePoint = transform;
            flamePresenter = GetComponentInChildren<TorchFlamePresenter>(true);
        }

        private void Awake()
        {
            if (flamePresenter == null)
            {
                flamePresenter = GetComponentInChildren<TorchFlamePresenter>(true);
            }

            flamePresenter?.SetLit(false);
        }

        public void TryAccumulateIgnite(Vector3 tipWorld, bool carrierLit, bool carrierAimedDown, float deltaTime)
        {
            if (IsLit || !carrierLit || !carrierAimedDown)
            {
                _igniteProgress = 0f;
                return;
            }

            if ((tipWorld - FlamePoint.position).sqrMagnitude > igniteRadius * igniteRadius)
            {
                _igniteProgress = 0f;
                return;
            }

            _igniteProgress += deltaTime;
            if (_igniteProgress >= igniteDuration)
            {
                _igniteProgress = 0f;
                flamePresenter?.SetLit(true);
            }
        }

        private void OnDrawGizmosSelected()
        {
            var center = FlamePoint.position;
            Gizmos.color = new Color(1f, 0.7f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(center, igniteRadius);
        }
    }
}
