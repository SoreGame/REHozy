namespace REHozy.CarryableTools
{
    public interface ICarryableToolCarriedUpdate
    {
        void OnCarriedUpdate(CarryableToolCore tool, bool attackHeld, bool returnHoldInProgress);
    }
}
