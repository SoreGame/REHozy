namespace REHozy.CarryableTools
{
    /// <summary>
    /// Stops loops, particles, and other active work when the tool is hidden or unbound (e.g. quest transition).
    /// </summary>
    public interface ICarryableToolForceRelease
    {
        void OnForceReleased(CarryableToolCore tool);
    }
}
