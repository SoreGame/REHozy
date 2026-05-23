using UnityEngine;
using UnityEngine.UI;

namespace REHozy.CarryableTools
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Carryable Tools/Carryable Return Hold UI")]
    public sealed class CarryableReturnHoldUI : MonoBehaviour
    {
        [SerializeField] private CarryableToolCore tool;
        [SerializeField] private CarryableToolInputHandler inputHandler;
        [SerializeField] private UnityEngine.Camera worldCamera;
        [SerializeField] private RectTransform root;
        [SerializeField] private Image fillImage;
        [SerializeField] private Canvas rootCanvas;

        [SerializeField] private float showDelay = 0.1f;
        [SerializeField] private Vector2 screenOffset = new(0f, 48f);
        [SerializeField] private Vector2 barSize = new(80f, 10f);
        [SerializeField] private Color backgroundColor = new(0.1f, 0.1f, 0.1f, 0.75f);
        [SerializeField] private Color fillColor = new(0.95f, 0.85f, 0.2f, 0.95f);
        [SerializeField] private Color blockedFillBright = new(0.95f, 0.2f, 0.15f, 0.95f);
        [SerializeField] private Color blockedFillDim = new(0.45f, 0.08f, 0.06f, 0.55f);
        [SerializeField] private float blockedBlinkSpeed = 5f;
        [SerializeField] private bool createUiIfMissing = true;

        public void BindToTool(CarryableToolCore core)
        {
            tool = core;
        }

        private void Awake()
        {
            if (tool == null)
            {
                tool = FindFirstObjectByType<CarryableToolCore>();
            }

            if (inputHandler == null)
            {
                inputHandler = FindFirstObjectByType<CarryableToolInputHandler>();
            }

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
        }

        private void LateUpdate()
        {
            if (root == null || fillImage == null || tool == null || inputHandler == null)
            {
                return;
            }

            if (!inputHandler.IsReturnHoldInProgress
                || tool.State != CarryableToolState.Carried
                || !tool.IsInHomeZone())
            {
                HideBar();
                return;
            }

            var holdDuration = Mathf.Max(tool.DropHoldDuration, 0.01f);
            var elapsed = inputHandler.ReturnHoldProgress01 * holdDuration;
            if (elapsed < showDelay)
            {
                HideBar();
                return;
            }

            var cam = ResolveCamera();
            if (cam == null)
            {
                HideBar();
                return;
            }

            var screen = cam.WorldToScreenPoint(tool.transform.position);
            if (screen.z < 0f)
            {
                HideBar();
                return;
            }

            if (!root.gameObject.activeSelf)
            {
                root.gameObject.SetActive(true);
            }

            if (tool.CanReturnHome())
            {
                var fillWindow = Mathf.Max(holdDuration - showDelay, 0.01f);
                fillImage.fillAmount = Mathf.Clamp01((elapsed - showDelay) / fillWindow);
                fillImage.color = fillColor;
            }
            else
            {
                fillImage.fillAmount = 1f;
                var blink = (Mathf.Sin(Time.unscaledTime * blockedBlinkSpeed) + 1f) * 0.5f;
                fillImage.color = Color.Lerp(blockedFillDim, blockedFillBright, blink);
            }

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

        private UnityEngine.Camera ResolveCamera()
        {
            return worldCamera != null ? worldCamera : UnityEngine.Camera.main;
        }

        private void EnsureBarHierarchy()
        {
            var canvasGo = new GameObject("CarryableReturnHoldCanvas");
            canvasGo.transform.SetParent(transform, false);

            rootCanvas = canvasGo.AddComponent<Canvas>();
            rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            rootCanvas.sortingOrder = 101;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var barGo = new GameObject("ReturnHoldBar", typeof(RectTransform));
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
            fillImage.color = fillColor;
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
    }
}
