using System.Collections.Generic;
using REHozy.Rendering;
using UnityEngine;

namespace REHozy.Torch
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Torch/Static Torch")]
    public sealed class StaticTorch : MonoBehaviour
    {
        private static readonly List<StaticTorch> ActiveStaticTorches = new();

        [SerializeField] private Transform flamePoint;
        [SerializeField] private TorchFlamePresenter flamePresenter;
        [SerializeField] private float igniteRadius = 1.1f;
        [SerializeField] private float maxVerticalReach = 1.5f;
        [SerializeField] private float igniteDuration = 1.5f;
        [SerializeField] private float igniteSpeedMultiplier = 1.5f;
        [SerializeField] private bool startLit;

        private float _igniteProgress;

        public bool IsLit => flamePresenter != null && flamePresenter.IsLit;
        public Transform FlamePoint => flamePoint != null ? flamePoint : transform;
        public float IgniteSpeedMultiplier => Mathf.Max(igniteSpeedMultiplier, 0.01f);

        public static IReadOnlyList<StaticTorch> ActiveInScene => ActiveStaticTorches;

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

            if (GetComponent<ObjectOutlineHighlight>() == null)
            {
                gameObject.AddComponent<ObjectOutlineHighlight>();
            }

            if (startLit)
            {
                flamePresenter?.SetLit(true);
            }
            else
            {
                flamePresenter?.SetLit(false);
            }
        }

        public bool ContainsPoint(Vector3 worldPoint)
        {
            var delta = worldPoint - FlamePoint.position;
            var flat = new Vector2(delta.x, delta.z);
            if (flat.sqrMagnitude > igniteRadius * igniteRadius)
            {
                return false;
            }

            return Mathf.Abs(delta.y) <= maxVerticalReach;
        }

        public static StaticTorch FindBestLitForTip(Vector3 tipWorld)
        {
            StaticTorch best = null;
            var bestSqr = float.MaxValue;

            for (var i = 0; i < ActiveStaticTorches.Count; i++)
            {
                var torch = ActiveStaticTorches[i];
                if (torch == null || !torch.IsLit || !torch.ContainsPoint(tipWorld))
                {
                    continue;
                }

                var sqr = (torch.FlamePoint.position - tipWorld).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = torch;
                }
            }

            return best;
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

        private void OnEnable()
        {
            if (!ActiveStaticTorches.Contains(this))
            {
                ActiveStaticTorches.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveStaticTorches.Remove(this);
        }

        private void OnDrawGizmosSelected()
        {
            var center = FlamePoint.position;
            Gizmos.color = new Color(1f, 0.7f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(center, igniteRadius);
        }
    }
}
