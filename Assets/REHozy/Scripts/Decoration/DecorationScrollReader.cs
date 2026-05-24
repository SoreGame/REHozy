using UnityEngine;
using UnityEngine.InputSystem;

namespace REHozy.Decoration
{
    internal static class DecorationScrollReader
    {
        private const float WindowsNotchReference = 120f;

        /// <summary>
        /// Returns scroll in "notches" (1.0 = one wheel step). Handles both ~120 and ~1 raw device scales.
        /// </summary>
        public static float ReadScrollNotches()
        {
            var rawY = ReadRawScrollY();
            if (Mathf.Abs(rawY) < 0.01f)
            {
                return 0f;
            }

            return Mathf.Abs(rawY) >= 10f ? rawY / WindowsNotchReference : Mathf.Sign(rawY);
        }

        private static float ReadRawScrollY()
        {
            var mouse = Mouse.current;
            if (mouse == null)
            {
                return 0f;
            }

            var scroll = mouse.scroll.ReadValue();
            if (Mathf.Abs(scroll.y) > 0.01f)
            {
                return scroll.y;
            }

            return mouse.scroll.y.ReadValue();
        }
    }
}
