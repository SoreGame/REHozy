using REHozy.CarryableTools;
using UnityEngine;
using UnityEngine.UI;

namespace REHozy.Torch
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Torch/Torch Fuel Progress UI")]
    public sealed class TorchFuelProgressUI : MonoBehaviour
    {
        [SerializeField] private TorchToolActions torchActions;
        [SerializeField] private UnityEngine.Camera worldCamera;
        [SerializeField] private RectTransform root;
        [SerializeField] private Image fillImage;
        [SerializeField] private Canvas rootCanvas;

        [SerializeField] private Vector2 screenOffset = new(0f, 36f);
        [SerializeField] private Vector2 barSize = new(72f, 9f);
        [SerializeField] private Color backgroundColor = new(0.08f, 0.08f, 0.08f, 0.8f);
        [SerializeField] private Color igniteFillColor = new(0.85f, 0.12f, 0.1f, 0.95f);
        [SerializeField] private Color burnFillColor = new(0.95f, 0.2f, 0.15f, 0.95f);
        [SerializeField] private bool createUiIfMissing = true;

        public void BindToTool(CarryableToolCore core)
        {
            torchActions = core != null ? core.GetComponent<TorchToolActions>() : null;
        }

        private void Awake()
        {
            if (createUiIfMissing && root == null)
            {
                EnsureBarHierarchy();
            }

            if (rootCanvas == null && root != null)
            {
                rootCanvas = root.GetComponentInParent<Canvas>();
            }

            if (root != null)
            {
                root.gameObject.SetActive(false);
            }

            TryResolveTorchActions();
        }

        private void LateUpdate()
        {
            if (torchActions == null)
            {
                TryResolveTorchActions();
            }

            if (root == null || fillImage == null || torchActions == null)
            {
                return;
            }

            if (!torchActions.ShouldShowFuelBar)
            {
                HideBar();
                return;
            }

            var cam = worldCamera != null ? worldCamera : UnityEngine.Camera.main;
            if (cam == null)
            {
                HideBar();
                return;
            }

            var anchor = torchActions.BarWorldAnchor;
            var screen = cam.WorldToScreenPoint(anchor);
            if (screen.z < 0f)
            {
                HideBar();
                return;
            }

            if (!root.gameObject.activeSelf)
            {
                root.gameObject.SetActive(true);
            }

            fillImage.fillAmount = Mathf.Clamp01(torchActions.BarFill01);
            fillImage.color = torchActions.IsIgniting || torchActions.IsRefueling
                ? igniteFillColor
                : burnFillColor;
            PositionOnScreen(screen + (Vector3)screenOffset);
        }

        private void HideBar()
        {
            if (root != null && root.gameObject.activeSelf)
            {
                root.gameObject.SetActive(false);
            }
        }

        private void PositionOnScreen(Vector3 screenPoint)
        {
            var canvasRect = rootCanvas != null ? rootCanvas.transform as RectTransform : null;
            if (canvasRect == null)
            {
                root.position = screenPoint;
                return;
            }

            var camForCanvas = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : rootCanvas.worldCamera;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, screenPoint, camForCanvas, out var localPoint))
            {
                root.anchoredPosition = localPoint;
            }
        }

        private void EnsureBarHierarchy()
        {
            var canvasGo = new GameObject("TorchFuelProgressCanvas");
            canvasGo.transform.SetParent(transform, false);

            rootCanvas = canvasGo.AddComponent<Canvas>();
            rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            rootCanvas.sortingOrder = 102;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var barGo = new GameObject("TorchFuelBar", typeof(RectTransform));
            barGo.transform.SetParent(canvasGo.transform, false);
            root = barGo.GetComponent<RectTransform>();
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = barSize;

            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgGo.transform.SetParent(barGo.transform, false);
            StretchFull(bgGo.GetComponent<RectTransform>());
            var bgImage = bgGo.GetComponent<Image>();
            bgImage.sprite = CreateWhiteSprite();
            bgImage.color = backgroundColor;
            bgImage.raycastTarget = false;

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillGo.transform.SetParent(barGo.transform, false);
            StretchFull(fillGo.GetComponent<RectTransform>());
            fillImage = fillGo.GetComponent<Image>();
            fillImage.sprite = CreateWhiteSprite();
            fillImage.color = burnFillColor;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 0f;
            fillImage.raycastTarget = false;
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Sprite CreateWhiteSprite()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
        }

        private void TryResolveTorchActions()
        {
            if (torchActions != null || PlayerToolModeState.Active != PlayerToolMode.Torch)
            {
                return;
            }

            var cores = FindObjectsByType<CarryableToolCore>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var core in cores)
            {
                if (core.ToolModeId != PlayerToolMode.Torch)
                {
                    continue;
                }

                var actions = core.GetComponent<TorchToolActions>();
                if (actions != null)
                {
                    torchActions = actions;
                    return;
                }
            }
        }
    }
}
