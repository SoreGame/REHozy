using UnityEngine;

namespace REHozy.CarryableTools
{
    /// <summary>
    /// Optional carry rotation offset on the same GameObject as <see cref="CarryableCarryDriver"/>,
    /// or on any of its children.
    /// </summary>
    public interface ICarryableCarryRotationModifier
    {
        /// <summary>
        /// Yaw follows cursor; pitch comes from <see cref="ApplyCarryRotationOffset"/> (no tip-down bind solve).
        /// </summary>
        bool UsesYawPitchCarry { get; }

        Quaternion ApplyCarryRotationOffset(Quaternion rotation);
    }
}
