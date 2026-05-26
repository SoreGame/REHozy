using REHozy.CarryableTools;
using REHozy.Decoration;
using UnityEngine;

namespace REHozy
{
    /// <summary>
    /// Blocks world gameplay input while a modal UI (e.g. quest list) is open.
    /// Shows the cursor; restores gameplay cursor state on close.
    /// </summary>
    public static class GameplayUiLock
    {
        private static int _depth;

        public static bool IsActive => _depth > 0;

        public static void SetActive(bool active)
        {
            if (active)
            {
                Push();
            }
            else
            {
                Pop();
            }
        }

        public static void Push()
        {
            if (_depth == 0)
            {
                CarryableGameplayLock.SetCanPickup(false);
            }

            _depth++;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public static void Pop()
        {
            if (_depth <= 0)
            {
                return;
            }

            _depth--;
            if (_depth != 0)
            {
                return;
            }

            DecorationGameplayLock.RestoreToolPickupIfAllowed();
            ApplyGameplayCursor();
        }

        /// <summary>
        /// Matches tool/decoration carry rules. Uses Locked+hidden so the OS cursor
        /// disappears immediately (visible=false with lock None can stick until the next click).
        /// </summary>
        public static void ApplyGameplayCursor()
        {
            if (IsActive)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            if (ShouldHideGameplayCursor())
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private static bool ShouldHideGameplayCursor()
        {
            if (DecorationCarrySession.IsCarrying)
            {
                return true;
            }

            var cores = Object.FindObjectsByType<CarryableToolCore>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            foreach (var core in cores)
            {
                if (core.State is CarryableToolState.Carried
                    or CarryableToolState.Busy
                    or CarryableToolState.Returning)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
