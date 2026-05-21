namespace REHozy.CarryableTools
{
    public enum PlayerToolMode
    {
        None,
        Harpoon,
        Brush,
        Water,
        FlameCarrier,
        PropPlacement,
        Garland
    }

    public static class PlayerToolModeState
    {
        public static PlayerToolMode Active { get; set; } = PlayerToolMode.Harpoon;
    }
}
