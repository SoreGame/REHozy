using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace REHozy.CarryableTools
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Carryable Tools/Carryable Tool Core")]
    public sealed class CarryableToolCore : MonoBehaviour
    {
        [Header("Tool")]
        [SerializeField] private PlayerToolMode toolModeId = PlayerToolMode.Harpoon;

        [Header("References")]
        [SerializeField] private Transform tip;
        [SerializeField] private Transform mountSocket;
        [SerializeField] private CarryableCarryDriver carryDriver;
        [SerializeField] private Collider pickupCollider;

        [Header("Home")]
        [Tooltip("Optional override. If empty, uses HomeZoneRegistry in the scene.")]
        [SerializeField] private Collider homeZone;

        [Header("Timing")]
        [SerializeField] private float dropHoldDuration = 2f;
        [SerializeField] private float returnLerpDuration = 0.5f;
        [SerializeField] private float animationLockDuration = 0.3f;

        private Pose _startPose;
        private Pose _groundRestPose;
        private CarryableToolState _state = CarryableToolState.OnGround;
        private Coroutine _phaseRoutine;
        private float _returnT;
        private Vector3 _returnFromPosition;
        private Quaternion _returnFromRotation;
        private ICarryableToolActions _actions;

        public PlayerToolMode ToolModeId => toolModeId;
        public CarryableToolState State => _state;
        public float DropHoldDuration => dropHoldDuration;
        public float AnimationLockDuration => animationLockDuration;
        public Transform Tip => tip != null ? tip : transform;
        public Transform MountSocket => mountSocket != null ? mountSocket : transform;
        public CarryableCarryDriver CarryDriver => carryDriver;

        public bool HasCargo => _actions != null && _actions.HasCargo(this);

        public bool CanReturnHome() => _actions != null && _actions.CanReturnHome(this);

        private void Reset()
        {
            carryDriver = GetComponent<CarryableCarryDriver>();
            pickupCollider = GetComponent<Collider>();
        }

        private void Awake()
        {
            if (carryDriver == null)
            {
                carryDriver = GetComponent<CarryableCarryDriver>();
            }

            _actions = GetComponent<ICarryableToolActions>();
            if (_actions == null)
            {
                foreach (var behaviour in GetComponents<MonoBehaviour>())
                {
                    if (behaviour is ICarryableToolActions actions)
                    {
                        _actions = actions;
                        break;
                    }
                }
            }

            ResolveHomeZone();
            CacheStartPose();
            _groundRestPose = _startPose;
            ApplyOnGround();
        }

        private void ResolveHomeZone()
        {
            if (homeZone != null)
            {
                return;
            }

            if (HomeZoneRegistry.Instance != null)
            {
                homeZone = HomeZoneRegistry.Instance.HomeZone;
            }
        }

        public void CacheStartPose()
        {
            _startPose = new Pose(transform.position, transform.rotation);
        }

        /// <summary>
        /// Snaps to ground and resting pose (e.g. before quest hides the tool).
        /// When carried, uses the ground point under the tool instead of the pickup pose.
        /// </summary>
        public void SnapToHomeGround()
        {
            if (_phaseRoutine != null)
            {
                StopCoroutine(_phaseRoutine);
                _phaseRoutine = null;
            }

            if (_state == CarryableToolState.Carried)
            {
                if (carryDriver != null && carryDriver.TryGetGroundAnchor(out var anchor))
                {
                    transform.SetPositionAndRotation(anchor, transform.rotation);
                    carryDriver.ResetCarryMotion(anchor);
                }
                else
                {
                    carryDriver?.ResetCarryMotion(transform.position);
                }
            }
            else
            {
                transform.SetPositionAndRotation(_groundRestPose.position, _groundRestPose.rotation);
            }

            ApplyOnGround();
        }

        /// <summary>
        /// Re-syncs smoothed carry motion after UI or other interruptions (quest panel, etc.).
        /// </summary>
        public void FreezeCarryMotionAtCurrentPose()
        {
            if (_state != CarryableToolState.Carried || carryDriver == null)
            {
                return;
            }

            carryDriver.ResetCarryMotion(transform.position);
        }

        public bool CanBePickedUp()
        {
            return _state == CarryableToolState.OnGround
                && CarryableGameplayLock.CanPickup
                && PlayerToolModeState.Active == toolModeId;
        }

        public void EnterCarried()
        {
            if (_state != CarryableToolState.OnGround || PlayerToolModeState.Active != toolModeId)
            {
                return;
            }

            CacheStartPose();
            _state = CarryableToolState.Carried;
            carryDriver?.ResetCarryMotion(transform.position);
            SetPickupColliderEnabled(false);
            SetCursorVisible(false);
        }

        public void TickCarried()
        {
            if (_state != CarryableToolState.Carried || carryDriver == null)
            {
                return;
            }

            carryDriver.TryApplySmoothedCarry(transform, Tip, HasCargo);
        }

        public void TickReturning()
        {
            if (_state != CarryableToolState.Returning)
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

        public bool IsInHomeZone()
        {
            ResolveHomeZone();
            if (homeZone == null || !homeZone.enabled)
            {
                return false;
            }

            var testPoint = transform.position;
            if (_state == CarryableToolState.Carried && carryDriver != null
                && carryDriver.TryGetGroundAnchor(out var groundPoint))
            {
                testPoint = groundPoint;
            }

            var closest = homeZone.ClosestPoint(testPoint);
            return (closest - testPoint).sqrMagnitude < 0.0001f;
        }

        public void StartReturnHome()
        {
            if (_state != CarryableToolState.Carried || !IsInHomeZone())
            {
                return;
            }

            if (_actions != null && !_actions.CanReturnHome(this))
            {
                return;
            }

            _state = CarryableToolState.Returning;
            _returnT = 0f;
            _returnFromPosition = transform.position;
            _returnFromRotation = transform.rotation;
            SetCursorVisible(true);
        }

        public void StartPhase(IEnumerator body, UnityEvent started, UnityEvent finished)
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
            _state = CarryableToolState.Busy;
            started?.Invoke();
            yield return body;
            finished?.Invoke();
            _phaseRoutine = null;

            if (previous == CarryableToolState.Carried)
            {
                _state = CarryableToolState.Carried;
            }
        }

        private void FinishReturnHome()
        {
            transform.SetPositionAndRotation(_startPose.position, _startPose.rotation);
            _groundRestPose = _startPose;
            ApplyOnGround();
        }

        private void ApplyOnGround()
        {
            _state = CarryableToolState.OnGround;
            SetPickupColliderEnabled(true);
            CarryableGameplayLock.SetCanPickup(true);
            SetCursorVisible(true);
        }

        private void OnDisable()
        {
            CarryableGameplayLock.SetCanPickup(true);
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
            Cursor.lockState = CursorLockMode.None;
        }
    }
}
