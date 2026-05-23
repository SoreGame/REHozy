using UnityEngine;

namespace REHozy.CarryableTools
{
    /// <summary>
    /// Applies the scene's active tool mode when play starts (static <see cref="PlayerToolModeState"/> resets on domain reload).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Carryable Tools/Carryable Tool Mode Bootstrap")]
    public sealed class CarryableToolModeBootstrap : MonoBehaviour
    {
        [SerializeField] private PlayerToolMode activeModeOnPlay = PlayerToolMode.Harpoon;

        private void Awake()
        {
            PlayerToolModeState.Active = activeModeOnPlay;
        }
    }
}
