namespace REHozy.Torch
{
    /// <summary>
    /// Procedural torch capsule (height 2, mesh scale Y = 0.55).
    /// Handle at the bottom (static pivot); flame at the top (tilts down when aiming).
    /// </summary>
    public static class TorchLayout
    {
        public const float MeshScaleY = 0.55f;
        public const float HalfHeight = MeshScaleY;
        public const float FullHeight = HalfHeight * 2f;

        /// <summary>Stable carry anchor at the flame end; stays on root (does not tilt).</summary>
        public const float CarryTipHeight = HalfHeight;

        /// <summary>Rotation pivot at the handle (bottom).</summary>
        public const float AimGroupBaseOffset = -HalfHeight;

        public const float MeshCenterOffset = HalfHeight;
        public const float FlameTipOffset = FullHeight;
    }
}
