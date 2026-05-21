namespace REHozy.CarryableTools
{
    public interface ICarryableToolActions
    {
        bool HasCargo(CarryableToolCore tool);
        bool CanReturnHome(CarryableToolCore tool);
        bool OnCarriedClick(CarryableToolCore tool);
        void OnHoldCompleteInHome(CarryableToolCore tool);
        void OnHoldCompleteOutsideHome(CarryableToolCore tool);
    }
}
