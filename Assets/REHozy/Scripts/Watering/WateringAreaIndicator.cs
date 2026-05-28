using REHozy.CarryableTools;
using UnityEngine;

namespace REHozy.Watering
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Watering/Watering Area Indicator")]
    public sealed class WateringAreaIndicator : MonoBehaviour
    {
        [SerializeField] private CarryableToolCore tool;
        [SerializeField] private float radius = 0.75f;
        [SerializeField] private float heightAboveSurface = 0.03f;
        [SerializeField] private Color color = new(0.35f, 0.75f, 1f, 0.45f);

        private Transform _discRoot;
        private Material _discMaterial;
        private bool _visible;

        public float Radius
        {
            get => radius;
            set => radius = value;
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (_discRoot != null)
            {
                _discRoot.gameObject.SetActive(visible);
            }
        }

        private void Awake()
        {
            if (tool == null)
            {
                tool = GetComponentInParent<CarryableToolCore>();
            }

            EnsureDisc();
            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (tool == null || tool.CarryDriver == null)
            {
                SetVisible(false);
                return;
            }

            if (tool.State != CarryableToolState.Carried)
            {
                SetVisible(false);
                return;
            }

            if (!tool.CarryDriver.TryGetGroundAnchor(out var anchor))
            {
                SetVisible(false);
                return;
            }

            EnsureDisc();
            _discRoot.position = anchor + Vector3.up * heightAboveSurface;
            _discRoot.localScale = new Vector3(radius * 2f, radius * 2f, 1f);

            if (_discMaterial != null)
            {
                _discMaterial.color = color;
            }
        }

        private void EnsureDisc()
        {
            if (_discRoot != null)
            {
                return;
            }

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "WateringAreaDisc";
            Destroy(quad.GetComponent<Collider>());

            _discRoot = quad.transform;
            _discRoot.SetParent(transform, false);
            _discRoot.rotation = Quaternion.Euler(90f, 0f, 0f);

            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default");
            _discMaterial = new Material(shader) { color = color };
            if (_discMaterial.HasProperty("_Surface"))
            {
                _discMaterial.SetFloat("_Surface", 1f);
                _discMaterial.SetFloat("_Blend", 0f);
                _discMaterial.renderQueue = 3000;
            }

            renderer.sharedMaterial = _discMaterial;
            quad.SetActive(_visible);
        }

        private void OnDestroy()
        {
            if (_discMaterial != null)
            {
                Destroy(_discMaterial);
            }
        }
    }
}
