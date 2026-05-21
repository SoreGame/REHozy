using Bitgem.VFX.StylisedWater;
using UnityEngine;

namespace REHozy.Harpoon
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Harpoon/Mountable Item")]
    public sealed class HarpoonMountableItem : MonoBehaviour
    {
        [SerializeField] private Transform attachPoint;

        [Header("Quest")]
        [SerializeField] private QuestSO questOnTrashDispose;
        [SerializeField] private int questProgressAmount = 1;

        public Transform AttachPoint => attachPoint != null ? attachPoint : transform;

        public void OnMounted(Transform mountSocket)
        {
            _consumed = false;
            _worldScaleBeforeMount = transform.lossyScale;

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

            foreach (var floater in GetComponentsInChildren<WateverVolumeFloater>(true))
            {
                floater.enabled = false;
            }

            var dropped = GetComponent<HarpoonDroppedCargo>();
            if (dropped != null)
            {
                Destroy(dropped);
            }
        }

        public void AlignToSocket(Transform mountSocket)
        {
            var point = AttachPoint;
            transform.SetParent(mountSocket, worldPositionStays: true);
            var offset = point.position - transform.position;
            transform.position = mountSocket.position - offset;
            transform.rotation = mountSocket.rotation;
            ApplyWorldScale(_worldScaleBeforeMount);
        }

        private void ApplyWorldScale(Vector3 targetWorldScale)
        {
            if (transform.parent == null)
            {
                transform.localScale = targetWorldScale;
                return;
            }

            var parentScale = transform.parent.lossyScale;
            transform.localScale = new Vector3(
                SafeDivide(targetWorldScale.x, parentScale.x),
                SafeDivide(targetWorldScale.y, parentScale.y),
                SafeDivide(targetWorldScale.z, parentScale.z));
        }

        private static float SafeDivide(float value, float divisor) =>
            Mathf.Abs(divisor) > 0.0001f ? value / divisor : value;

        public void ReleaseDropped(LayerMask groundMask, LayerMask waterMask = default)
        {
            DetachWithPhysics();

            var trashDrop = GetComponent<HarpoonCargoTrashDrop>();
            if (trashDrop != null)
            {
                Destroy(trashDrop);
            }

            var dropped = GetComponent<HarpoonDroppedCargo>();
            if (dropped == null)
            {
                dropped = gameObject.AddComponent<HarpoonDroppedCargo>();
            }

            dropped.Initialize(groundMask, waterMask);
        }

        public void ConsumeInTrashBin()
        {
            if (_consumed)
            {
                return;
            }

            _consumed = true;
            DetachWithPhysics();

            var dropped = GetComponent<HarpoonDroppedCargo>();
            if (dropped != null)
            {
                Destroy(dropped);
            }

            var trashDrop = GetComponent<HarpoonCargoTrashDrop>();
            if (trashDrop == null)
            {
                trashDrop = gameObject.AddComponent<HarpoonCargoTrashDrop>();
            }

            trashDrop.BeginTrashConsume();
            ReportTrashDisposeQuestProgress();
        }

        private bool _consumed;
        private Vector3 _worldScaleBeforeMount = Vector3.one;

        private void DetachWithPhysics()
        {
            var worldPose = new Pose(transform.position, transform.rotation);
            transform.SetParent(null, false);
            transform.SetPositionAndRotation(worldPose.position, worldPose.rotation);
            ApplyWorldScale(_worldScaleBeforeMount);
            Physics.SyncTransforms();

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
        }

        void ReportTrashDisposeQuestProgress()
        {
            if (questOnTrashDispose == null || questProgressAmount == 0)
            {
                return;
            }

            QuestBus.GetInstance().OnUpdateCounter?.Invoke(
                questOnTrashDispose.QuestId,
                questProgressAmount);
        }
    }
}
