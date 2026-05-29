using REHozy.Decoration;
using REHozy.Torch;
using UnityEngine;
using UnityEngine.InputSystem;

namespace REHozy.CarryableTools
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Carryable Tools/Carryable Tool Input Handler")]
    public sealed class CarryableToolInputHandler : MonoBehaviour
    {
        [Header("Active Mode")]
        [SerializeField] private PlayerToolMode activeModeOnPlay = PlayerToolMode.Harpoon;

        [Header("Input")]
        [SerializeField] private CarryableToolCore tool;
        [SerializeField] private UnityEngine.Camera rayCamera;
        [SerializeField] private InputActionReference attackAction;
        [SerializeField] private InputActionAsset inputActionsFallback;
        [SerializeField] private float pickupMaxDistance = 150f;
        [SerializeField] private LayerMask pickupMask = ~0;
        [SerializeField] private float clickMaxDuration = 0.25f;
        [SerializeField] private bool useMouseButtonFallback = true;

        private InputAction _attack;
        private InputActionMap _playerMap;
        private float _pressStartTime;
        private bool _holdActionTriggered;
        private ICarryableToolActions _actions;
        private ICarryableToolCarriedUpdate _carriedUpdate;

        public bool IsReturnHoldInProgress { get; private set; }
        public float ReturnHoldProgress01 { get; private set; }

        private void Awake()
        {
            PlayerToolModeState.Active = activeModeOnPlay;

            if (tool == null)
            {
                tool = FindFirstObjectByType<CarryableToolCore>();
            }

            RefreshToolBinding();

            ResolveInputActions();

            if (tool == null)
            {
                Debug.LogWarning(
                    "[CarryableToolInputHandler] CarryableToolCore not assigned and none found in scene.",
                    this);
            }
        }

        private void OnEnable()
        {
            _playerMap?.Enable();
            _attack?.Enable();
            CarryableGameplayLock.SetCanPickup(true);
            RefreshToolBinding();
        }

        private void OnDisable()
        {
            _attack?.Disable();
            _playerMap?.Disable();
            CarryableGameplayLock.SetCanPickup(true);
        }

        private void Update()
        {
            RefreshToolBinding();

            if (REHozy.GameplayUiLock.IsActive)
            {
                ClearReturnHoldProgress();
                return;
            }

            if (DecorationCarrySession.IsCarrying)
            {
                ClearReturnHoldProgress();
                return;
            }

            if (_attack == null && !useMouseButtonFallback)
            {
                return;
            }

            if (tool == null)
            {
                ClearReturnHoldProgress();
                return;
            }

            if (tool.State == CarryableToolState.OnGround)
            {
                ClearReturnHoldProgress();
                UpdateOnGroundInput();
                return;
            }

            if (!IsToolActive())
            {
                ClearReturnHoldProgress();
                return;
            }

            switch (tool.State)
            {
                case CarryableToolState.Carried:
                    UpdateCarriedInput();
                    break;
                case CarryableToolState.Returning:
                    ClearReturnHoldProgress();
                    tool.TickReturning();
                    break;
                default:
                    ClearReturnHoldProgress();
                    break;
            }
        }

        private bool IsToolActive()
        {
            if (tool == null)
            {
                return false;
            }

            if (tool.State is CarryableToolState.Carried or CarryableToolState.Busy)
            {
                return true;
            }

            return PlayerToolModeState.Active == tool.ToolModeId;
        }

        public void RefreshToolBinding()
        {
            var active = PlayerToolModeState.Active;
            if (active == PlayerToolMode.None)
            {
                return;
            }

            if (tool != null && tool.ToolModeId == active)
            {
                return;
            }

            var cores = FindObjectsByType<CarryableToolCore>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var core in cores)
            {
                if (core.ToolModeId != active)
                {
                    continue;
                }

                SetBoundTool(core);
                return;
            }
        }

        private void SetBoundTool(CarryableToolCore core)
        {
            tool = core;
            _actions = tool != null ? tool.GetComponent<ICarryableToolActions>() : null;
            _carriedUpdate = tool != null ? tool.GetComponent<ICarryableToolCarriedUpdate>() : null;

            var reticle = GetComponent<CarryableAimReticleUI>();
            reticle?.BindToTool(core);

            var returnHoldUi = GetComponent<CarryableReturnHoldUI>();
            returnHoldUi?.BindToTool(core);

            EnsureTorchFuelUi(core);
            EnsureTorchMapOutline(core);
        }

        private void EnsureTorchMapOutline(CarryableToolCore core)
        {
            if (core == null || core.ToolModeId != PlayerToolMode.Torch)
            {
                return;
            }

            var outlineController = GetComponent<TorchMapOutlineController>();
            if (outlineController == null)
            {
                outlineController = gameObject.AddComponent<TorchMapOutlineController>();
            }

            outlineController.BindToTool(core);
        }

        private void EnsureTorchFuelUi(CarryableToolCore core)
        {
            if (core == null || core.ToolModeId != PlayerToolMode.Torch)
            {
                return;
            }

            var torchFuelUi = GetComponent<TorchFuelProgressUI>();
            if (torchFuelUi == null)
            {
                torchFuelUi = gameObject.AddComponent<TorchFuelProgressUI>();
            }

            torchFuelUi.BindToTool(core);
        }

        private void ClearReturnHoldProgress()
        {
            IsReturnHoldInProgress = false;
            ReturnHoldProgress01 = 0f;
        }

        private void UpdateOnGroundInput()
        {
            if (WasAttackPressedThisFrame())
            {
                _pressStartTime = Time.time;
                _holdActionTriggered = false;

                if (TryPickUpToolUnderCursor())
                {
                    return;
                }
            }

            if (WasAttackReleasedThisFrame() && !_holdActionTriggered
                && Time.time - _pressStartTime <= clickMaxDuration)
            {
                TryPickUpToolUnderCursor();
            }
        }

        private bool TryPickUpToolUnderCursor()
        {
            if (DecorationCarrySession.IsCarrying)
            {
                return false;
            }

            if (!TryRaycastPickableToolOnGround(out var pickable, out _))
            {
                return false;
            }

            PlayerToolModeState.Active = pickable.ToolModeId;
            SetBoundTool(pickable);
            pickable.EnterCarried();
            return true;
        }

        private void UpdateCarriedInput()
        {
            tool.TickCarried();

            if (tool.State != CarryableToolState.Carried)
            {
                ClearReturnHoldProgress();
                return;
            }

            if (WasAttackPressedThisFrame())
            {
                _pressStartTime = Time.time;
                _holdActionTriggered = false;
            }

            if (IsAttackPressed() && !_holdActionTriggered)
            {
                var held = Time.time - _pressStartTime;
                var duration = Mathf.Max(tool.DropHoldDuration, 0.01f);
                var inHome = tool.IsInHomeZone();

                if (inHome)
                {
                    IsReturnHoldInProgress = true;
                    ReturnHoldProgress01 = Mathf.Clamp01(held / duration);
                }

                if (held >= duration)
                {
                    _holdActionTriggered = true;
                    ClearReturnHoldProgress();
                    if (_actions != null)
                    {
                        if (inHome)
                        {
                            _actions.OnHoldCompleteInHome(tool);
                        }
                        else
                        {
                            _actions.OnHoldCompleteOutsideHome(tool);
                        }
                    }
                }

                var attackHeldDuringHold = IsAttackPressed();
                var returnHoldDuringHold = attackHeldDuringHold && !_holdActionTriggered && tool.IsInHomeZone();
                _carriedUpdate?.OnCarriedUpdate(tool, attackHeldDuringHold, returnHoldDuringHold);
                return;
            }

            ClearReturnHoldProgress();

            var attackHeld = IsAttackPressed();
            _carriedUpdate?.OnCarriedUpdate(tool, attackHeld, returnHoldInProgress: false);

            if (WasAttackReleasedThisFrame() && !_holdActionTriggered)
            {
                if (Time.time - _pressStartTime <= clickMaxDuration)
                {
                    _actions?.OnCarriedClick(tool);
                }
            }
        }

        private bool TryRaycastPickableToolOnGround(out CarryableToolCore pickable, out RaycastHit hit)
        {
            pickable = null;
            hit = default;

            if (!CarryableGameplayLock.CanPickup)
            {
                return false;
            }

            var cam = rayCamera != null ? rayCamera : UnityEngine.Camera.main;
            if (cam == null)
            {
                return false;
            }

            if (!CarryableMouseRay.TryGetRay(cam, out var ray))
            {
                return false;
            }
            var hits = Physics.RaycastAll(ray, pickupMaxDistance, pickupMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var candidate in hits)
            {
                if (candidate.collider == null)
                {
                    continue;
                }

                var core = candidate.collider.GetComponentInParent<CarryableToolCore>();
                if (core == null || core.State != CarryableToolState.OnGround)
                {
                    continue;
                }

                PlayerToolModeState.Active = core.ToolModeId;
                if (!core.CanBePickedUp())
                {
                    continue;
                }

                pickable = core;
                hit = candidate;
                return true;
            }

            return false;
        }

        private void ResolveInputActions()
        {
            if (attackAction != null && attackAction.action != null)
            {
                _attack = attackAction.action;
                return;
            }

            if (inputActionsFallback == null)
            {
                return;
            }

            _playerMap = inputActionsFallback.FindActionMap("Player", false);
            _playerMap?.Enable();
            _attack = _playerMap?.FindAction("Attack", false);
        }

        private bool WasAttackPressedThisFrame()
        {
            if (_attack != null && _attack.WasPressedThisFrame())
            {
                return true;
            }

            return useMouseButtonFallback && Mouse.current != null
                && Mouse.current.leftButton.wasPressedThisFrame;
        }

        private bool WasAttackReleasedThisFrame()
        {
            if (_attack != null && _attack.WasReleasedThisFrame())
            {
                return true;
            }

            return useMouseButtonFallback && Mouse.current != null
                && Mouse.current.leftButton.wasReleasedThisFrame;
        }

        private bool IsAttackPressed()
        {
            if (_attack != null && _attack.IsPressed())
            {
                return true;
            }

            return useMouseButtonFallback && Mouse.current != null
                && Mouse.current.leftButton.isPressed;
        }
    }
}
