using REHozy.CarryableTools;
using REHozy.Rendering;
using UnityEngine;

namespace REHozy.Torch
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Torch/Torch Map Outline Controller")]
    public sealed class TorchMapOutlineController : MonoBehaviour
    {
        [SerializeField] private CarryableToolCore carriedTorch;
        [SerializeField] private Color outlineColor = new(0.95f, 0.35f, 0.12f, 1f);
        [SerializeField] private float outlineWidth = 0.025f;

        public void BindToTool(CarryableToolCore core)
        {
            carriedTorch = core != null && core.ToolModeId == PlayerToolMode.Torch ? core : null;
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
            var carrying = carriedTorch != null && carriedTorch.State == CarryableToolState.Carried;
            if (!carrying)
            {
                SetAllOutlines(false);
                return;
            }

            var staticTorches = StaticTorch.ActiveInScene;
            for (var i = 0; i < staticTorches.Count; i++)
            {
                var staticTorch = staticTorches[i];
                if (staticTorch == null)
                {
                    continue;
                }

                var highlight = staticTorch.GetComponent<ObjectOutlineHighlight>();
                if (highlight == null)
                {
                    highlight = staticTorch.gameObject.AddComponent<ObjectOutlineHighlight>();
                }

                highlight.Configure(outlineColor, outlineWidth);
                highlight.SetHighlighted(!staticTorch.IsLit);
            }
        }

        private void SetAllOutlines(bool active)
        {
            var staticTorches = StaticTorch.ActiveInScene;
            for (var i = 0; i < staticTorches.Count; i++)
            {
                var staticTorch = staticTorches[i];
                if (staticTorch == null)
                {
                    continue;
                }

                var highlight = staticTorch.GetComponent<ObjectOutlineHighlight>();
                if (highlight != null)
                {
                    highlight.SetHighlighted(active);
                }
            }
        }
    }
}
