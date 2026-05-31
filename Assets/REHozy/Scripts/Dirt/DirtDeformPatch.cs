using System;
using UnityEngine;
using UnityEngine.Serialization;

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
        private static readonly int SnowGroundOffsetId = Shader.PropertyToID("_SnowGroundOffset");

        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private int resolution = 256;
        [SerializeField] private bool hideWhenFullyEroded;
        [SerializeField] private float visibilityCutoff = 0.1f;
        [SerializeField] private bool previewInEditMode = true;

        [Header("Ground contact")]
        [Tooltip("Raycast down to align deform=0 with terrain below the patch mesh plane.")]
        [SerializeField] private bool autoGroundOffset = true;
        [SerializeField] private LayerMask groundRaycastMask = ~0;
        [SerializeField] private float groundRaycastDistance = 8f;
        [Tooltip("Used when Auto Ground Offset is off, or when no ground is hit.")]
        [SerializeField] private float groundOffsetManual;

        [Header("Quest")]
        [Tooltip("Quest points when this patch is fully cleared (summed toward Quest Goal, e.g. 100).")]
        [FormerlySerializedAs("questMassScale")]
        [SerializeField] [Min(0.01f)] private float questWeight = 1f;

        [Header("Quest complete")]
        [SerializeField] private float questCompleteHideDuration = 0.35f;

        private Texture2D _deformMap;
        private MaterialPropertyBlock _propertyBlock;
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
        private float[] _falloffWeights;
        private float _cachedQuestMass;

        public static event Action<DirtDeformPatch> DirtMassChanged;
        public static event Action<DirtDeformPatch> DirtPlayModeReady;

        public int Resolution => resolution;
        public bool IsPlayModeReady => Application.isPlaying && _pixelBuffer != null && _pixelBuffer.Length > 0;
        public float QuestWeight => Mathf.Max(0.01f, questWeight);
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

            _propertyBlock ??= new MaterialPropertyBlock();
            ComputeWorldMapping();
            ComputeGroundContactOffset();
            CreateDeformMap();
            RebuildQuestMassCache();
        }

        private void RefreshEditorPreview()
        {
            if (!previewInEditMode || Application.isPlaying)
            {
                return;
            }

            TeardownEditorPreview();
            Initialize();
            _editorPreviewActive = _deformMap != null;

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
            _falloffWeights = null;
            _cachedQuestMass = 0f;
            _mapDirty = false;
            _editorPreviewActive = false;

            if (meshRenderer != null)
            {
                meshRenderer.SetPropertyBlock(null);
            }
        }

        private void TeardownRuntimeResources()
        {
            if (_deformMap != null)
            {
                DestroyObject(_deformMap);
                _deformMap = null;
            }

            if (meshRenderer != null)
            {
                meshRenderer.SetPropertyBlock(null);
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

            ApplyShaderParams();

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
            if (meshRenderer == null || meshRenderer.sharedMaterial == null)
            {
                return;
            }

            _propertyBlock ??= new MaterialPropertyBlock();
            _propertyBlock.SetVector(GlobalOffsetXZId, new Vector4(_globalOffsetXZ.x, _globalOffsetXZ.y, 0f, 0f));
            _propertyBlock.SetFloat(DeformScaleId, _deformScale);
            _propertyBlock.SetVector(PlaneHalfExtentId, new Vector4(_planeHalfExtent.x, _planeHalfExtent.y, 0f, 0f));
            _propertyBlock.SetFloat(EdgeFalloffUseObjectPosId, 1f);
            _propertyBlock.SetFloat(EdgeFalloffRadialId, 1f);
            _propertyBlock.SetFloat(EdgeFalloffWidthId, 0.22f);
            _propertyBlock.SetFloat(SnowGroundOffsetId, _groundContactOffset);

            if (_deformMap != null)
            {
                _propertyBlock.SetTexture(DeformMapId, _deformMap);
            }

            meshRenderer.SetPropertyBlock(_propertyBlock);
        }

        private float _groundContactOffset;

        private void ComputeGroundContactOffset()
        {
            if (!autoGroundOffset)
            {
                _groundContactOffset = Mathf.Max(0f, groundOffsetManual);
                return;
            }

            if (meshRenderer == null)
            {
                _groundContactOffset = Mathf.Max(0f, groundOffsetManual);
                return;
            }

            GetWorkPlane(out var pointOnPlane, out var planeNormal);
            var origin = pointOnPlane + planeNormal * 0.05f;
            var hits = Physics.RaycastAll(
                origin,
                -planeNormal,
                groundRaycastDistance,
                groundRaycastMask,
                QueryTriggerInteraction.Ignore);

            var bestDistance = float.MaxValue;
            var closestOffset = 0f;
            var found = false;

            for (var i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.collider == null || IsSelfOrChild(hit.collider.transform))
                {
                    continue;
                }

                var otherPatch = hit.collider.GetComponentInParent<DirtDeformPatch>();
                if (otherPatch != null && otherPatch != this)
                {
                    continue;
                }

                if (hit.distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = hit.distance;
                closestOffset = Mathf.Max(0f, Vector3.Dot(pointOnPlane - hit.point, planeNormal));
                found = true;
            }

            _groundContactOffset = found ? closestOffset : Mathf.Max(0f, groundOffsetManual);
        }

        private bool IsSelfOrChild(Transform other)
        {
            return other == transform || other.IsChildOf(transform);
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
                    ApplyQuestMassDelta(index, oldValue, newValue);
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

            if (_falloffWeights == null || _falloffWeights.Length != _pixelBuffer.Length)
            {
                RebuildQuestMassCache();
            }

            return _cachedQuestMass;
        }

        public float GetRemainingRatio01()
        {
            if (_baselineMass <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(GetQuestMass() / _baselineMass);
        }

        /// <summary>
        /// True when the patch mesh is still visible and has dirt above the given remaining ratio.
        /// </summary>
        public bool HasRemainingDirt(float remainingRatioThreshold = 0.05f)
        {
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            {
                return false;
            }

            if (meshRenderer != null && !meshRenderer.enabled)
            {
                return false;
            }

            if (transform.localScale.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            if (!Application.isPlaying)
            {
                return true;
            }

            if (!IsPlayModeReady)
            {
                return true;
            }

            if (_baselineMass > 0f)
            {
                return GetRemainingRatio01() > remainingRatioThreshold;
            }

            return GetQuestMass() > 0f;
        }

        private void SyncQuestFalloffFromMaterial()
        {
            if (meshRenderer == null)
            {
                return;
            }

            var mat = meshRenderer.sharedMaterial;
            if (mat == null)
            {
                return;
            }

            if (mat.HasProperty(EdgeFalloffWidthId))
            {
                _edgeFalloffWidthForQuest = mat.GetFloat(EdgeFalloffWidthId);
            }

            if (mat.HasProperty(EdgeFalloffRadialId))
            {
                _edgeFalloffRadialForQuest = mat.GetFloat(EdgeFalloffRadialId) > 0.5f;
            }

            if (mat.HasProperty(EdgeFalloffEnableId))
            {
                _edgeFalloffEnabledForQuest = mat.GetFloat(EdgeFalloffEnableId) > 0.5f;
            }
        }

        private void RebuildQuestMassCache()
        {
            if (_pixelBuffer == null || _pixelBuffer.Length == 0)
            {
                _cachedQuestMass = 0f;
                return;
            }

            BuildFalloffWeights();
            RecomputeQuestMassFromBuffer();
        }

        private void BuildFalloffWeights()
        {
            if (_pixelBuffer == null)
            {
                return;
            }

            SyncQuestFalloffFromMaterial();
            var count = _pixelBuffer.Length;
            if (_falloffWeights == null || _falloffWeights.Length != count)
            {
                _falloffWeights = new float[count];
            }

            var res = resolution;
            for (var y = 0; y < res; y++)
            {
                var row = y * res;
                for (var x = 0; x < res; x++)
                {
                    _falloffWeights[row + x] = GetFalloffWeightForTexel(x, y, res);
                }
            }
        }

        private void RecomputeQuestMassFromBuffer()
        {
            if (_pixelBuffer == null || _falloffWeights == null)
            {
                _cachedQuestMass = 0f;
                return;
            }

            var sum = 0f;
            for (var i = 0; i < _pixelBuffer.Length; i++)
            {
                sum += _pixelBuffer[i] * _falloffWeights[i];
            }

            _cachedQuestMass = sum / 255f;
        }

        private void ApplyQuestMassDelta(int index, byte oldValue, byte newValue)
        {
            if (_falloffWeights == null || index < 0 || index >= _falloffWeights.Length)
            {
                return;
            }

            _cachedQuestMass += (newValue - oldValue) * _falloffWeights[index] / 255f;
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

        public void PlayQuestCompleteHide(float? durationOverride = null)
        {
            if (!Application.isPlaying || !isActiveAndEnabled)
            {
                return;
            }

            var transition = GetComponent<QuestWorldScaleTransition>();
            if (transition == null)
            {
                transition = gameObject.AddComponent<QuestWorldScaleTransition>();
            }

            transition.Duration = durationOverride ?? questCompleteHideDuration;
            transition.PlayHide();
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

            _cachedQuestMass = 0f;

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
                if (_propertyBlock == null || _deformMap == null)
                {
                    Initialize();
                    return;
                }

                ComputeWorldMapping();
                ComputeGroundContactOffset();
                ApplyShaderParams();
                RebuildQuestMassCache();
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
