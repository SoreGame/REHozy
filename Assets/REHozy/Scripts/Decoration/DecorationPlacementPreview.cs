using UnityEngine;
using UnityEngine.Rendering;

namespace REHozy.Decoration
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Decoration/Decoration Placement Preview")]
    public sealed class DecorationPlacementPreview : MonoBehaviour
    {
        [SerializeField] private float heightAboveSurface = 0.03f;
        [SerializeField] private float boundsPadding = 1.05f;
        [SerializeField] private Color validColor = new Color(0.2f, 0.85f, 0.35f, 0.45f);
        [SerializeField] private Color invalidColor = new Color(0.9f, 0.2f, 0.15f, 0.5f);

        private Transform _previewRoot;
        private Renderer _previewRenderer;
        private Material _previewMaterial;
        private bool _isVisible;

        public void SetVisible(bool visible)
        {
            _isVisible = visible;
            if (_previewRoot != null)
            {
                _previewRoot.gameObject.SetActive(visible);
            }
        }

        public void UpdatePreview(
            Vector3 anchor,
            Vector3 surfaceNormal,
            Quaternion placementRotation,
            bool isValid,
            Bounds sourceBounds)
        {
            if (!_isVisible)
            {
                return;
            }

            EnsurePreviewObjects();

            var normal = surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal.normalized : Vector3.up;

            // World-space footprint (quad is unparented so parent scale does not shrink the shadow).
            var footprintX = Mathf.Max(sourceBounds.size.x * boundsPadding, 0.2f);
            var footprintZ = Mathf.Max(sourceBounds.size.z * boundsPadding, 0.2f);

            // Quad lies in the placement tangent plane; +90° X maps mesh normal to placement up (surface normal).
            var previewRotation = placementRotation * Quaternion.Euler(90f, 0f, 0f);

            _previewRoot.SetParent(null, true);
            _previewRoot.localScale = new Vector3(footprintX, footprintZ, 1f);
            _previewRoot.SetPositionAndRotation(
                anchor + normal * heightAboveSurface,
                previewRotation);

            _previewMaterial.color = isValid ? validColor : invalidColor;
        }

        private void EnsurePreviewObjects()
        {
            if (_previewRoot != null)
            {
                return;
            }

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "PlacementPreview";
            Destroy(quad.GetComponent<Collider>());

            _previewRoot = quad.transform;
            _previewRoot.SetParent(transform, false);
            _previewRenderer = quad.GetComponent<Renderer>();

            _previewMaterial = CreatePreviewMaterial();
            _previewRenderer.sharedMaterial = _previewMaterial;
            _previewRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _previewRenderer.receiveShadows = false;

            _previewRoot.gameObject.SetActive(_isVisible);
        }

        private static Material CreatePreviewMaterial()
        {
            var shader = Shader.Find("Sprites/Default")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Transparent")
                ?? Shader.Find("Unlit/Color");
            var material = new Material(shader);
            material.renderQueue = 3000;
            material.SetInt("_Cull", (int)CullMode.Off);

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetInt("_ZWrite", 0);
            }

            return material;
        }

        private void OnDestroy()
        {
            if (_previewMaterial != null)
            {
                Destroy(_previewMaterial);
            }
        }
    }
}
