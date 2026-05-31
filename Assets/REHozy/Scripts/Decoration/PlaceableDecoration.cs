using REHozy.Audio;
using REHozy.CarryableTools;
using UnityEngine;

namespace REHozy.Decoration
{
    [DefaultExecutionOrder(50)]
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
            EnsurePlacementPivotAtVisualBottom();
        }

        private void Start()
        {
            EnsurePlacementPivotAtVisualBottom();
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
            EnsurePlacementPivotAtVisualBottom();
            carryDriver?.ResetCarryMotion(transform.position);
            _placementPreview?.SetVisible(true);
            SetCursorVisible(false);
            GameAudio.Play(GameSoundId.PropPickup, transform.position);
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

        public void AddCarryYawDegrees(float deltaDegrees)
        {
            if (_state != PlaceableDecorationState.Carried || Mathf.Abs(deltaDegrees) < Mathf.Epsilon)
            {
                return;
            }

            _carryYawDegrees = Mathf.Repeat(_carryYawDegrees + deltaDegrees, 360f);
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

            if (!TryGetPlacementPose(camera, out var rootPosition, out var rootRotation, out _, out _, out var isValid))
            {
                return false;
            }

            if (!isValid)
            {
                return false;
            }

            transform.SetPositionAndRotation(rootPosition, rootRotation);
            GameAudio.Play(GameSoundId.PropPlace, rootPosition);
            ApplyPlacedState();
            return true;
        }

        private void UpdatePlacementPreview(UnityEngine.Camera camera)
        {
            if (_placementPreview == null)
            {
                return;
            }

            if (!TryGetPlacementPose(camera, out _, out var rootRotation, out var anchor, out var surfaceNormal, out var isValid))
            {
                CanPlaceAtCurrentPreview = false;
                return;
            }

            CanPlaceAtCurrentPreview = isValid;
            _placementPreview.UpdatePreview(anchor, surfaceNormal, rootRotation, isValid, _cachedVisualBounds);
        }

        private bool TryGetPlacementPose(
            UnityEngine.Camera camera,
            out Vector3 rootPosition,
            out Quaternion rootRotation,
            out Vector3 anchor,
            out Vector3 surfaceNormal,
            out bool isValid)
        {
            rootPosition = default;
            rootRotation = Quaternion.identity;
            anchor = default;
            surfaceNormal = Vector3.up;
            isValid = false;

            if (!DecorationPlacementUtility.TryResolvePlacementAnchor(
                    camera, groundMask, out anchor, out surfaceNormal))
            {
                return false;
            }

            if (DecorationPlacementUtility.TrySampleTopGroundAt(
                    anchor,
                    groundMask,
                    transform,
                    out var groundHit))
            {
                anchor = new Vector3(anchor.x, groundHit.point.y, anchor.z);
                surfaceNormal = groundHit.normal.sqrMagnitude > 0.0001f ? groundHit.normal : surfaceNormal;
            }

            EnsurePlacementPivotAtVisualBottom();

            isValid = DecorationPlacementUtility.IsValidPlacementAnchor(anchor);
            var referenceRotation = _state == PlaceableDecorationState.Carried
                ? Quaternion.Euler(0f, _carryYawDegrees, 0f)
                : transform.rotation;
            rootRotation = DecorationPlacementUtility.AlignRotationToSurface(referenceRotation, surfaceNormal);
            DecorationPlacementUtility.ComputeRootPositionAtAnchor(
                transform,
                Pivot,
                anchor,
                surfaceNormal,
                groundSnapOffset,
                rootRotation,
                out rootPosition);
            return true;
        }

        private void EnsurePlacementPivotAtVisualBottom()
        {
            if (placementPivot == null)
            {
                return;
            }

            if (!TryComputeBottomLocalY(out var bottomLocalY))
            {
                return;
            }

            var pivotLocal = placementPivot.localPosition;
            placementPivot.localPosition = new Vector3(pivotLocal.x, bottomLocalY, pivotLocal.z);
        }

        private bool TryComputeBottomLocalY(out float bottomLocalY)
        {
            bottomLocalY = float.PositiveInfinity;
            var found = false;

            var meshFilters = GetComponentsInChildren<MeshFilter>(true);
            for (var i = 0; i < meshFilters.Length; i++)
            {
                var meshFilter = meshFilters[i];
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    continue;
                }

                if (meshFilter.gameObject.name == "PlacementPreview")
                {
                    continue;
                }

                if (TryGetLowestRootLocalY(meshFilter.transform, meshFilter.sharedMesh.bounds, ref bottomLocalY))
                {
                    found = true;
                }
            }

            var renderers = GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || renderer is ParticleSystemRenderer || renderer.gameObject.name == "PlacementPreview")
                {
                    continue;
                }

                if (TryGetLowestRootLocalYFromWorldBounds(renderer.bounds, ref bottomLocalY))
                {
                    found = true;
                }
            }

            for (var i = 0; i < _colliders.Length; i++)
            {
                var col = _colliders[i];
                if (col == null)
                {
                    continue;
                }

                if (TryGetLowestRootLocalYFromWorldBounds(col.bounds, ref bottomLocalY))
                {
                    found = true;
                }
            }

            if (!found || bottomLocalY == float.PositiveInfinity)
            {
                bottomLocalY = 0f;
                return false;
            }

            return true;
        }

        private bool TryGetLowestRootLocalY(Transform piece, Bounds localBounds, ref float bottomLocalY)
        {
            var min = localBounds.min;
            var max = localBounds.max;
            var found = false;

            for (var x = 0; x < 2; x++)
            {
                for (var y = 0; y < 2; y++)
                {
                    for (var z = 0; z < 2; z++)
                    {
                        var corner = new Vector3(
                            x == 0 ? min.x : max.x,
                            y == 0 ? min.y : max.y,
                            z == 0 ? min.z : max.z);
                        var rootLocalY = transform.InverseTransformPoint(piece.TransformPoint(corner)).y;
                        if (!found || rootLocalY < bottomLocalY)
                        {
                            bottomLocalY = rootLocalY;
                            found = true;
                        }
                    }
                }
            }

            return found;
        }

        private bool TryGetLowestRootLocalYFromWorldBounds(Bounds worldBounds, ref float bottomLocalY)
        {
            var min = worldBounds.min;
            var max = worldBounds.max;
            var found = false;

            for (var x = 0; x < 2; x++)
            {
                for (var y = 0; y < 2; y++)
                {
                    for (var z = 0; z < 2; z++)
                    {
                        var corner = new Vector3(
                            x == 0 ? min.x : max.x,
                            y == 0 ? min.y : max.y,
                            z == 0 ? min.z : max.z);
                        var rootLocalY = transform.InverseTransformPoint(corner).y;
                        if (!found || rootLocalY < bottomLocalY)
                        {
                            bottomLocalY = rootLocalY;
                            found = true;
                        }
                    }
                }
            }

            return found;
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
