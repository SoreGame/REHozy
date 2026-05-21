using UnityEngine;
using UnityEngine.InputSystem;

namespace REHozy.CarryableTools
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Carryable Tools/Carryable Tool Input Handler")]
    public sealed class CarryableToolInputHandler : MonoBehaviour
    {
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

        public bool IsReturnHoldInProgress { get; private set; }
        public float ReturnHoldProgress01 { get; private set; }

        private void Awake()
        {
            if (tool == null)
            {
                tool = FindFirstObjectByType<CarryableToolCore>();
            }

            if (tool != null)
            {
                _actions = tool.GetComponent<ICarryableToolActions>();
            }

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

            if (PlayerToolModeState.Active == PlayerToolMode.None && tool != null)
            {
                PlayerToolModeState.Active = tool.ToolModeId;
            }
        }

        private void OnDisable()
        {
            _attack?.Disable();
            _playerMap?.Disable();
            CarryableGameplayLock.SetCanPickup(true);
        }

        private void Update()
        {
            if (tool == null || !IsToolActive())
            {
                ClearReturnHoldProgress();
                return;
            }

            if (_attack == null && !useMouseButtonFallback)
            {
                return;
            }

            switch (tool.State)
            {
                case CarryableToolState.OnGround:
                    ClearReturnHoldProgress();
                    UpdateOnGroundInput();
                    break;
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

        private bool IsToolActive() => PlayerToolModeState.Active == tool.ToolModeId;

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

                if (TryRaycastTool(out _))
                {
                    tool.EnterCarried();
                }

                return;
            }

            if (WasAttackReleasedThisFrame() && !_holdActionTriggered
                && Time.time - _pressStartTime <= clickMaxDuration
                && TryRaycastTool(out _))
            {
                tool.EnterCarried();
            }
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

                return;
            }

            ClearReturnHoldProgress();

            if (WasAttackReleasedThisFrame() && !_holdActionTriggered)
            {
                if (Time.time - _pressStartTime <= clickMaxDuration)
                {
                    _actions?.OnCarriedClick(tool);
                }
            }
        }

        private bool TryRaycastTool(out RaycastHit hit)
        {
            hit = default;
            if (!tool.CanBePickedUp())
            {
                return false;
            }

            var cam = rayCamera != null ? rayCamera : UnityEngine.Camera.main;
            if (cam == null)
            {
                return false;
            }

            var mouse = Mouse.current;
            if (mouse == null)
            {
                return false;
            }

            var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            var hits = Physics.RaycastAll(ray, pickupMaxDistance, pickupMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var candidate in hits)
            {
                if (candidate.collider == null)
                {
                    continue;
                }

                if (candidate.collider.GetComponentInParent<CarryableToolCore>() == tool)
                {
                    hit = candidate;
                    return true;
                }
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
