using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace REHozy.Camera
{
    /// <summary>
    /// Drives <see cref="CinemachineOrbitalFollow.HorizontalAxis"/> from mouse X while RMB is held.
    /// Works with <see cref="CinemachineHardLookAt"/> for a small horizontal orbit around the tracking target.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CinemachineOrbitalFollow))]
    [AddComponentMenu("REHozy/Camera/Cinemachine Mouse Yaw Input")]
    public sealed class CinemachineMousePitchInput : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CinemachineOrbitalFollow orbitalFollow;
        [SerializeField] private InputActionReference lookAction;
        [SerializeField] private InputActionReference orbitHoldAction;

        [Header("Input")]
        [SerializeField] private float sensitivity = 0.12f;
        [SerializeField] private bool invertX;

        [Header("Yaw limits (degrees)")]
        [SerializeField] private float minYaw = -30f;
        [SerializeField] private float maxYaw = 30f;

        [Header("Startup")]
        [SerializeField] private bool syncPoseOnPlay = true;
        [SerializeField] private bool limitsRelativeToStartPose = true;

        private float _baseYaw;

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

            if (orbitalFollow == null)
            {
                return;
            }

            ApplyHorizontalLimits();

            if (syncPoseOnPlay)
            {
                SyncOrbitalFromCurrentPose();
            }

            _baseYaw = orbitalFollow.HorizontalAxis.Value;
            ApplyHorizontalLimits();
        }

        private void Update()
        {
            if (orbitalFollow == null || !IsOrbitHeld())
            {
                return;
            }

            var delta = ReadLookDelta();
            if (Mathf.Abs(delta.x) < Mathf.Epsilon)
            {
                return;
            }

            var horizontal = orbitalFollow.HorizontalAxis;
            horizontal.TrackValueChange();

            var sign = invertX ? -1f : 1f;
            horizontal.Value += delta.x * sensitivity * sign;
            horizontal.Value = horizontal.ClampValue(horizontal.Value);
            orbitalFollow.HorizontalAxis = horizontal;
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

            var targetPos = GetTrackingWorldPosition();
            var offset = transform.position - targetPos;
            var distance = offset.magnitude;
            if (distance > 0.001f)
            {
                orbitalFollow.Radius = distance;
            }

            orbitalFollow.ForceCameraPosition(transform.position, transform.rotation);
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

        private Vector3 GetTrackingWorldPosition()
        {
            var vcam = GetComponent<CinemachineCamera>();
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
