using UnityEngine;

namespace REHozy.Torch
{
    /// <summary>
    /// Links forest campfire mesh (NF_Prop_Campfire) with nearby VFX_Fire + light.
    /// Adds/configures <see cref="TorchIgnitionSource"/> at the flame position.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Torch/Campfire Ignition Link")]
    public sealed class CampfireIgnitionLink : MonoBehaviour
    {
        [SerializeField] private ParticleSystem fireVfx;
        [SerializeField] private Light fireLight;
        [SerializeField] private Transform ignitePoint;
        [SerializeField] private float igniteRadius = 2f;
        [SerializeField] private float igniteSpeedMultiplier = 4f;
        [SerializeField] private float vfxSearchRadius = 4f;

        private void Reset()
        {
            fireVfx = GetComponentInChildren<ParticleSystem>();
            ignitePoint = transform.Find("IgnitePoint");
        }

        private void Awake()
        {
            EnsureComponentsExist();
            ApplyIgnitionSource();
        }

        /// <param name="addMissingComponents">True when called from editor setup menu.</param>
        public void AutoWire(bool addMissingComponents = false)
        {
            if (fireVfx == null)
            {
                fireVfx = GetComponentInChildren<ParticleSystem>();
            }

            if (fireVfx == null)
            {
                fireVfx = FindNearbyFireVfx();
            }

            if (fireLight == null && fireVfx != null)
            {
                fireLight = fireVfx.GetComponentInChildren<Light>();
            }

            if (ignitePoint == null)
            {
                ignitePoint = CreateIgnitePoint();
            }
            else if (fireVfx != null)
            {
                ignitePoint.position = fireVfx.transform.position;
            }

            if (addMissingComponents || Application.isPlaying)
            {
                EnsureComponentsExist();
            }

            ApplyIgnitionSource();
        }

        private void EnsureComponentsExist()
        {
            if (GetComponent<TorchIgnitionSource>() == null)
            {
                gameObject.AddComponent<TorchIgnitionSource>();
            }

            if (GetComponent<SphereCollider>() == null)
            {
                gameObject.AddComponent<SphereCollider>();
            }
        }

        private void ApplyIgnitionSource()
        {
            if (ignitePoint == null)
            {
                ignitePoint = CreateIgnitePoint();
            }

            var source = GetComponent<TorchIgnitionSource>();
            if (source == null)
            {
                return;
            }

            source.Configure(ignitePoint, igniteRadius, igniteSpeedMultiplier);

            var trigger = GetComponent<SphereCollider>();
            if (trigger == null)
            {
                return;
            }

            trigger.isTrigger = true;
            trigger.radius = igniteRadius;
            trigger.center = transform.InverseTransformPoint(ignitePoint.position);
        }

        private Transform CreateIgnitePoint()
        {
            var existing = transform.Find("IgnitePoint");
            if (existing != null)
            {
                if (fireVfx != null)
                {
                    existing.position = fireVfx.transform.position;
                }

                return existing;
            }

            var go = new GameObject("IgnitePoint");
            var point = go.transform;
            point.SetParent(transform, true);

            if (fireVfx != null)
            {
                point.position = fireVfx.transform.position;
            }
            else if (TryGetMeshTopWorldPoint(out var top))
            {
                point.position = top;
            }
            else
            {
                point.localPosition = new Vector3(0f, 0.45f, 0f);
            }

            return point;
        }

        private bool TryGetMeshTopWorldPoint(out Vector3 top)
        {
            top = default;
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return false;
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            top = bounds.center + Vector3.up * bounds.extents.y;
            return true;
        }

        private ParticleSystem FindNearbyFireVfx()
        {
            ParticleSystem best = null;
            var bestSqr = vfxSearchRadius * vfxSearchRadius;

            foreach (var ps in GetComponentsInChildren<ParticleSystem>())
            {
                if (ps == null || !ps.gameObject.name.Contains("VFX_Fire"))
                {
                    continue;
                }

                best = ps;
                bestSqr = 0f;
                break;
            }

            if (best != null)
            {
                return best;
            }

            var particleSystems = FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
            foreach (var ps in particleSystems)
            {
                if (ps == null || !ps.gameObject.name.Contains("VFX_Fire"))
                {
                    continue;
                }

                var sqr = (ps.transform.position - transform.position).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    best = ps;
                }
            }

            return best;
        }

        private void OnDrawGizmosSelected()
        {
            if (ignitePoint == null)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.4f);
            Gizmos.DrawWireSphere(ignitePoint.position, igniteRadius);
        }
    }
}
