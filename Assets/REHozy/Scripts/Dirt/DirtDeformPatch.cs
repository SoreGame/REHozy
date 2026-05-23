using UnityEngine;

namespace REHozy.Dirt
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshRenderer))]
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

        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private int resolution = 256;
        [SerializeField] private bool hideWhenFullyEroded;
        [SerializeField] private float visibilityCutoff = 0.1f;

        private Texture2D _deformMap;
        private Material _materialInstance;
        private Vector2 _globalOffsetXZ;
        private float _deformScale;
        private Vector2 _planeHalfExtent;
        private byte[] _pixelBuffer;
        private bool _mapDirty;
        private int _emptyFrames;

        public int Resolution => resolution;

        private void Reset()
        {
            meshRenderer = GetComponent<MeshRenderer>();
        }

        private void Awake()
        {
            if (meshRenderer == null)
            {
                meshRenderer = GetComponent<MeshRenderer>();
            }

            Initialize();
        }

        private void OnDestroy()
        {
            if (_deformMap != null)
            {
                Destroy(_deformMap);
            }

            if (_materialInstance != null)
            {
                Destroy(_materialInstance);
            }
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
                Destroy(_deformMap);
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
            if (_deformMap == null || _pixelBuffer == null || brushRadiusWorld <= 0f || strengthPerSecond <= 0f)
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
            var maxValue = 0;

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
                        if (newValue > maxValue)
                        {
                            maxValue = newValue;
                        }

                        continue;
                    }

                    _pixelBuffer[index] = newValue;
                    changed = true;
                    if (newValue > maxValue)
                    {
                        maxValue = newValue;
                    }
                }
            }

            if (changed)
            {
                _mapDirty = true;
            }

            if (hideWhenFullyEroded)
            {
                UpdateFullyErodedState(maxValue);
            }

            return changed;
        }

        private void UpdateFullyErodedState(int maxValue)
        {
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
            if (!Application.isPlaying)
            {
                return;
            }

            if (_materialInstance == null || _deformMap == null)
            {
                Initialize();
                return;
            }

            if (meshRenderer == null)
            {
                return;
            }

            ComputeWorldMapping();
            ApplyShaderParams();
        }
#endif
    }
}
