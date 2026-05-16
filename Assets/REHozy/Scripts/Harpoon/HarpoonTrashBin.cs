using UnityEngine;

namespace REHozy.Harpoon
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("REHozy/Harpoon/Trash Bin")]
    public sealed class HarpoonTrashBin : MonoBehaviour
    {
        [SerializeField] private Collider volume;

        private void Reset()
        {
            volume = GetComponent<Collider>();
            if (volume != null)
            {
                volume.isTrigger = true;
            }
        }

        private void Awake()
        {
            if (volume == null)
            {
                volume = GetComponent<Collider>();
            }
        }

        public bool Contains(Vector3 worldPosition)
        {
            if (volume == null)
            {
                return false;
            }

            return volume.bounds.Contains(worldPosition);
        }
    }
}
