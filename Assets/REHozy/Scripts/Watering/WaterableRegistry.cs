using System.Collections.Generic;

namespace REHozy.Watering
{
    public static class WaterableRegistry
    {
        private static readonly List<IWaterable> Active = new();

        public static IReadOnlyList<IWaterable> ActiveInScene => Active;

        public static void Register(IWaterable waterable)
        {
            if (waterable == null || Active.Contains(waterable))
            {
                return;
            }

            Active.Add(waterable);
        }

        public static void Unregister(IWaterable waterable)
        {
            if (waterable == null)
            {
                return;
            }

            Active.Remove(waterable);
        }
    }
}
