using REHozy.CarryableTools;
using UnityEngine;
using UnityEngine.InputSystem;

namespace REHozy.Decoration
{
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Decoration/Decoration Input Handler")]
    public sealed class DecorationInputHandler : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private UnityEngine.Camera rayCamera;
        [SerializeField] private InputActionReference attackAction;
        [SerializeField] private InputActionAsset inputActionsFallback;
        [SerializeField] private float pickupMaxDistance = 150f;
        [SerializeField] private LayerMask interactionMask = ~0;
        [SerializeField] private float clickMaxDuration = 0.25f;
        [SerializeField] private bool useMouseButtonFallback = true;

        [Header("Carry rotation")]
        [SerializeField] private float scrollYawDegreesPerNotch = 5f;

        private InputAction _attack;
        private InputActionMap _playerMap;
        private InputAction _uiScrollAction;
        private InputAction _decorationScrollAction;
        private float _pressStartTime;
        private bool _clickConsumed;
        private float _queuedScrollYaw;
        private int _lastScrollFrame = -1;

        private void Awake()
        {
            ResolveInputActions();
            ResolveDecorationScrollAction();
        }

        private void OnEnable()
        {
            UnityEngine.InputSystem.InputSystem.onBeforeUpdate += QueueScrollBeforeInputUpdate;
            _playerMap?.Enable();
            _attack?.Enable();
            _decorationScrollAction?.Enable();
        }

        private void OnDisable()
        {
            UnityEngine.InputSystem.InputSystem.onBeforeUpdate -= QueueScrollBeforeInputUpdate;
            _attack?.Disable();
            _playerMap?.Disable();
            _decorationScrollAction?.Disable();
            _queuedScrollYaw = 0f;
        }

        private void OnDestroy()
        {
            if (_decorationScrollAction == null)
            {
                return;
            }

            _decorationScrollAction.performed -= OnDecorationScrollPerformed;
            _decorationScrollAction.Dispose();
            _decorationScrollAction = null;
        }

        private void Update()
        {
            if (REHozy.GameplayUiLock.IsActive)
            {
                _queuedScrollYaw = 0f;
                return;
            }

            if (DecorationCarrySession.IsCarrying)
            {
                UpdateWhileCarrying();
                return;
            }

            _queuedScrollYaw = 0f;

            if (DecorationGameplayLock.IsAnyToolOccupyingHands())
            {
                return;
            }

            if (_attack == null && !useMouseButtonFallback)
            {
                return;
            }

            UpdateWhileIdle();
        }

        private void ResolveDecorationScrollAction()
        {
            _decorationScrollAction = new InputAction(
                name: "DecorationScrollRotate",
                type: InputActionType.Value,
                expectedControlType: "Vector2");
            _decorationScrollAction.AddBinding("<Mouse>/scroll");
            _decorationScrollAction.performed += OnDecorationScrollPerformed;
        }

        private void OnDecorationScrollPerformed(InputAction.CallbackContext context)
        {
            if (!DecorationCarrySession.IsCarrying || REHozy.GameplayUiLock.IsActive)
            {
                return;
            }

            var scrollY = context.ReadValue<Vector2>().y;
            QueueScrollYaw(scrollY);
        }

        private void QueueScrollBeforeInputUpdate()
        {
            if (!DecorationCarrySession.IsCarrying || REHozy.GameplayUiLock.IsActive)
            {
                return;
            }

            if (!TryReadScrollY(out var scrollY))
            {
                return;
            }

            QueueScrollYaw(scrollY);
        }

        private void QueueScrollYaw(float scrollY)
        {
            if (Mathf.Abs(scrollY) < 0.001f)
            {
                return;
            }

            if (_lastScrollFrame == Time.frameCount)
            {
                return;
            }

            _lastScrollFrame = Time.frameCount;
            _queuedScrollYaw += Mathf.Sign(scrollY) * scrollYawDegreesPerNotch;
        }

        private void UpdateWhileCarrying()
        {
            var active = DecorationCarrySession.Active;
            if (active == null)
            {
                return;
            }

            if (Mathf.Abs(_queuedScrollYaw) > Mathf.Epsilon)
            {
                active.AddCarryYawDegrees(_queuedScrollYaw);
                _queuedScrollYaw = 0f;
            }

            var cam = ResolveCamera();
            active.TickCarried(cam);

            if (WasAttackPressedThisFrame())
            {
                _pressStartTime = Time.time;
                _clickConsumed = false;
            }

            if (WasAttackReleasedThisFrame() && !_clickConsumed
                && Time.time - _pressStartTime <= clickMaxDuration)
            {
                _clickConsumed = true;
                if (cam != null)
                {
                    active.TryPlaceAtCursor(cam);
                }
            }
        }

