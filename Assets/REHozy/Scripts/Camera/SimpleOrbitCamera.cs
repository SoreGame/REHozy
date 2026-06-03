using REHozy.Decoration;
using UnityEngine;
using UnityEngine.InputSystem;

namespace REHozy.Camera
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Camera/Simple Orbit Camera")]
    public sealed class SimpleOrbitCamera : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 targetOffset = Vector3.zero;

        [Header("Orbit")]
        [SerializeField] private float yaw = 0f;
        [SerializeField] private float pitch = 25f;
        [SerializeField] private Vector2 pitchLimits = new Vector2(-10f, 80f);
        [SerializeField] private float rotationSensitivity = 0.2f;

        [Header("Zoom")]
        [SerializeField] private float distance = 8f;
        [SerializeField] private Vector2 distanceLimits = new Vector2(2f, 18f);
        [SerializeField] private float zoomScrollSensitivity = 0.5f;
        [SerializeField] private bool invertScroll;

        [Header("Input")]
        [SerializeField] private int rotateMouseButton = 1; // 1 = RMB

        private void Reset()
        {
            var cam = GetComponent<UnityEngine.Camera>();
            if (cam != null)
            {
                // Keep defaults; user will assign target in inspector.
            }
        }

        private void OnValidate()
        {
            if (pitchLimits.x > pitchLimits.y)
            {
                (pitchLimits.x, pitchLimits.y) = (pitchLimits.y, pitchLimits.x);
            }

            if (distanceLimits.x > distanceLimits.y)
            {
                (distanceLimits.x, distanceLimits.y) = (distanceLimits.y, distanceLimits.x);
            }

            pitch = Mathf.Clamp(pitch, pitchLimits.x, pitchLimits.y);
            distance = Mathf.Clamp(distance, distanceLimits.x, distanceLimits.y);
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            if (REHozy.GameplayUiLock.IsActive)
            {
                return;
            }

            if (!DecorationCarrySession.IsCarrying)
            {
                TryApplyScrollZoom();
            }

            if (IsMouseButtonHeld(rotateMouseButton))
            {
                var delta = GetMouseDelta();
                yaw += delta.x * rotationSensitivity;
            }

            var pivot = target.position + targetOffset;
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            var pos = pivot + rot * new Vector3(0f, 0f, -distance);

            transform.SetPositionAndRotation(pos, rot);
        }

        private static bool IsMouseButtonHeld(int button)
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Mouse.current == null) return false;
            return button switch
            {
                0 => UnityEngine.InputSystem.Mouse.current.leftButton.isPressed,
                1 => UnityEngine.InputSystem.Mouse.current.rightButton.isPressed,
                2 => UnityEngine.InputSystem.Mouse.current.middleButton.isPressed,
                _ => false
            };
#else
            return Input.GetMouseButton(button);
#endif
        }

        private static Vector2 GetMouseDelta()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null) return Vector2.zero;
            return mouse.delta.ReadValue();
#else
            return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
#endif
        }

        private void TryApplyScrollZoom()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            var scrollY = mouse.scroll.ReadValue().y;
#else
            var scrollY = Input.GetAxis("Mouse ScrollWheel");
#endif
            if (Mathf.Abs(scrollY) < 0.001f)
            {
                return;
            }

            var scrollSign = invertScroll ? -1f : 1f;
            distance -= scrollY * zoomScrollSensitivity * scrollSign;
            distance = Mathf.Clamp(distance, distanceLimits.x, distanceLimits.y);
        }
    }
}

