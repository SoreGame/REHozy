namespace REHozy.CarryableTools
{
    public static class CarryableGameplayLock
    {
        public static bool CanPickup { get; private set; } = true;

        public static void SetCanPickup(bool value) => CanPickup = value;
    }
}
