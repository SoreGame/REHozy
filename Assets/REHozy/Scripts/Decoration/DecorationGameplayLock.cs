using REHozy.CarryableTools;
using UnityEngine;

namespace REHozy.Decoration
{
    internal static class DecorationGameplayLock
    {
        public static void BlockToolPickup()
        {
            CarryableGameplayLock.SetCanPickup(false);
        }

        public static void RestoreToolPickupIfAllowed()
        {
            if (DecorationCarrySession.IsCarrying || IsAnyToolOccupyingHands())
            {
                return;
            }

            CarryableGameplayLock.SetCanPickup(true);
        }

        public static bool IsAnyToolOccupyingHands()
        {
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
