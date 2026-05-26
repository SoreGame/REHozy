using REHozy.Decoration;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;
using UnityEngine.InputSystem;

namespace REHozy.Camera
{
    /// <summary>
    /// RMB + mouse X: camera travels on a horizontal arc around the orbit center, always facing it.
    /// RMB + mouse Y / scroll: zoom (<see cref="CinemachineOrbitalFollow.Radius"/>).
    /// Requires <see cref="CinemachineHardLookAt"/> and a shared follow/look-at target.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CinemachineOrbitalFollow))]
    [AddComponentMenu("REHozy/Camera/Cinemachine Mouse Yaw Input")]
    public sealed class CinemachineMousePitchInput : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CinemachineOrbitalFollow orbitalFollow;
        [Tooltip("If set, camera looks here and orbits this point (offsets on CM components are cleared).")]
        [SerializeField] private Transform focusPoint;
        [SerializeField] private InputActionReference lookAction;
        [SerializeField] private InputActionReference orbitHoldAction;

        [Header("Input")]
        [SerializeField] private float sensitivity = 0.12f;
        [SerializeField] private bool invertX;
        [Tooltip("World-units per mouse pixel while dragging (scaled by distance to pivot).")]
        [SerializeField] private float zoomDragSensitivity = 0.35f;
        [SerializeField] private float zoomScrollSensitivity = 12f;
        [SerializeField] private bool zoomWithoutButton = true;
        [SerializeField] private bool invertY;

        [Header("Zoom")]
        [SerializeField] private float minRadius = 25f;
        [SerializeField] private float maxRadius = 120f;

        [Header("Arc limits (degrees from start pose)")]
        [SerializeField] private float minYaw = -30f;
        [SerializeField] private float maxYaw = 30f;
        [SerializeField] private bool lockVerticalWhileOrbiting = true;

        [Header("Startup")]
        [SerializeField] private bool syncPoseOnPlay = true;
        [SerializeField] private bool limitsRelativeToStartPose = true;
        [SerializeField] private bool configureOrbitRigOnPlay = true;
        [Tooltip("When off, Look At Offset on Hard Look At is kept as authored in the inspector.")]
        [SerializeField] private bool syncLookAtOffsetFromOrbitOffset;

        private float _baseYaw;
        private float _lockedVertical;
        private bool _wasOrbitHeld;
        private CinemachineCamera _vcam;
        private CinemachineHardLookAt _hardLookAt;

        private void Reset()
        {
            orbitalFollow = GetComponent<CinemachineOrbitalFollow>();
        }

        private void OnValidate()
        {
            if (minYaw > maxYaw)
            {
                (minYaw, maxYaw) = (maxYaw, minYaw);
            }

            if (minRadius > maxRadius)
            {
                (minRadius, maxRadius) = (maxRadius, minRadius);
            }

            if (orbitalFollow != null)
            {
                orbitalFollow.Radius = Mathf.Clamp(orbitalFollow.Radius, minRadius, maxRadius);
            }

            ApplyHorizontalLimits();
        }

        private void OnEnable()
        {
            lookAction?.action?.Enable();
            orbitHoldAction?.action?.Enable();
        }

        private void OnDisable()
        {
            lookAction?.action?.Disable();
            orbitHoldAction?.action?.Disable();
        }

        private void Start()
        {
            if (orbitalFollow == null)
            {
                orbitalFollow = GetComponent<CinemachineOrbitalFollow>();
            }

            _vcam = GetComponent<CinemachineCamera>();
            _hardLookAt = GetComponent<CinemachineHardLookAt>();

            if (orbitalFollow == null)
            {
                return;
            }

            if (configureOrbitRigOnPlay)
            {
                ConfigureOrbitRig();
            }

            _lockedVertical = orbitalFollow.VerticalAxis.Value;

            if (syncPoseOnPlay)
            {
                SyncOrbitalFromCurrentPose();
            }
            else
            {
                AlignAxesToCurrentPose();
            }

            _baseYaw = orbitalFollow.HorizontalAxis.Value;
            _lockedVertical = orbitalFollow.VerticalAxis.Value;
            ApplyHorizontalLimits();
        }

