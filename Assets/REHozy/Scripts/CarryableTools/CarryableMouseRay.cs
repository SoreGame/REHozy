using REHozy;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace REHozy.CarryableTools
{
    internal static class CarryableMouseRay
    {
        public static bool TryGetRay(UnityEngine.Camera camera, out Ray ray)
        {
            ray = default;

            if (BlocksWorldAimRay())
            {
                return false;
            }

            if (camera == null || !camera.isActiveAndEnabled)
            {
                return false;
            }

            var pixelRect = camera.pixelRect;
            if (pixelRect.width < 1f || pixelRect.height < 1f)
            {
                return false;
            }

            if (!TryReadMouseScreenPosition(out var screenPos))
            {
                return false;
            }

            screenPos.x = Mathf.Clamp(screenPos.x, pixelRect.xMin, pixelRect.xMax - 0.001f);
            screenPos.y = Mathf.Clamp(screenPos.y, pixelRect.yMin, pixelRect.yMax - 0.001f);

            ray = camera.ScreenPointToRay(screenPos);
            return true;
        }

        private static bool TryReadMouseScreenPosition(out Vector2 screenPos)
        {
            screenPos = default;

            var mouse = Mouse.current;
            if (mouse != null)
            {
                screenPos = mouse.position.ReadValue();
                if (IsFiniteScreenPosition(screenPos))
                {
                    return true;
                }
            }

            var pointer = Pointer.current;
            if (pointer != null)
            {
                screenPos = pointer.position.ReadValue();
                if (IsFiniteScreenPosition(screenPos))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsFiniteScreenPosition(Vector2 screenPos)
        {
            return float.IsFinite(screenPos.x)
                && float.IsFinite(screenPos.y)
                && screenPos.x >= -100000f
                && screenPos.y >= -100000f;
        }

        private static bool BlocksWorldAimRay()
        {
            if (GameplayUiLock.IsActive)
            {
                return true;
            }

            var eventSystem = EventSystem.current;
            return eventSystem != null && eventSystem.IsPointerOverGameObject();
        }
    }
}
