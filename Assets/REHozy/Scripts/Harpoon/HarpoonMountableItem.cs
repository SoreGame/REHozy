using Bitgem.VFX.StylisedWater;
using UnityEngine;

namespace REHozy.Harpoon
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Harpoon/Mountable Item")]
    public sealed class HarpoonMountableItem : MonoBehaviour
    {
        [SerializeField] private Transform attachPoint;

        public Transform AttachPoint => attachPoint != null ? attachPoint : transform;

        public void OnMounted(Transform mountSocket)
        {
            var colliders = GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                col.enabled = false;
            }

            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
            }

            foreach (var floater in GetComponentsInChildren<WateverVolumeFloater>())
            {
                floater.enabled = false;
            }
        }

        public void AlignToSocket(Transform mountSocket)
        {
            var point = AttachPoint;
            transform.SetParent(mountSocket, worldPositionStays: true);
            var offset = point.position - transform.position;
            transform.position = mountSocket.position - offset;
            transform.rotation = mountSocket.rotation;
        }

        public void ReleaseIntoTrash(LayerMask groundMask)
        {
            transform.SetParent(null, true);

            var colliders = GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                col.enabled = true;
            }


            var rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }

            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            var drop = GetComponent<HarpoonCargoTrashDrop>();
            if (drop == null)
            {
                drop = gameObject.AddComponent<HarpoonCargoTrashDrop>();
            }

            drop.Initialize(groundMask);
        }
    }
}