        private void UpdateWhileIdle()
        {
            if (WasAttackPressedThisFrame())
            {
                _pressStartTime = Time.time;
                _clickConsumed = false;
            }

            if (WasAttackReleasedThisFrame() && !_clickConsumed
                && Time.time - _pressStartTime <= clickMaxDuration)
            {
                _clickConsumed = true;
                TryInteractOnShortClick();
            }
        }

        private void TryInteractOnShortClick()
        {
            if (TryPickPlacedDecorationUnderCursor(out var decoration))
            {
                decoration.EnterCarried();
                return;
            }

            if (TryRaycastSpawnBoxUnderCursor(out var box))
            {
                TrySpawnFromBox(box);
            }
        }

        private bool TryPickPlacedDecorationUnderCursor(out PlaceableDecoration decoration)
        {
            decoration = null;

            if (!TryRaycastInteraction(out var hits))
            {
                return false;
            }

            foreach (var hit in hits)
            {
                if (hit.collider == null)
                {
                    continue;
                }

                var candidate = hit.collider.GetComponentInParent<PlaceableDecoration>();
                if (candidate == null || !candidate.CanPickUp)
                {
                    continue;
                }

                decoration = candidate;
                return true;
            }

            return false;
        }

        private bool TryRaycastSpawnBoxUnderCursor(out PropSpawnBox box)
        {
            box = null;

            if (!TryRaycastInteraction(out var hits))
            {
                return false;
            }

            foreach (var hit in hits)
            {
                if (hit.collider == null)
                {
                    continue;
                }

                var candidate = hit.collider.GetComponentInParent<PropSpawnBox>();
                if (candidate == null || !candidate.HasRemaining)
                {
                    continue;
                }

                box = candidate;
                return true;
            }

            return false;
        }

        private void TrySpawnFromBox(PropSpawnBox box)
        {
            if (box == null || !box.HasRemaining || !box.TryDrawRandom(out var prefab) || prefab == null)
            {
                return;
            }

            var instance = Instantiate(prefab, box.GetSpawnPosition(), box.GetSpawnRotation());
            var decoration = instance.GetComponent<PlaceableDecoration>();
            if (decoration == null)
            {
                Debug.LogWarning(
                    $"[DecorationInputHandler] Spawned prefab '{prefab.name}' has no PlaceableDecoration.",
                    prefab);
                return;
            }

            decoration.EnterCarried();
        }

        private bool TryRaycastInteraction(out RaycastHit[] hits)
        {
            hits = System.Array.Empty<RaycastHit>();

            var cam = ResolveCamera();
            if (cam == null)
            {
                return false;
            }

            if (!CarryableMouseRay.TryGetRay(cam, out var ray))
            {
                return false;
            }

            hits = Physics.RaycastAll(ray, pickupMaxDistance, interactionMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            return hits.Length > 0;
        }

        private UnityEngine.Camera ResolveCamera()
        {
            return rayCamera != null ? rayCamera : UnityEngine.Camera.main;
        }

        private void ResolveInputActions()
        {
            if (attackAction != null && attackAction.action != null)
            {
                _attack = attackAction.action;
            }
            else if (inputActionsFallback != null)
            {
                _playerMap = inputActionsFallback.FindActionMap("Player", false);
                _playerMap?.Enable();
                _attack = _playerMap?.FindAction("Attack", false);
            }

            if (inputActionsFallback == null)
            {
                return;
            }

            var uiMap = inputActionsFallback.FindActionMap("UI", false);
            _uiScrollAction = uiMap?.FindAction("ScrollWheel", false);
        }

        private bool TryReadScrollY(out float scrollY)
        {
            scrollY = 0f;

            var mouse = Mouse.current;
            if (mouse != null)
            {
                var deviceScrollY = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(deviceScrollY) >= 0.001f)
                {
                    scrollY = deviceScrollY;
                    return true;
                }
            }

            if (_uiScrollAction != null && _uiScrollAction.WasPerformedThisFrame())
            {
                var actionScrollY = _uiScrollAction.ReadValue<Vector2>().y;
                if (Mathf.Abs(actionScrollY) >= 0.001f)
                {
                    scrollY = actionScrollY;
                    return true;
                }
            }

            return false;
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
    }
}
