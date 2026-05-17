namespace REHozy.Rendering
{
    public static class ColorSpreadPaletteMask
    {
        public const int Red = 1;
        public const int Blue = 2;
        public const int Green = 4;
        public const int All = 8;

        public static int FromStep(ColorSpreadStep step) =>
            step switch
            {
                ColorSpreadStep.RedTones => Red,
                ColorSpreadStep.BlueTones => Blue,
                ColorSpreadStep.GreenTones => Green,
                ColorSpreadStep.FullColor => All,
                _ => 0
            };
    }
}
