using REHozy.CarryableTools;
using UnityEngine;

namespace REHozy.Decoration
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Decoration/Placeable Decoration")]
    public sealed class PlaceableDecoration : MonoBehaviour
    {
        [SerializeField] private Collider pickupCollider;
        [SerializeField] private CarryableCarryDriver carryDriver;
        [SerializeField] private Transform placementPivot;
        [SerializeField] private float groundSnapOffset;
        [SerializeField] private LayerMask groundMask = ~0;

        [Header("Carry")]
        [SerializeField] private float carryPositionSmoothTime = 0.1f;

        [Header("Carry rotation")]
        [Tooltip("Yaw degrees per mouse-wheel notch. Only changes while scrolling; no inertia.")]
        [SerializeField] private float scrollYawDegreesPerNotch = 5f;

        private PlaceableDecorationState _state = PlaceableDecorationState.Placed;
        private float _carryYawDegrees;
        private Vector3 _carryPositionVelocity;
        private Collider[] _colliders = System.Array.Empty<Collider>();
        private Rigidbody _rigidbody;
        private DecorationPlacementPreview _placementPreview;
        private Bounds _cachedVisualBounds;

        public PlaceableDecorationState State => _state;

        public bool CanPickUp =>
            _state == PlaceableDecorationState.Placed
            && !DecorationCarrySession.IsCarrying;

        public bool CanPlaceAtCurrentPreview { get; private set; }

        private Transform Pivot => placementPivot != null ? placementPivot : transform;

        private void Reset()
        {
            pickupCollider = GetComponent<Collider>();
            carryDriver = GetComponent<CarryableCarryDriver>();
        }

        private void Awake()
        {
            if (pickupCollider == null)
            {
                pickupCollider = GetComponent<Collider>();
            }

            if (carryDriver == null)
            {
                carryDriver = GetComponent<CarryableCarryDriver>();
            }

            _colliders = GetComponentsInChildren<Collider>();
            _rigidbody = GetComponent<Rigidbody>();
            _placementPreview = GetComponent<DecorationPlacementPreview>();
            if (_placementPreview == null)
            {
                _placementPreview = gameObject.AddComponent<DecorationPlacementPreview>();
            }

            _placementPreview.SetVisible(false);
            ApplyPlacedPhysics();
        }

        private void OnDisable()
        {
            _placementPreview?.SetVisible(false);

            if (DecorationCarrySession.Active == this)
            {
                DecorationCarrySession.Clear(this);
                DecorationGameplayLock.RestoreToolPickupIfAllowed();
            }
        }

        public void EnterCarried()
        {
            if (_state != PlaceableDecorationState.Placed || DecorationCarrySession.IsCarrying)
            {
                return;
            }

            _state = PlaceableDecorationState.Carried;
            DecorationCarrySession.SetActive(this);
            DecorationGameplayLock.BlockToolPickup();

            SetCollidersEnabled(false);
            CacheVisualBounds();

            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = true;
            }

            _carryYawDegrees = transform.eulerAngles.y;
            _carryPositionVelocity = Vector3.zero;
            carryDriver?.ResetCarryMotion(transform.position);
            _placementPreview?.SetVisible(true);
            SetCursorVisible(false);
        }

        public void ApplyScrollRotation(float scrollNotches)
        {
            if (_state != PlaceableDecorationState.Carried || Mathf.Abs(scrollNotches) < 0.0001f)
            {
                return;
            }

            _carryYawDegrees += scrollNotches * scrollYawDegreesPerNotch;
            transform.rotation = Quaternion.Euler(0f, _carryYawDegrees, 0f);
        }

        public void TickCarried(UnityEngine.Camera camera)
        {
            if (_state != PlaceableDecorationState.Carried)
            {
                return;
            }

            if (carryDriver != null)
            {
                ApplyCarryPositionOnly();
            }

            transform.rotation = Quaternion.Euler(0f, _carryYawDegrees, 0f);
            UpdatePlacementPreview(camera);
        }

        private void ApplyCarryPositionOnly()
        {
            if (carryDriver == null || !carryDriver.TryGetCarryPose(out var targetPosition, out _))
            {
                return;
            }

            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPosition,
                ref _carryPositionVelocity,
                Mathf.Max(carryPositionSmoothTime, 0.01f));
        }

        public bool TryPlaceAtCursor(UnityEngine.Camera camera)
        {
            if (_state != PlaceableDecorationState.Carried || camera == null)
            {
                return false;
            }

            if (!TryGetPlacementPose(camera, out var rootPosition, out var rootRotation, out _, out var isValid))
            {
                return false;
            }

            if (!isValid)
            {
                return false;
            }

            transform.SetPositionAndRotation(rootPosition, rootRotation);
            ApplyPlacedState();
            return true;
        }

        private void UpdatePlacementPreview(UnityEngine.Camera camera)
        {
            if (_placementPreview == null)
            {
                return;
            }

            if (!TryGetPlacementPose(camera, out _, out var rootRotation, out var anchor, out var isValid))
            {
                CanPlaceAtCurrentPreview = false;
                return;
            }

            CanPlaceAtCurrentPreview = isValid;
            _placementPreview.UpdatePreview(anchor, rootRotation, isValid, _cachedVisualBounds);
        }

        private bool TryGetPlacementPose(
            UnityEngine.Camera camera,
            out Vector3 rootPosition,
            out Quaternion rootRotation,
            out Vector3 anchor,
            out bool isValid)
        {
            rootPosition = default;
            rootRotation = Quaternion.identity;
            anchor = default;
            isValid = false;

            if (!DecorationPlacementUtility.TryResolvePlacementAnchor(
                    camera, carryDriver, groundMask, out anchor, out var surfaceNormal))
            {
                return false;
            }

            isValid = DecorationPlacementUtility.IsValidPlacementAnchor(anchor);
            DecorationPlacementUtility.ComputeRootPositionAtAnchor(
                transform,
                Pivot,
                anchor,
                surfaceNormal,
                groundSnapOffset,
                out rootPosition);
            rootRotation = DecorationPlacementUtility.AlignRotationToSurface(transform.rotation, surfaceNormal);
            return true;
        }

        private void ApplyPlacedState()
        {
            _state = PlaceableDecorationState.Placed;
            CanPlaceAtCurrentPreview = false;
            _placementPreview?.SetVisible(false);
            DecorationCarrySession.Clear(this);
            DecorationGameplayLock.RestoreToolPickupIfAllowed();
            ApplyPlacedPhysics();
            SetCollidersEnabled(true);
            SetCursorVisible(true);
        }

        private void CacheVisualBounds()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                _cachedVisualBounds = new Bounds(transform.position, Vector3.one * 0.35f);
                return;
            }

            _cachedVisualBounds = default;
            var hasBounds = false;
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].gameObject.name == "PlacementPreview")
                {
                    continue;
                }

                if (!hasBounds)
                {
                    _cachedVisualBounds = renderers[i].bounds;
                    hasBounds = true;
                }
                else
                {
                    _cachedVisualBounds.Encapsulate(renderers[i].bounds);
                }
            }

            if (!hasBounds)
            {
                _cachedVisualBounds = new Bounds(transform.position, Vector3.one * 0.35f);
            }
        }

        private void ApplyPlacedPhysics()
        {
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = true;
            }
        }

        private void SetCollidersEnabled(bool enabled)
        {
            foreach (var col in _colliders)
            {
                if (col != null)
                {
                    col.enabled = enabled;
                }
            }
        }

        private static void SetCursorVisible(bool visible)
        {
            Cursor.visible = visible;
            Cursor.lockState = CursorLockMode.None;
        }
    }
}
