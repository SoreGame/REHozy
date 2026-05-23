using System.Collections.Generic;
using UnityEngine;

namespace REHozy.Torch
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Torch/Torch Ignition Source")]
    public sealed class TorchIgnitionSource : MonoBehaviour
    {
        private static readonly List<TorchIgnitionSource> ActiveSources = new();

        [SerializeField] private Transform ignitePoint;
        [SerializeField] private float igniteRadius = 2f;
        [SerializeField] private float maxVerticalReach = 2.5f;

        public Transform IgnitePoint => ignitePoint != null ? ignitePoint : transform;
        public float IgniteRadius => igniteRadius;

        public static IReadOnlyList<TorchIgnitionSource> ActiveInScene => ActiveSources;

        public void Configure(Transform point, float radius)
        {
            ignitePoint = point;
            igniteRadius = radius;
        }

        public bool ContainsPoint(Vector3 worldPoint)
        {
            var delta = worldPoint - IgnitePoint.position;
            var flat = new Vector2(delta.x, delta.z);
            if (flat.sqrMagnitude > igniteRadius * igniteRadius)
            {
                return false;
            }

            return Mathf.Abs(delta.y) <= maxVerticalReach;
        }

        public static TorchIgnitionSource FindBestForTip(
            Vector3 tipWorld,
            float maxSearchDistance = float.MaxValue,
            bool requireInsideRadius = true)
        {
            TorchIgnitionSource best = null;
            var bestSqr = float.MaxValue;
            var maxSqr = maxSearchDistance * maxSearchDistance;

            for (var i = 0; i < ActiveSources.Count; i++)
            {
                var source = ActiveSources[i];
                if (source == null)
                {
                    continue;
                }

                var sqr = (source.IgnitePoint.position - tipWorld).sqrMagnitude;
                if (sqr > maxSqr)
                {
                    continue;
                }

                if (requireInsideRadius && !source.ContainsPoint(tipWorld))
                {
                    continue;
                }

                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = source;
                }
            }

            return best;
        }

        private void OnEnable()
        {
            if (!ActiveSources.Contains(this))
            {
                ActiveSources.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveSources.Remove(this);
        }

        private void Reset()
        {
            ignitePoint = transform;
        }

        private void OnDrawGizmosSelected()
        {
            var center = IgnitePoint.position;
            Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.35f);
            Gizmos.DrawWireSphere(center, igniteRadius);
        }
    }
}
