using UnityEngine;

namespace REHozy.Watering
{
    public interface IWaterable
    {
        void TryWater(Vector3 waterPoint, float amount, float deltaTime);
    }
}
