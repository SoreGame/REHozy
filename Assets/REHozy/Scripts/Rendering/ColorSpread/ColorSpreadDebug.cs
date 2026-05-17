using UnityEngine;
using UnityEngine.InputSystem;

namespace REHozy.Rendering
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Rendering/Color Spread Debug")]
    public sealed class ColorSpreadDebug : MonoBehaviour
    {
        [SerializeField] ColorSpreadController controller;
        [SerializeField] Transform waveOrigin;
        [SerializeField] bool enableHotkeys = true;

        void Reset()
        {
            controller = GetComponent<ColorSpreadController>();
            waveOrigin = transform;
        }

        void Update()
        {
            if (!enableHotkeys || !Application.isPlaying)
                return;

            if (controller == null)
                controller = ColorSpreadController.Instance;

            if (controller == null)
                return;

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.digit0Key.wasPressedThisFrame)
                controller.SetStep(ColorSpreadStep.Grayscale, GetOrigin());
            if (keyboard.digit1Key.wasPressedThisFrame)
                controller.SetStep(ColorSpreadStep.RedTones, GetOrigin());
            if (keyboard.digit2Key.wasPressedThisFrame)
                controller.SetStep(ColorSpreadStep.BlueTones, GetOrigin());
            if (keyboard.digit3Key.wasPressedThisFrame)
                controller.SetStep(ColorSpreadStep.GreenTones, GetOrigin());
            if (keyboard.digit4Key.wasPressedThisFrame)
                controller.SetStep(ColorSpreadStep.FullColor, GetOrigin());
        }

        Vector3 GetOrigin() => waveOrigin != null ? waveOrigin.position : transform.position;

        void OnDrawGizmosSelected()
        {
            if (controller == null)
                controller = GetComponent<ColorSpreadController>();

            if (controller == null || controller.Settings == null)
                return;

            var origin = GetOrigin();
            var radius = Application.isPlaying ? controller.GetCurrentEffectRadius() : controller.Settings.maxRadius;
            Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.35f);
            Gizmos.DrawWireSphere(origin, radius);
        }
    }
}
