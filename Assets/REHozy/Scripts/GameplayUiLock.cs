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
                FreezeCarriedToolsMotion();
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

            FreezeCarriedToolsMotion();
            DecorationGameplayLock.RestoreToolPickupIfAllowed();
            ApplyGameplayCursor();
        }

        private static void FreezeCarriedToolsMotion()
        {
            var cores = Object.FindObjectsByType<CarryableToolCore>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            foreach (var core in cores)
            {
                core.FreezeCarryMotionAtCurrentPose();
            }
        }

        /// <summary>
        /// Matches tool/decoration carry rules.
        /// Note: tools use mouse screen position for aiming; CursorLockMode.Locked can freeze Mouse.position
        /// in the new Input System, causing carried tools to aim at screen center after UI closes.
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
                Cursor.lockState = CursorLockMode.None;
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
                if (core.HidesGameplayCursor)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
