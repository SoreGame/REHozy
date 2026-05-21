using UnityEngine;
using UnityEngine.UI;

namespace REHozy.CarryableTools
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Carryable Tools/Carryable Aim Reticle UI")]
    public sealed class CarryableAimReticleUI : MonoBehaviour
    {
        [SerializeField] private CarryableToolCore tool;
        [SerializeField] private CarryableCarryDriver carryDriver;
        [SerializeField] private UnityEngine.Camera worldCamera;
        [SerializeField] private RectTransform reticle;
        [SerializeField] private Canvas rootCanvas;

        [SerializeField] private Vector2 reticleSize = new(24f, 24f);
        [SerializeField] private Color reticleColor = new(1f, 1f, 1f, 0.9f);
        [SerializeField] private bool createUiIfMissing = true;

        private void Awake()
        {
            if (tool == null)
            {
                tool = FindFirstObjectByType<CarryableToolCore>();
            }

            if (carryDriver == null && tool != null)
            {
                carryDriver = tool.CarryDriver;
            }

            if (createUiIfMissing && reticle == null)
            {
                EnsureReticleHierarchy();
            }

            if (rootCanvas == null && reticle != null)
            {
                rootCanvas = reticle.GetComponentInParent<Canvas>();
            }

            if (reticle != null)
            {
                reticle.gameObject.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            if (reticle == null || tool == null || carryDriver == null)
            {
                return;
            }

            if (tool.State != CarryableToolState.Carried)
            {
                if (reticle.gameObject.activeSelf)
                {
                    reticle.gameObject.SetActive(false);
                }

                return;
            }

            if (!carryDriver.TryGetGroundAnchor(out var groundPoint))
            {
                if (reticle.gameObject.activeSelf)
                {
                    reticle.gameObject.SetActive(false);
                }

                return;
            }

            var cam = worldCamera != null ? worldCamera : UnityEngine.Camera.main;
            if (cam == null)
            {
                return;
            }

            var screen = cam.WorldToScreenPoint(groundPoint);
            if (screen.z < 0f)
            {
                reticle.gameObject.SetActive(false);
                return;
            }

            if (!reticle.gameObject.activeSelf)
            {
                reticle.gameObject.SetActive(true);
            }

            var canvasRect = rootCanvas != null ? rootCanvas.transform as RectTransform : null;
            if (canvasRect == null)
            {
                reticle.position = screen;
                return;
            }

            var camForCanvas = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : rootCanvas.worldCamera;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, screen, camForCanvas, out var localPoint))
            {
                reticle.anchoredPosition = localPoint;
            }
        }

        private void EnsureReticleHierarchy()
        {
            var canvasGo = new GameObject("CarryableReticleCanvas");
            canvasGo.transform.SetParent(transform, false);

            rootCanvas = canvasGo.AddComponent<Canvas>();
            rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            rootCanvas.sortingOrder = 100;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var reticleGo = new GameObject("Reticle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            reticleGo.transform.SetParent(canvasGo.transform, false);

            reticle = reticleGo.GetComponent<RectTransform>();
            reticle.anchorMin = reticle.anchorMax = new Vector2(0.5f, 0.5f);
            reticle.pivot = new Vector2(0.5f, 0.5f);
            reticle.sizeDelta = reticleSize;

            var image = reticleGo.GetComponent<Image>();
            image.sprite = CreateDotSprite();
            image.color = reticleColor;
            image.raycastTarget = false;
        }

        private static Sprite CreateDotSprite()
        {
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var center = (size - 1) * 0.5f;
            var radius = size * 0.35f;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    var alpha = dist <= radius ? 1f : 0f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
