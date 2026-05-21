using UnityEngine;
using UnityEngine.InputSystem;

namespace REHozy.Harpoon
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Harpoon/Harpoon Input Handler")]
    public sealed class HarpoonInputHandler : MonoBehaviour
    {
        [SerializeField] private HarpoonController harpoon;
        [SerializeField] private UnityEngine.Camera rayCamera;
        [SerializeField] private InputActionReference attackAction;
        [SerializeField] private InputActionAsset inputActionsFallback;
        [SerializeField] private float pickupMaxDistance = 150f;
        [SerializeField] private LayerMask harpoonPickupMask = ~0;
        [SerializeField] private float clickMaxDuration = 0.25f;
        [SerializeField] private bool useMouseButtonFallback = true;

        private InputAction _attack;
        private InputActionMap _playerMap;
        private float _pressStartTime;
        private bool _holdActionTriggered;

        /// <summary>True while LMB is held during carry and return-hold has not fired yet.</summary>
        public bool IsCarriedReturnHoldInProgress { get; private set; }

        /// <summary>0–1 progress toward return-hold completion; 0 when not in progress.</summary>
        public float CarriedReturnHoldProgress01 { get; private set; }

        private void Awake()
        {
            if (harpoon == null)
            {
                harpoon = FindFirstObjectByType<HarpoonController>();
            }

            ResolveInputActions();

            if (harpoon == null)
            {
                Debug.LogWarning(
                    "[HarpoonInputHandler] HarpoonController not assigned and none found in scene.",
                    this);
            }

            if (_attack == null && !useMouseButtonFallback)
            {
                Debug.LogWarning(
                    "[HarpoonInputHandler] Attack action missing. Assign InputAttack or InputSystem_Actions.",
                    this);
            }
        }

        private void OnEnable()
        {
            _playerMap?.Enable();
            _attack?.Enable();
            HarpoonGameplayLock.SetCanPickup(true);
        }

        private void OnDisable()
        {
            _attack?.Disable();
            _playerMap?.Disable();
            HarpoonGameplayLock.SetCanPickup(true);
        }

        private void Update()
        {
            if (harpoon == null)
            {
                return;
            }

            if (_attack == null && !useMouseButtonFallback)
            {
                return;
            }

            switch (harpoon.State)
            {
                case HarpoonState.OnGround:
                    ClearCarriedReturnHoldProgress();
                    UpdateOnGroundInput();
                    break;
                case HarpoonState.Carried:
                    UpdateCarriedInput();
                    break;
                case HarpoonState.Returning:
                    ClearCarriedReturnHoldProgress();
                    harpoon.TickReturning();
                    break;
                default:
                    ClearCarriedReturnHoldProgress();
                    break;
            }
        }

        private void ClearCarriedReturnHoldProgress()
        {
            IsCarriedReturnHoldInProgress = false;
            CarriedReturnHoldProgress01 = 0f;
        }

        private void UpdateOnGroundInput()
        {
            if (WasAttackPressedThisFrame())
            {
                _pressStartTime = Time.time;
                _holdActionTriggered = false;

                if (TryRaycastHarpoon(out _))
                {
                    harpoon.EnterCarried();
                }

                return;
            }

            if (WasAttackReleasedThisFrame() && !_holdActionTriggered
                && Time.time - _pressStartTime <= clickMaxDuration
                && TryRaycastHarpoon(out _))
            {
                harpoon.EnterCarried();
            }
        }

        private void UpdateCarriedInput()
        {
            harpoon.TickCarried();

            if (harpoon.State != HarpoonState.Carried)
            {
                ClearCarriedReturnHoldProgress();
                return;
            }

            if (WasAttackPressedThisFrame())
            {
                _pressStartTime = Time.time;
                _holdActionTriggered = false;
            }

            if (IsAttackPressed() && !_holdActionTriggered)
            {
                if (harpoon.IsInHomeZone())
                {
                    var held = Time.time - _pressStartTime;
                    var duration = Mathf.Max(harpoon.DropHoldDuration, 0.01f);
                    IsCarriedReturnHoldInProgress = true;
                    CarriedReturnHoldProgress01 = Mathf.Clamp01(held / duration);

                    if (held >= duration)
                    {
                        _holdActionTriggered = true;
                        ClearCarriedReturnHoldProgress();
                        if (harpoon.HasMountedItem)
                        {
                            harpoon.StartBlockedReturnHold();
                        }
                        else
                        {
                            harpoon.StartReturnHome();
                        }
                    }
                }

                return;
            }

            ClearCarriedReturnHoldProgress();

            if (WasAttackReleasedThisFrame() && !_holdActionTriggered)
            {
                if (Time.time - _pressStartTime <= clickMaxDuration)
                {
                    TryCarriedClick();
                }
            }
        }

        private void TryCarriedClick()
        {
            if (harpoon.TryDisposeOnClick())
            {
                return;
            }

            harpoon.TryImpaleOnClick();
        }

        private bool TryRaycastHarpoon(out RaycastHit hit)
        {
            hit = default;
            if (!harpoon.CanBePickedUp())
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
            var hits = Physics.RaycastAll(ray, pickupMaxDistance, harpoonPickupMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var candidate in hits)
            {
                if (candidate.collider == null)
                {
                    continue;
                }

                if (candidate.collider.GetComponentInParent<HarpoonController>() == harpoon)
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
