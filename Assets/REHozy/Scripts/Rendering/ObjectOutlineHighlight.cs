using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace REHozy.Rendering
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Rendering/Object Outline Highlight")]
    public sealed class ObjectOutlineHighlight : MonoBehaviour
    {
        private const string OutlineMaterialResourceName = "ObjectOutline";

        private static Material _sharedOutlineMaterial;
        private static Material _outlineMaterialSource;

        [SerializeField] private Color outlineColor = new(0.95f, 0.35f, 0.12f, 1f);
        [SerializeField] private float outlineWidth = 0.025f;
        [SerializeField] private bool includeInactiveChildren;

        private readonly List<Renderer> _outlineRenderers = new();
        private bool _built;
        private bool _highlighted;

        public bool IsHighlighted => _highlighted;

        public void Configure(Color color, float width)
        {
            outlineColor = color;
            outlineWidth = width;
            ApplyMaterialSettings();
        }

        public void SetHighlighted(bool highlighted)
        {
            if (_highlighted == highlighted)
            {
                return;
            }

            EnsureBuilt();
            _highlighted = highlighted;

            for (var i = 0; i < _outlineRenderers.Count; i++)
            {
                var renderer = _outlineRenderers[i];
                if (renderer != null)
                {
                    renderer.enabled = highlighted;
                }
            }
        }

        private void Awake()
        {
            EnsureBuilt();
            SetHighlighted(false);
        }

        private void OnDestroy()
        {
            for (var i = 0; i < _outlineRenderers.Count; i++)
            {
                var renderer = _outlineRenderers[i];
                if (renderer != null)
                {
                    Destroy(renderer.gameObject);
                }
            }

            _outlineRenderers.Clear();
        }

        private void EnsureBuilt()
        {
            if (_built)
            {
                return;
            }

            _built = true;
            ApplyMaterialSettings();

            var meshRenderers = GetComponentsInChildren<MeshRenderer>(includeInactiveChildren);
            for (var i = 0; i < meshRenderers.Length; i++)
            {
                var source = meshRenderers[i];
                if (source == null || source.gameObject.name == "OutlineShell")
                {
                    continue;
                }

                var meshFilter = source.GetComponent<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    continue;
                }

                var shellGo = new GameObject("OutlineShell");
                shellGo.transform.SetParent(source.transform, false);
                shellGo.layer = source.gameObject.layer;

                var shellFilter = shellGo.AddComponent<MeshFilter>();
                shellFilter.sharedMesh = meshFilter.sharedMesh;

                var shellRenderer = shellGo.AddComponent<MeshRenderer>();
                shellRenderer.sharedMaterial = GetOutlineMaterial();
                shellRenderer.shadowCastingMode = ShadowCastingMode.Off;
                shellRenderer.receiveShadows = false;
                shellRenderer.lightProbeUsage = LightProbeUsage.Off;
                shellRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                shellRenderer.enabled = false;
                _outlineRenderers.Add(shellRenderer);
            }
        }

        private void ApplyMaterialSettings()
        {
            var material = GetOutlineMaterial();
            if (material == null)
            {
                return;
            }

            material.SetColor("_OutlineColor", outlineColor);
            material.SetFloat("_OutlineWidth", outlineWidth);
        }

        private Material GetOutlineMaterial()
        {
            if (_sharedOutlineMaterial != null)
            {
                return _sharedOutlineMaterial;
            }

            _outlineMaterialSource ??= Resources.Load<Material>(OutlineMaterialResourceName);
            if (_outlineMaterialSource != null)
            {
                _sharedOutlineMaterial = new Material(_outlineMaterialSource);
                return _sharedOutlineMaterial;
            }

            var shader = Shader.Find("REHozy/ObjectOutline");
            if (shader == null)
            {
                Debug.LogWarning(
                    "[ObjectOutlineHighlight] Outline material not found. Add Assets/REHozy/Resources/ObjectOutline.mat or include REHozy/ObjectOutline in Graphics settings.",
                    this);
                return null;
            }

            _sharedOutlineMaterial = new Material(shader);
            return _sharedOutlineMaterial;
        }
    }
}
