using UnityEngine;

namespace REHozy.CarryableTools
{
    /// <summary>
    /// Optional aim resolver on the same GameObject as <see cref="CarryableCarryDriver"/>.
    /// Used e.g. by shovel to slide on a fixed work plane while digging (avoids snapping to roofs).
    /// </summary>
    public interface ICarryableAimOverride
    {
        bool TryOverrideAim(UnityEngine.Camera camera, out Vector3 anchor, out Vector3 planeNormal);
    }
}
