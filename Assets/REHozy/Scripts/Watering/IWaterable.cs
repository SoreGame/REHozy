using UnityEngine;

namespace REHozy.Watering
{
    public interface IWaterable
    {
        bool IsWateringComplete { get; }

        void TryWater(Vector3 waterPoint, float amount, float deltaTime);
    }
}
