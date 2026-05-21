using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace REHozy.Harpoon
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Harpoon/Harpoon Controller")]
    public sealed class HarpoonController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform tip;
        [SerializeField] private Transform mountSocket;
        [SerializeField] private HarpoonCarryDriver carryDriver;
        [SerializeField] private Collider pickupCollider;

        [Header("Interaction")]
        [Tooltip("Trigger collider of the Home area. Return-on-hold and cargo drop work only while the harpoon is inside this volume.")]
        [SerializeField] private Collider homeZone;
        [SerializeField] private float impaleRadius = 0.35f;
        [SerializeField] private LayerMask mountableMask = ~0;
        [SerializeField] private float dropHoldDuration = 2f;
        [SerializeField] private float returnLerpDuration = 0.5f;
        [SerializeField] private float animationLockDuration = 0.3f;

        [Header("Events")]
        [SerializeField] private UnityEvent onImpaleStarted;
        [SerializeField] private UnityEvent onImpaleFinished;
        [SerializeField] private UnityEvent onDisposeStarted;
        [SerializeField] private UnityEvent onDisposeFinished;
        [SerializeField] private UnityEvent onBlockedReturnStarted;
        [SerializeField] private UnityEvent onBlockedReturnFinished;

        private Pose _startPose;
        private HarpoonMountableItem _mountedItem;
        private HarpoonState _state = HarpoonState.OnGround;
        private Coroutine _phaseRoutine;
        private float _returnT;
        private Vector3 _returnFromPosition;
        private Quaternion _returnFromRotation;

        public HarpoonState State => _state;
        public bool HasMountedItem => _mountedItem != null;
        public float DropHoldDuration => dropHoldDuration;

        public Transform Tip => tip != null ? tip : transform;
        public Transform MountSocket => mountSocket != null ? mountSocket : transform;

        private void Reset()
        {
            carryDriver = GetComponent<HarpoonCarryDriver>();
            pickupCollider = GetComponent<Collider>();
        }

        private void Awake()
        {
            if (carryDriver == null)
            {
                carryDriver = GetComponent<HarpoonCarryDriver>();
            }

            CacheStartPose();
            ApplyOnGround();
        }

        public void CacheStartPose()
        {
            _startPose = new Pose(transform.position, transform.rotation);
        }

        public bool CanBePickedUp() => _state == HarpoonState.OnGround;

        public void EnterCarried()
        {
            if (!CanBePickedUp())
            {
                return;
            }

            CacheStartPose();
            _state = HarpoonState.Carried;
            carryDriver?.ResetCarryMotion(transform.position);
            SetPickupColliderEnabled(false);
            SetCursorVisible(false);
        }

        public void TickCarried()
        {
            if (_state != HarpoonState.Carried || carryDriver == null)
            {
                return;
            }

            carryDriver.TryApplySmoothedCarry(transform, Tip, HasMountedItem);
        }

        public void TickReturning()
        {
            if (_state != HarpoonState.Returning)
            {
                return;
            }

            _returnT += Time.deltaTime / Mathf.Max(returnLerpDuration, 0.01f);
            var eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_returnT));
            transform.position = Vector3.Lerp(_returnFromPosition, _startPose.position, eased);
            transform.rotation = Quaternion.Slerp(_returnFromRotation, _startPose.rotation, eased);

            if (_returnT >= 1f)
            {
                FinishReturnHome();
            }
        }

        public bool TryImpaleOnClick()
        {
            if (_state != HarpoonState.Carried || _mountedItem != null)
            {
                return false;
            }

            var candidate = FindMountableUnderTip();
            if (candidate == null)
            {
                return false;
            }

            StartPhase(ImpalePhase(candidate), onImpaleStarted, onImpaleFinished);
            return true;
        }

        public bool TryDisposeOnClick()
        {
            if (_state != HarpoonState.Carried || _mountedItem == null)
            {
                return false;
            }

            if (!IsInHomeZone() && FindTrashBinAt(transform.position) == null)
            {
                return false;
            }

            StartPhase(DisposePhase(), onDisposeStarted, onDisposeFinished);
            return true;
        }

        public bool IsInHomeZone()
        {
            if (homeZone == null || !homeZone.enabled)
            {
                return false;
            }

            var testPoint = transform.position;
            if (_state == HarpoonState.Carried && carryDriver != null
                && carryDriver.TryGetGroundAnchor(out var groundPoint))
            {
                testPoint = groundPoint;
            }

            var closest = homeZone.ClosestPoint(testPoint);
            return (closest - testPoint).sqrMagnitude < 0.0001f;
        }

        public void StartReturnHome()
        {
            if (_state != HarpoonState.Carried || _mountedItem != null || !IsInHomeZone())
            {
                return;
            }

            _state = HarpoonState.Returning;
            _returnT = 0f;
            _returnFromPosition = transform.position;
            _returnFromRotation = transform.rotation;

            SetCursorVisible(true);
        }

        public void StartBlockedReturnHold()
        {
            if (_state != HarpoonState.Carried || _mountedItem == null || !IsInHomeZone())
            {
                return;
            }

            StartPhase(BlockedReturnPhase(), onBlockedReturnStarted, onBlockedReturnFinished);
        }

        private IEnumerator ImpalePhase(HarpoonMountableItem item)
        {
            _mountedItem = item;
            item.OnMounted(MountSocket);
            item.AlignToSocket(MountSocket);
            yield return new WaitForSeconds(animationLockDuration);
        }

        private IEnumerator DisposePhase()
        {
            var item = _mountedItem;
            _mountedItem = null;
            yield return new WaitForSeconds(animationLockDuration);

            if (item != null)
            {
                var groundMask = carryDriver != null ? carryDriver.GroundMask : (LayerMask)~0;
                item.ReleaseIntoTrash(groundMask);
            }
        }

        private IEnumerator BlockedReturnPhase()
        {
            yield return new WaitForSeconds(animationLockDuration);
        }

        private void StartPhase(IEnumerator body, UnityEvent started, UnityEvent finished)
        {
            if (_phaseRoutine != null)
            {
                StopCoroutine(_phaseRoutine);
            }

            _phaseRoutine = StartCoroutine(RunPhase(body, started, finished));
        }

        private IEnumerator RunPhase(IEnumerator body, UnityEvent started, UnityEvent finished)
        {
            var previous = _state;
            _state = HarpoonState.Busy;
            started?.Invoke();
            yield return body;
            finished?.Invoke();
            _phaseRoutine = null;

            if (previous == HarpoonState.Carried)
            {
                _state = HarpoonState.Carried;
            }
        }

        private void FinishReturnHome()
        {
            transform.SetPositionAndRotation(_startPose.position, _startPose.rotation);
            ApplyOnGround();
        }

        private void ApplyOnGround()
        {
            _state = HarpoonState.OnGround;
            SetPickupColliderEnabled(true);
            HarpoonGameplayLock.SetCanPickup(true);
            SetCursorVisible(true);
        }

        private void OnDisable()
        {
            HarpoonGameplayLock.SetCanPickup(true);
        }

        private HarpoonMountableItem FindMountableUnderTip()
        {
            var origin = Tip.position;
            var hits = Physics.OverlapSphere(origin, impaleRadius, mountableMask, QueryTriggerInteraction.Collide);
            HarpoonMountableItem best = null;
            var bestDist = float.MaxValue;

            foreach (var hit in hits)
            {
                var item = hit.GetComponentInParent<HarpoonMountableItem>();
                if (item == null || !item.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var dist = Vector3.SqrMagnitude(item.transform.position - origin);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = item;
                }
            }

            return best;
        }

        private static HarpoonTrashBin FindTrashBinAt(Vector3 position)
        {
            var bins = FindObjectsByType<HarpoonTrashBin>(FindObjectsSortMode.None);
            foreach (var bin in bins)
            {
                if (bin.Contains(position))
                {
                    return bin;
                }
            }

            return null;
        }

        private void SetPickupColliderEnabled(bool enabled)
        {
            if (pickupCollider != null)
            {
                pickupCollider.enabled = enabled;
            }
        }

        private static void SetCursorVisible(bool visible)
        {
            Cursor.visible = visible;
            Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.None;
        }
    }
}