        private void ConfigureOrbitRig()
        {
            if (_vcam != null)
            {
                var target = _vcam.Target;

                if (focusPoint != null)
                {
                    target.LookAtTarget = focusPoint;
                    if (target.TrackingTarget == null)
                    {
                        target.TrackingTarget = focusPoint;
                    }

                    orbitalFollow.TargetOffset = target.TrackingTarget.InverseTransformPoint(focusPoint.position);
                    if (_hardLookAt != null)
                    {
                        _hardLookAt.LookAtOffset = Vector3.zero;
                    }
                }
                else if (target.TrackingTarget != null && target.LookAtTarget == null)
                {
                    target.LookAtTarget = target.TrackingTarget;
                }

                _vcam.Target = target;
            }

            if (syncLookAtOffsetFromOrbitOffset && _hardLookAt != null && focusPoint == null)
            {
                _hardLookAt.LookAtOffset = orbitalFollow.TargetOffset;
            }

            var tracker = orbitalFollow.TrackerSettings;
            tracker.BindingMode = BindingMode.LockToTargetWithWorldUp;
            orbitalFollow.TrackerSettings = tracker;
        }

        private void AlignAxesToCurrentPose()
        {
            orbitalFollow.ForceCameraPosition(transform.position, transform.rotation);
            SyncRadiusFromCurrentPose();
        }

        private void EnforceLockedVertical()
        {
            if (!lockVerticalWhileOrbiting)
            {
                return;
            }

            var vertical = orbitalFollow.VerticalAxis;
            if (Mathf.Approximately(vertical.Value, _lockedVertical))
            {
                return;
            }

            vertical.Value = _lockedVertical;
            orbitalFollow.VerticalAxis = vertical;
        }

        private void SyncRadiusFromCurrentPose()
        {
            var pivot = GetOrbitCenterWorldPosition();
            var dist = (transform.position - pivot).magnitude;
            if (dist > 0.001f)
            {
                orbitalFollow.Radius = Mathf.Clamp(dist, minRadius, maxRadius);
            }
        }

        private void Update()
        {
            if (orbitalFollow == null)
            {
                return;
            }

            if (REHozy.GameplayUiLock.IsActive)
            {
                _wasOrbitHeld = false;
                return;
            }

            var carryingDecoration = DecorationCarrySession.IsCarrying;

            if (!carryingDecoration && (zoomWithoutButton || IsOrbitHeld()))
            {
                ApplyScrollZoom();
            }

            var orbitHeld = IsOrbitHeld();
            if (!orbitHeld)
            {
                _wasOrbitHeld = false;
                return;
            }

            var justPressed = !_wasOrbitHeld;
            _wasOrbitHeld = true;

            if (!carryingDecoration)
            {
                EnforceLockedVertical();
            }

            var delta = ClampMouseDelta(ReadLookDelta());
            if (justPressed)
            {
                delta = Vector2.zero;
            }

            var xSign = invertX ? -1f : 1f;
            var ySign = invertY ? -1f : 1f;

            if (Mathf.Abs(delta.x) >= Mathf.Epsilon)
            {
                EnforceLockedVertical();

                var horizontal = orbitalFollow.HorizontalAxis;
                horizontal.TrackValueChange();
                horizontal.Value += delta.x * sensitivity * xSign;
                horizontal.Value = horizontal.ClampValue(horizontal.Value);
                orbitalFollow.HorizontalAxis = horizontal;
            }

            if (Mathf.Abs(delta.y) < Mathf.Epsilon)
            {
                return;
            }

            if (carryingDecoration)
            {
                var vertical = orbitalFollow.VerticalAxis;
                vertical.TrackValueChange();
                vertical.Value += delta.y * sensitivity * ySign;
                vertical.Value = vertical.ClampValue(vertical.Value);
                orbitalFollow.VerticalAxis = vertical;
            }
            else
            {
                ApplyDollyZoom(delta.y * zoomDragSensitivity * ySign);
            }
        }

