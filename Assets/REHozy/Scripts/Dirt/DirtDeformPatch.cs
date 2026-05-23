using System;
using UnityEngine;

namespace REHozy.Dirt
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshRenderer))]
    [ExecuteAlways]
    [DefaultExecutionOrder(-100)]
    [AddComponentMenu("REHozy/Dirt/Dirt Deform Patch")]
    public sealed class DirtDeformPatch : MonoBehaviour
    {
        private static readonly int DeformMapId = Shader.PropertyToID("_DeformMap");
        private static readonly int GlobalOffsetXZId = Shader.PropertyToID("_GlobalOffsetXZ");
        private static readonly int DeformScaleId = Shader.PropertyToID("_DeformScale");
        private static readonly int PlaneHalfExtentId = Shader.PropertyToID("_PlaneHalfExtent");
        private static readonly int EdgeFalloffUseObjectPosId = Shader.PropertyToID("_EdgeFalloffUseObjectPos");
        private static readonly int EdgeFalloffRadialId = Shader.PropertyToID("_EdgeFalloffRadial");
        private static readonly int EdgeFalloffWidthId = Shader.PropertyToID("_EdgeFalloffWidth");
        private static readonly int EdgeFalloffEnableId = Shader.PropertyToID("_EdgeFalloffEnable");

        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private int resolution = 256;
        [SerializeField] private bool hideWhenFullyEroded;
        [SerializeField] private float visibilityCutoff = 0.1f;
        [SerializeField] private bool previewInEditMode = true;

        [Header("Quest")]
        [Tooltip("Share of this patch's mass that counts for the dirt quest (0–1). Lower = quest reaches 100% sooner when visible dirt is gone.")]
        [SerializeField] [Range(0f, 1f)] private float questMassScale = 1f;

        private Texture2D _deformMap;
        private Material _materialInstance;
        private Vector2 _globalOffsetXZ;
        private float _deformScale;
        private Vector2 _planeHalfExtent;
        private byte[] _pixelBuffer;
        private bool _mapDirty;
        private int _emptyFrames;
        private bool _editorPreviewActive;
        private float _baselineMass = -1f;
        private float _edgeFalloffWidthForQuest = 0.22f;
        private bool _edgeFalloffRadialForQuest = true;
        private bool _edgeFalloffEnabledForQuest = true;

        public static event Action<DirtDeformPatch> DirtMassChanged;
        public static event Action<DirtDeformPatch> DirtPlayModeReady;

        public int Resolution => resolution;
        public bool IsPlayModeReady => Application.isPlaying && _pixelBuffer != null && _pixelBuffer.Length > 0;
        public float QuestMassScale => Mathf.Clamp01(questMassScale);
        public float BaselineMass => _baselineMass > 0f ? _baselineMass : GetQuestMass();

        public void GetWorkPlane(out Vector3 pointOnPlane, out Vector3 normal)
        {
            normal = transform.up;
            pointOnPlane = transform.position;

            if (meshRenderer != null)
            {
                pointOnPlane = meshRenderer.bounds.center;
            }

            if (!Application.isPlaying)
            {
                return;
            }

            var origin = pointOnPlane + normal * 2f;
            if (Physics.Raycast(origin, -normal, out var hit, 6f, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore)
                && hit.collider != null
                && hit.collider.GetComponentInParent<DirtDeformPatch>() == this)
            {
                pointOnPlane = hit.point;
            }
        }

        private void Reset()
        {
            meshRenderer = GetComponent<MeshRenderer>();
        }

        private void OnEnable()
        {
            if (meshRenderer == null)
            {
                meshRenderer = GetComponent<MeshRenderer>();
            }

            if (Application.isPlaying)
            {
                Initialize();
            }
            else if (previewInEditMode)
            {
                RefreshEditorPreview();
            }
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
            {
                TeardownEditorPreview();
            }
        }

        private void Awake()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (meshRenderer == null)
            {
                meshRenderer = GetComponent<MeshRenderer>();
            }

            Initialize();
        }

        private void OnDestroy()
        {
            TeardownRuntimeResources();
            TeardownEditorPreview();
        }

        private void LateUpdate()
        {
            if (!_mapDirty || _deformMap == null)
            {
                return;
            }

            _deformMap.LoadRawTextureData(_pixelBuffer);
            _deformMap.Apply(false, false);
            _mapDirty = false;
        }

        public void Initialize()
        {
            if (meshRenderer == null)
            {
                meshRenderer = GetComponent<MeshRenderer>();
            }

            if (meshRenderer == null)
            {
                return;
            }

            if (meshRenderer.sharedMaterial == null)
            {
                Debug.LogWarning(
                    $"[DirtDeformPatch] {name} has no material on MeshRenderer. Assign SnowVertexLit material first.",
                    this);
                return;
            }

            _materialInstance = meshRenderer.material;
            ComputeWorldMapping();
            CreateDeformMap();
            ApplyShaderParams();
        }

        private void RefreshEditorPreview()
        {
            if (!previewInEditMode || Application.isPlaying)
            {
                return;
            }

            TeardownEditorPreview();
            Initialize();
            _editorPreviewActive = _deformMap != null && _materialInstance != null;

#if UNITY_EDITOR
            UnityEditor.SceneView.RepaintAll();
#endif
        }

        private void TeardownEditorPreview()
        {
            if (!_editorPreviewActive && _deformMap == null)
            {
                return;
            }

            if (_deformMap != null)
            {
                DestroyObject(_deformMap);
                _deformMap = null;
            }

            _pixelBuffer = null;
            _mapDirty = false;
            _editorPreviewActive = false;

            if (meshRenderer != null && _materialInstance != null)
            {
                _materialInstance.SetTexture(DeformMapId, null);
            }
        }

        private void TeardownRuntimeResources()
        {
            if (_deformMap != null)
            {
                DestroyObject(_deformMap);
                _deformMap = null;
            }

            if (_materialInstance != null)
            {
                DestroyObject(_materialInstance);
                _materialInstance = null;
            }
        }

        private static void DestroyObject(UnityEngine.Object obj)
        {
            if (obj == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(obj);
            }
            else
            {
                DestroyImmediate(obj);
            }
        }

        private void ComputeWorldMapping()
        {
            var bounds = meshRenderer.bounds;
            var min = bounds.min;
            var max = bounds.max;
            _globalOffsetXZ = new Vector2(-min.x, -min.z);

            var sizeX = Mathf.Max(max.x - min.x, 0.01f);
            var sizeZ = Mathf.Max(max.z - min.z, 0.01f);
            var maxSize = Mathf.Max(sizeX, sizeZ);
            _deformScale = 1f / maxSize;

            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                var localBounds = meshFilter.sharedMesh.bounds;
                _planeHalfExtent = new Vector2(localBounds.extents.x, localBounds.extents.z);
            }
            else
            {
                _planeHalfExtent = new Vector2(sizeX * 0.5f, sizeZ * 0.5f);
            }
        }

        private void CreateDeformMap()
        {
            if (_deformMap != null)
            {
                DestroyObject(_deformMap);
            }

            _deformMap = new Texture2D(resolution, resolution, TextureFormat.R8, false, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = $"{name}_DeformMap"
            };

            var count = resolution * resolution;
            _pixelBuffer = new byte[count];
            for (var i = 0; i < count; i++)
            {
                _pixelBuffer[i] = 255;
            }

            _deformMap.LoadRawTextureData(_pixelBuffer);
            _deformMap.Apply(false, false);

            if (_materialInstance != null)
            {
                _materialInstance.SetTexture(DeformMapId, _deformMap);
            }

            _mapDirty = false;
            DirtPlayModeReady?.Invoke(this);
        }

        public void EnsurePlayModeInitialized()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (!IsPlayModeReady)
            {
                Initialize();
            }
        }

        private void ApplyShaderParams()
        {
            if (_materialInstance == null)
            {
                return;
            }

            _materialInstance.SetVector(GlobalOffsetXZId, new Vector4(_globalOffsetXZ.x, _globalOffsetXZ.y, 0f, 0f));
            _materialInstance.SetFloat(DeformScaleId, _deformScale);
            _materialInstance.SetVector(PlaneHalfExtentId, new Vector4(_planeHalfExtent.x, _planeHalfExtent.y, 0f, 0f));
            _materialInstance.SetFloat(EdgeFalloffUseObjectPosId, 1f);
            _materialInstance.SetFloat(EdgeFalloffRadialId, 1f);
            _materialInstance.SetFloat(EdgeFalloffWidthId, 0.22f);
        }

        public bool TryErodeAtWorld(Vector3 worldPos, float brushRadiusWorld, float strengthPerSecond)
        {
            if (!Application.isPlaying || _deformMap == null || _pixelBuffer == null
                || brushRadiusWorld <= 0f || strengthPerSecond <= 0f)
            {
                return false;
            }

            var worldXz = new Vector2(worldPos.x, worldPos.z);
            var uv = (worldXz + _globalOffsetXZ) * _deformScale;

            if (uv.x < 0f || uv.y < 0f || uv.x > 1f || uv.y > 1f)
            {
                return false;
            }

            var centerX = uv.x * (resolution - 1);
            var centerY = uv.y * (resolution - 1);
            var radiusTexels = brushRadiusWorld * _deformScale * (resolution - 1);
            var radiusSq = radiusTexels * radiusTexels;
            var erodeByte = (byte)Mathf.Clamp(Mathf.RoundToInt(strengthPerSecond * 255f * Time.deltaTime), 1, 255);

            var minX = Mathf.Max(0, Mathf.FloorToInt(centerX - radiusTexels));
            var maxX = Mathf.Min(resolution - 1, Mathf.CeilToInt(centerX + radiusTexels));
            var minY = Mathf.Max(0, Mathf.FloorToInt(centerY - radiusTexels));
            var maxY = Mathf.Min(resolution - 1, Mathf.CeilToInt(centerY + radiusTexels));

            var changed = false;

            for (var y = minY; y <= maxY; y++)
            {
                var dy = y - centerY;
                var row = y * resolution;
                for (var x = minX; x <= maxX; x++)
                {
                    var dx = x - centerX;
                    if (dx * dx + dy * dy > radiusSq)
                    {
                        continue;
                    }

                    var index = row + x;
                    var oldValue = _pixelBuffer[index];
                    if (oldValue == 0)
                    {
                        continue;
                    }

                    var newValue = (byte)Mathf.Max(0, oldValue - erodeByte);
                    if (newValue == oldValue)
                    {
                        continue;
                    }

                    _pixelBuffer[index] = newValue;
                    changed = true;
                }
            }

            if (changed)
            {
                _mapDirty = true;
            }

            if (hideWhenFullyEroded)
            {
                UpdateFullyErodedState();
            }

            if (changed)
            {
                DirtMassChanged?.Invoke(this);
            }

            return changed;
        }

        public float CaptureBaselineMass()
        {
            SyncQuestFalloffFromMaterial();
            _baselineMass = GetQuestMass();
            return _baselineMass;
        }

        public float GetCurrentMass()
        {
            if (_pixelBuffer == null || _pixelBuffer.Length == 0)
            {
                return 0f;
            }

            long sum = 0;
            for (var i = 0; i < _pixelBuffer.Length; i++)
            {
                sum += _pixelBuffer[i];
            }

            return sum;
        }

        /// <summary>
        /// Mass weighted like visible deform in SnowVertexLit (radial falloff + visibility cutoff).
        /// </summary>
        public float GetQuestMass()
        {
            if (_pixelBuffer == null || _pixelBuffer.Length == 0)
            {
                return 0f;
            }

            SyncQuestFalloffFromMaterial();
            var res = resolution;
            var sum = 0f;

            for (var y = 0; y < res; y++)
            {
                var row = y * res;
                for (var x = 0; x < res; x++)
                {
                    var falloff = GetFalloffWeightForTexel(x, y, res);
                    var deform01 = _pixelBuffer[row + x] / 255f;
                    sum += deform01 * falloff;
                }
            }

            return sum;
        }

        public float GetRemainingRatio01()
        {
            if (_baselineMass <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(GetQuestMass() / _baselineMass);
        }

        private void SyncQuestFalloffFromMaterial()
        {
            if (_materialInstance == null)
            {
                return;
            }

            if (_materialInstance.HasProperty(EdgeFalloffWidthId))
            {
                _edgeFalloffWidthForQuest = _materialInstance.GetFloat(EdgeFalloffWidthId);
            }

            if (_materialInstance.HasProperty(EdgeFalloffRadialId))
            {
                _edgeFalloffRadialForQuest = _materialInstance.GetFloat(EdgeFalloffRadialId) > 0.5f;
            }

            if (_materialInstance.HasProperty(EdgeFalloffEnableId))
            {
                _edgeFalloffEnabledForQuest = _materialInstance.GetFloat(EdgeFalloffEnableId) > 0.5f;
            }
        }

        private float GetFalloffWeightForTexel(int x, int y, int res)
        {
            if (!_edgeFalloffEnabledForQuest)
            {
                return 1f;
            }

            var denom = Mathf.Max(res - 1, 1);
            var fracPos = new Vector2((float)x / denom, (float)y / denom);
            float d;

            if (_edgeFalloffRadialForQuest)
            {
                var h = new Vector2(
                    Mathf.Max(_planeHalfExtent.x, 0.001f),
                    Mathf.Max(_planeHalfExtent.y, 0.001f));
                var centered = fracPos * 2f - Vector2.one;
                var ellip = new Vector2(centered.x, centered.y * (h.x / h.y));
                d = 1f - Mathf.Clamp01(ellip.magnitude);
            }
            else
            {
                d = Mathf.Min(
                    Mathf.Min(fracPos.x, 1f - fracPos.x),
                    Mathf.Min(fracPos.y, 1f - fracPos.y)) * 2f;
            }

            var w = Mathf.Clamp(_edgeFalloffWidthForQuest, 0.02f, 0.5f);
            return Mathf.SmoothStep(0f, w, d);
        }

        public void ClearAllDirt()
        {
            if (_pixelBuffer == null)
            {
                if (meshRenderer != null)
                {
                    meshRenderer.enabled = false;
                }

                return;
            }

            for (var i = 0; i < _pixelBuffer.Length; i++)
            {
                _pixelBuffer[i] = 0;
            }

            if (_deformMap != null)
            {
                _deformMap.LoadRawTextureData(_pixelBuffer);
                _deformMap.Apply(false, false);
            }

            _mapDirty = false;

            if (meshRenderer != null)
            {
                meshRenderer.enabled = false;
            }

            DirtMassChanged?.Invoke(this);
        }

        private void UpdateFullyErodedState()
        {
            var maxValue = 0;
            for (var i = 0; i < _pixelBuffer.Length; i++)
            {
                if (_pixelBuffer[i] > maxValue)
                {
                    maxValue = _pixelBuffer[i];
                }
            }

            var cutoffByte = (byte)Mathf.Clamp(Mathf.RoundToInt(visibilityCutoff * 255f), 0, 255);
            if (maxValue > cutoffByte)
            {
                _emptyFrames = 0;
                return;
            }

            _emptyFrames++;
            if (_emptyFrames >= 3)
            {
                meshRenderer.enabled = false;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (meshRenderer == null)
            {
                meshRenderer = GetComponent<MeshRenderer>();
            }

            if (Application.isPlaying)
            {
                if (_materialInstance == null || _deformMap == null)
                {
                    Initialize();
                    return;
                }

                ComputeWorldMapping();
                ApplyShaderParams();
                return;
            }

            if (!previewInEditMode)
            {
                TeardownEditorPreview();
                return;
            }

            UnityEditor.EditorApplication.delayCall += DelayedEditorPreview;
        }

        private void DelayedEditorPreview()
        {
            UnityEditor.EditorApplication.delayCall -= DelayedEditorPreview;
            if (this == null || !previewInEditMode || Application.isPlaying)
            {
                return;
            }

            RefreshEditorPreview();
        }
#endif
    }
}
