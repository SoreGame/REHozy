using REHozy.Decoration;
using UnityEngine;

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
        [SerializeField] private bool invertY = false;

        [Header("Zoom")]
        [SerializeField] private float distance = 8f;
        [SerializeField] private Vector2 distanceLimits = new Vector2(2f, 18f);
        [SerializeField] private float zoomSensitivity = 2.5f;
        [SerializeField] private float zoomDragSensitivity = 0.02f;

        [Header("Input")]
        [SerializeField] private int rotateMouseButton = 1; // 1 = RMB
        [SerializeField] private bool zoomWithoutButton = true;

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

            var rotateHeld = IsMouseButtonHeld(rotateMouseButton);
            var carryingDecoration = DecorationCarrySession.IsCarrying;

            if (rotateHeld)
            {
                var delta = GetMouseDelta();
                yaw += delta.x * rotationSensitivity;

                var ySign = invertY ? 1f : -1f;
                if (carryingDecoration)
                {
                    pitch += delta.y * rotationSensitivity * ySign;
                    pitch = Mathf.Clamp(pitch, pitchLimits.x, pitchLimits.y);
                }
                else
                {
                    var zoomSign = invertY ? -1f : 1f;
                    distance -= delta.y * zoomDragSensitivity * zoomSign;
                    distance = Mathf.Clamp(distance, distanceLimits.x, distanceLimits.y);
                }
            }

            if ((zoomWithoutButton || rotateHeld) && !carryingDecoration)
            {
                var scroll = GetScroll();
                if (Mathf.Abs(scroll) > 0.0001f)
                {
                    distance -= scroll * zoomSensitivity;
                    distance = Mathf.Clamp(distance, distanceLimits.x, distanceLimits.y);
                }
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
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return Vector2.zero;
            return mouse.delta.ReadValue();
#else
            return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
#endif
        }

        private static float GetScroll()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return 0f;
            // scroll.y is typically +/-120 per notch on Windows; normalize.
            return mouse.scroll.ReadValue().y / 120f;
#else
            return Input.mouseScrollDelta.y;
#endif
        }
    }
}

