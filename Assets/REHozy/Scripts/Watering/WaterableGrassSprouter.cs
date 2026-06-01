using System.Collections.Generic;
using UnityEngine;

namespace REHozy.Watering
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Watering/Waterable Grass Sprouter")]
    public sealed class WaterableGrassSprouter : MonoBehaviour, IWaterable
    {
        [SerializeField] private GameObject[] grassPrefabs;
        [SerializeField] private float spawnRadius = 0.8f;
        [SerializeField] private int maxBlades = 12;
        [SerializeField] private float minBladeDistance = 0.12f;
        [SerializeField] private float spawnProgressPerBlade = 0.35f;
        [SerializeField] private LayerMask groundMask = ~0;

        private readonly List<Transform> _spawned = new();
        private float _spawnProgress;

        private void OnEnable()
        {
            WaterableRegistry.Register(this);
        }

        private void OnDisable()
        {
            WaterableRegistry.Unregister(this);
        }

        public bool IsWateringComplete
        {
            get
            {
                PruneDestroyed();
                return _spawned.Count >= maxBlades;
            }
        }

        public void TryWater(Vector3 waterPoint, float amount, float deltaTime)
        {
            PruneDestroyed();
            if (_spawned.Count >= maxBlades || grassPrefabs == null || grassPrefabs.Length == 0)
            {
                return;
            }

            _spawnProgress += amount * deltaTime;
            while (_spawnProgress >= spawnProgressPerBlade && _spawned.Count < maxBlades)
            {
                _spawnProgress -= spawnProgressPerBlade;
                if (!TrySpawnBlade(waterPoint))
                {
                    break;
                }
            }
        }

        private void PruneDestroyed()
        {
            for (var i = _spawned.Count - 1; i >= 0; i--)
            {
                if (_spawned[i] == null)
                {
                    _spawned.RemoveAt(i);
                }
            }
        }

        private bool TrySpawnBlade(Vector3 waterPoint)
        {
            for (var attempt = 0; attempt < 8; attempt++)
            {
                var offset = Random.insideUnitCircle * spawnRadius;
                var candidate = waterPoint + new Vector3(offset.x, 0f, offset.y);

                if (!TryGetGroundPoint(candidate, out var groundPoint))
                {
                    continue;
                }

                if (!IsFarEnoughFromOthers(groundPoint))
                {
                    continue;
                }

                var prefab = grassPrefabs[Random.Range(0, grassPrefabs.Length)];
                if (prefab == null)
                {
                    return false;
                }

                var instance = Instantiate(prefab, groundPoint, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);
                _spawned.Add(instance.transform);
                return true;
            }

            return false;
        }

        private bool TryGetGroundPoint(Vector3 worldPoint, out Vector3 groundPoint)
        {
            groundPoint = default;
            var origin = worldPoint + Vector3.up * 2f;
            if (!Physics.Raycast(origin, Vector3.down, out var hit, 6f, groundMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            groundPoint = hit.point;
            return true;
        }

        private bool IsFarEnoughFromOthers(Vector3 point)
        {
            var minDistSqr = minBladeDistance * minBladeDistance;
            foreach (var blade in _spawned)
            {
                if (blade == null)
                {
                    continue;
                }

                if ((blade.position - point).sqrMagnitude < minDistSqr)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
