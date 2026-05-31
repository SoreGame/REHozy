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
        [SerializeField] private Collider igniteCollider;
        [SerializeField] private float igniteRadius = 2f;
        [SerializeField] private float igniteSpeedMultiplier = 1f;

        public Transform IgnitePoint => ignitePoint != null ? ignitePoint : transform;
        public float IgniteRadius => igniteRadius;
        public float IgniteSpeedMultiplier => Mathf.Max(igniteSpeedMultiplier, 0.01f);

        public static IReadOnlyList<TorchIgnitionSource> ActiveInScene => ActiveSources;

        public void Configure(Transform point, float radius, float speedMultiplier = 1f)
        {
            ignitePoint = point;
            igniteRadius = radius;
            igniteSpeedMultiplier = speedMultiplier;
            SyncSphereColliderRadius();
        }

        public bool ContainsPoint(Vector3 worldPoint)
        {
            ResolveIgniteCollider();
            return TorchIgnitionColliderUtility.ContainsWorldPoint(igniteCollider, worldPoint);
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

        private void Awake()
        {
            ResolveIgniteCollider();
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

        private void OnValidate()
        {
            ResolveIgniteCollider();
            SyncSphereColliderRadius();
        }

        private void Reset()
        {
            ignitePoint = transform;
            ResolveIgniteCollider();
        }

        private void ResolveIgniteCollider()
        {
            if (igniteCollider != null)
            {
                return;
            }

            igniteCollider = GetComponent<Collider>();
            if (igniteCollider == null)
            {
                igniteCollider = GetComponentInChildren<Collider>(true);
            }
        }

        private void SyncSphereColliderRadius()
        {
            if (igniteCollider is not SphereCollider sphere)
            {
                return;
            }

            sphere.radius = igniteRadius;
            if (ignitePoint != null)
            {
                sphere.center = transform.InverseTransformPoint(ignitePoint.position);
            }
        }

        private void OnDrawGizmosSelected()
        {
            ResolveIgniteCollider();
            Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.35f);

            if (igniteCollider is SphereCollider sphere)
            {
                Gizmos.matrix = igniteCollider.transform.localToWorldMatrix;
                Gizmos.DrawWireSphere(sphere.center, sphere.radius);
                return;
            }

            if (igniteCollider != null)
            {
                Gizmos.matrix = Matrix4x4.identity;
                Gizmos.DrawWireCube(igniteCollider.bounds.center, igniteCollider.bounds.size);
                return;
            }

            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.DrawWireSphere(IgnitePoint.position, igniteRadius);
        }
    }
}
