using UnityEngine;

namespace REHozy.CarryableTools
{
    /// <summary>
    /// Optional work-pose magnet on the same GameObject as <see cref="CarryableCarryDriver"/>.
    /// Blends spout/tip facing toward a nearby target while the player can still overpower it via cursor distance.
    /// </summary>
    public interface ICarryableWorkMagnet
    {
        bool TryGetWorkMagnet(
            UnityEngine.Camera camera,
            Transform tipPivot,
            Vector3 cursorGroundAnchor,
            out Vector3 targetWorldPoint,
            out float strength01);
    }
}
