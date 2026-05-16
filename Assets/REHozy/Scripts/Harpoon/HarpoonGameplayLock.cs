namespace REHozy.Harpoon
{
    /// <summary>
    /// Global harpoon gates (e.g. pickup lock during return Lerp).
    /// </summary>
    public static class HarpoonGameplayLock
    {
        public static bool CanPickup { get; private set; } = true;

        public static void SetCanPickup(bool value) => CanPickup = value;
    }
}
