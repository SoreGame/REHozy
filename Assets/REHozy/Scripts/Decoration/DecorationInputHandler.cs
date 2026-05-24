using REHozy.CarryableTools;
using UnityEngine;
using UnityEngine.InputSystem;

namespace REHozy.Decoration
{
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

        private InputAction _attack;
        private InputActionMap _playerMap;
        private float _pressStartTime;
        private bool _clickConsumed;

        private void Awake()
        {
            ResolveInputActions();
        }

        private void OnEnable()
        {
            _playerMap?.Enable();
            _attack?.Enable();
        }

        private void OnDisable()
        {
            _attack?.Disable();
            _playerMap?.Disable();
        }

        private void Update()
        {
            if (DecorationCarrySession.IsCarrying)
            {
                UpdateWhileCarrying();
                return;
            }

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

        private void LateUpdate()
        {
            if (!DecorationCarrySession.IsCarrying)
            {
                return;
            }

            var active = DecorationCarrySession.Active;
            if (active == null)
            {
                return;
            }

            var scrollNotches = DecorationScrollReader.ReadScrollNotches();
            if (Mathf.Abs(scrollNotches) > 0.0001f)
            {
                active.ApplyScrollRotation(scrollNotches);
            }
        }

        private void UpdateWhileCarrying()
        {
            var active = DecorationCarrySession.Active;
            if (active == null)
            {
                return;
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

    }
}