        private void ApplyScrollZoom()
        {
            var scroll = ReadNormalizedScroll();
            if (Mathf.Abs(scroll) < 0.0001f)
            {
                return;
            }

            ApplyDollyZoom(scroll * zoomScrollSensitivity);
        }

        /// <summary>
        /// Adjusts orbital distance. Positive <paramref name="zoomInAmount"/> = closer.
        /// Only changes <see cref="CinemachineOrbitalFollow.Radius"/> so the rig stays consistent.
        /// </summary>
        private void ApplyDollyZoom(float zoomInAmount)
        {
            if (Mathf.Abs(zoomInAmount) < Mathf.Epsilon)
            {
                return;
            }

            var radius = orbitalFollow.Radius;
            var scaledAmount = zoomInAmount * Mathf.Max(radius * 0.004f, 0.08f);
            orbitalFollow.Radius = Mathf.Clamp(radius - scaledAmount, minRadius, maxRadius);
        }

        private static Vector2 ClampMouseDelta(Vector2 delta, float maxMagnitude = 50f)
        {
            var maxSqr = maxMagnitude * maxMagnitude;
            return delta.sqrMagnitude <= maxSqr ? delta : delta.normalized * maxMagnitude;
        }

        private static float ReadNormalizedScroll()
        {
            var mouse = Mouse.current;
            if (mouse == null)
            {
                return 0f;
            }

            return mouse.scroll.ReadValue().y / 120f;
        }

        /// <summary>
        /// Matches orbital axes to the current vcam transform (keeps framing after edits in the Scene view).
        /// </summary>
        public void SyncOrbitalFromCurrentPose()
        {
            if (orbitalFollow == null)
            {
                return;
            }

            ConfigureOrbitRig();
            AlignAxesToCurrentPose();
            _baseYaw = orbitalFollow.HorizontalAxis.Value;
            _lockedVertical = orbitalFollow.VerticalAxis.Value;
            ApplyHorizontalLimits();
        }

        private void ApplyHorizontalLimits()
        {
            if (orbitalFollow == null)
            {
                return;
            }

            var horizontal = orbitalFollow.HorizontalAxis;
            horizontal.Range = limitsRelativeToStartPose
                ? new Vector2(_baseYaw + minYaw, _baseYaw + maxYaw)
                : new Vector2(minYaw, maxYaw);
            horizontal.Wrap = false;
            horizontal.Value = horizontal.ClampValue(horizontal.Value);
            orbitalFollow.HorizontalAxis = horizontal;
        }

        private Vector3 GetLookAtWorldPosition()
        {
            if (focusPoint != null)
            {
                return focusPoint.position;
            }

            if (_vcam != null && _vcam.Target.LookAtTarget != null)
            {
                var lookAt = _vcam.Target.LookAtTarget;
                var offset = _hardLookAt != null ? _hardLookAt.LookAtOffset : Vector3.zero;
                return lookAt.position + lookAt.rotation * offset;
            }

            return GetOrbitCenterWorldPosition();
        }

        private Vector3 GetOrbitCenterWorldPosition()
        {
            if (focusPoint != null)
            {
                return focusPoint.position;
            }

            var vcam = _vcam != null ? _vcam : GetComponent<CinemachineCamera>();
            var target = vcam != null ? vcam.Target.TrackingTarget : null;
            if (target == null)
            {
                return transform.position - transform.forward * Mathf.Max(orbitalFollow.Radius, 1f);
            }

            return target.position + target.rotation * orbitalFollow.TargetOffset;
        }

        private bool IsOrbitHeld()
        {
            if (orbitHoldAction != null && orbitHoldAction.action != null)
            {
                return orbitHoldAction.action.IsPressed();
            }

            var mouse = Mouse.current;
            return mouse != null && mouse.rightButton.isPressed;
        }

        private Vector2 ReadLookDelta()
        {
            if (lookAction != null && lookAction.action != null)
            {
                return lookAction.action.ReadValue<Vector2>();
            }

            var mouse = Mouse.current;
            return mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
        }
    }
}
