using System.Collections.Generic;
using REHozy.CarryableTools;
using UnityEngine;

namespace REHozy.Decoration
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Decoration/Prop Spawn Box")]
    public sealed class PropSpawnBox : MonoBehaviour
    {
        [SerializeField] private PropSpawnEntry[] entries = System.Array.Empty<PropSpawnEntry>();
        [SerializeField] private Transform spawnAnchor;
        [SerializeField] private Collider interactCollider;

        private readonly List<GameObject> _remaining = new();

        public int RemainingCount => _remaining.Count;
        public bool HasRemaining => _remaining.Count > 0;

        private void Awake()
        {
            if (interactCollider == null)
            {
                interactCollider = GetComponent<Collider>();
            }

            RebuildPool();
        }

        public void RebuildPool()
        {
            _remaining.Clear();

            if (entries == null)
            {
                return;
            }

            foreach (var entry in entries)
            {
                if (entry.prefab == null || entry.count <= 0)
                {
                    continue;
                }

                for (var i = 0; i < entry.count; i++)
                {
                    _remaining.Add(entry.prefab);
                }
            }
        }

        public bool TryDrawRandom(out GameObject prefab)
        {
            prefab = null;

            if (_remaining.Count == 0)
            {
                return false;
            }

            var index = Random.Range(0, _remaining.Count);
            prefab = _remaining[index];
            _remaining.RemoveAt(index);
            return true;
        }

        public Vector3 GetSpawnPosition()
        {
            return spawnAnchor != null ? spawnAnchor.position : transform.position;
        }

        public Quaternion GetSpawnRotation()
        {
            return spawnAnchor != null ? spawnAnchor.rotation : transform.rotation;
        }

        public bool IsPointInsideHome(Vector3 worldPoint)
        {
            if (HomeZoneRegistry.Instance == null)
            {
                return true;
            }

            var zone = HomeZoneRegistry.Instance.HomeZone;
            if (zone == null || !zone.enabled)
            {
                return true;
            }

            var closest = zone.ClosestPoint(worldPoint);
            return (closest - worldPoint).sqrMagnitude < 0.0001f;
        }
    }
}
