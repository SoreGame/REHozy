using REHozy.CarryableTools;
using REHozy.Rendering;
using UnityEngine;

namespace REHozy.Watering
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Watering/Watering Map Outline Controller")]
    public sealed class WateringMapOutlineController : MonoBehaviour
    {
        [SerializeField] private CarryableToolCore carriedWateringCan;
        [SerializeField] private Color outlineColor = new(0.25f, 0.9f, 0.35f, 1f);
        [SerializeField] private float outlineWidth = 0.025f;

        public void BindToTool(CarryableToolCore core)
        {
            carriedWateringCan = core != null && core.ToolModeId == PlayerToolMode.Water ? core : null;
            RefreshOutlines();
        }

        private void LateUpdate()
        {
            RefreshOutlines();
        }

        private void OnDisable()
        {
            SetAllOutlines(false);
        }

        private void RefreshOutlines()
        {
            var carrying = carriedWateringCan != null && carriedWateringCan.State == CarryableToolState.Carried;
            if (!carrying)
            {
                SetAllOutlines(false);
                return;
            }

            var waterables = WaterableRegistry.ActiveInScene;
            for (var i = 0; i < waterables.Count; i++)
            {
                var waterable = waterables[i];
                if (waterable == null || waterable.IsWateringComplete)
                {
                    continue;
                }

                if (waterable is not Component behaviour)
                {
                    continue;
                }

                var highlight = behaviour.GetComponent<ObjectOutlineHighlight>();
                if (highlight == null)
                {
                    highlight = behaviour.gameObject.AddComponent<ObjectOutlineHighlight>();
                }

                highlight.Configure(outlineColor, outlineWidth);
                highlight.SetHighlighted(true);
            }

            for (var i = 0; i < waterables.Count; i++)
            {
                var waterable = waterables[i];
                if (waterable == null || !waterable.IsWateringComplete || waterable is not Component behaviour)
                {
                    continue;
                }

                var highlight = behaviour.GetComponent<ObjectOutlineHighlight>();
                if (highlight != null)
                {
                    highlight.SetHighlighted(false);
                }
            }
        }

        private void SetAllOutlines(bool active)
        {
            var waterables = WaterableRegistry.ActiveInScene;
            for (var i = 0; i < waterables.Count; i++)
            {
                var waterable = waterables[i];
                if (waterable is not Component behaviour)
                {
                    continue;
                }

                var highlight = behaviour.GetComponent<ObjectOutlineHighlight>();
                if (highlight != null)
                {
                    highlight.SetHighlighted(active);
                }
            }
        }
    }
}
