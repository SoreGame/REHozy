using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
using static UnityEngine.Rendering.RenderGraphModule.Util.RenderGraphUtils;

namespace REHozy.Rendering
{
    public sealed class ColorSpreadRendererFeature : ScriptableRendererFeature
    {
        public static bool LastPassEnqueued { get; private set; }
        public static int LastStepApplied { get; private set; }
        public static string LastSkipReason { get; private set; } = "Not yet rendered";

        [SerializeField] Shader shader;
        [SerializeField] RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;

        ColorSpreadRenderPass _pass;
        Material _material;
        Texture2D _fallbackNoise;

        public override void Create()
        {
            _pass = new ColorSpreadRenderPass();
            if (shader != null)
                _material = CoreUtils.CreateEngineMaterial(shader);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            if (_fallbackNoise != null)
                DestroyImmediate(_fallbackNoise);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            LastPassEnqueued = false;
            LastSkipReason = "Unknown";

            if (_material == null || shader == null)
            {
                LastSkipReason = "Material or shader missing on ColorSpread renderer feature";
                return;
            }

            var cameraData = renderingData.cameraData;
            var camera = cameraData.camera;
            if (camera == null)
            {
                LastSkipReason = "Camera is null";
                return;
            }

            if (camera.cameraType == CameraType.Preview || camera.cameraType == CameraType.Reflection)
            {
                LastSkipReason = "Camera type skipped";
                return;
            }

            if (!cameraData.postProcessEnabled)
            {
                LastSkipReason = "Post Processing disabled on camera";
                return;
            }

            var controller = ColorSpreadController.Instance
                ?? Object.FindFirstObjectByType<ColorSpreadController>(FindObjectsInactive.Include);

            ColorSpreadShaderParams shaderParams;
            if (controller != null)
            {
                if (!controller.EffectEnabled || !controller.RuntimeData.effectEnabled)
                {
                    LastSkipReason = "Effect disabled on controller";
                    return;
                }

                EnsureFallbackNoise();
                shaderParams = ColorSpreadShaderParams.FromRuntimeData(controller.RuntimeData, _fallbackNoise, camera);
            }
            else
            {
                var volume = VolumeManager.instance.stack.GetComponent<ColorSpreadVolume>();
                if (volume == null || !volume.IsActive())
                {
                    LastSkipReason = "No controller and volume inactive";
                    return;
                }

                EnsureFallbackNoise();
                shaderParams = ColorSpreadShaderParams.FromVolume(volume, _fallbackNoise, camera);
            }

            LastPassEnqueued = true;
            LastStepApplied = shaderParams.step;
            LastSkipReason = "OK";

            _pass.renderPassEvent = injectionPoint;
            _pass.Setup(_material, shaderParams);
            _pass.ConfigureInput(ScriptableRenderPassInput.Depth);
            _pass.requiresIntermediateTexture = true;
            renderer.EnqueuePass(_pass);
        }

        void EnsureFallbackNoise()
        {
            if (_fallbackNoise != null)
                return;

            _fallbackNoise = new Texture2D(4, 4, TextureFormat.R8, false)
            {
                name = "ColorSpreadFallbackNoise",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color[16];
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = new Color(0.5f, 0.5f, 0.5f, 1f);
            _fallbackNoise.SetPixels(pixels);
            _fallbackNoise.Apply();
        }

        readonly struct ColorSpreadShaderParams
        {
            public readonly int step;
            public readonly int previousMask;
            public readonly int waveAddMask;
            public readonly Vector3 center;
            public readonly float startTime;
            public readonly float growthSpeed;
            public readonly float maxRadius;
            public readonly float edgeSoftness;
            public readonly float noiseScale;
            public readonly float noiseStrength;
            public readonly Texture noiseTexture;
            public readonly Vector4 redHueRangeA;
            public readonly Vector4 redHueRangeB;
            public readonly Vector4 blueHueRangeA;
            public readonly Vector4 blueHueRangeB;
            public readonly Vector4 greenHueRangeA;
            public readonly Vector4 greenHueRangeB;
            public readonly float waveEdgeIntensity;
            public readonly float waveEdgeWidth;
            public readonly Color waveEdgeColor;
            public readonly Matrix4x4 inverseViewProj;

            public static ColorSpreadShaderParams FromRuntimeData(
                ColorSpreadRuntimeData data,
                Texture2D fallbackNoise,
                Camera camera)
            {
                return new ColorSpreadShaderParams(
                    data.step,
                    data.previousMask,
                    data.waveAddMask,
                    data.center,
                    data.startTime,
                    data.growthSpeed,
                    data.maxRadius,
                    data.edgeSoftness,
                    data.noiseScale,
                    data.noiseStrength,
                    data.noiseTexture != null ? data.noiseTexture : fallbackNoise,
                    data.redHueRangeA,
                    data.redHueRangeB,
                    data.blueHueRangeA,
                    data.blueHueRangeB,
                    data.greenHueRangeA,
                    data.greenHueRangeB,
                    data.waveEdgeIntensity,
                    data.waveEdgeWidth,
                    data.waveEdgeColor,
                    BuildInverseViewProj(camera));
            }

            public static ColorSpreadShaderParams FromVolume(
                ColorSpreadVolume volume,
                Texture2D fallbackNoise,
                Camera camera)
            {
                var addMask = volume.waveAddMask.overrideState
                    ? volume.waveAddMask.value
                    : ColorSpreadPaletteMask.FromStep((ColorSpreadStep)volume.step.value);

                var unlocked = volume.unlockedMask.overrideState
                    ? volume.unlockedMask.value
                    : addMask;

                var prevMask = volume.previousMask.overrideState
                    ? volume.previousMask.value
                    : unlocked & ~addMask;

                return new ColorSpreadShaderParams(
                    volume.step.value,
                    prevMask,
                    addMask,
                    volume.center.value,
                    volume.startTime.value,
                    volume.growthSpeed.value,
                    volume.maxRadius.value,
                    volume.edgeSoftness.value,
                    volume.noiseScale.value,
                    volume.noiseStrength.value,
                    volume.noiseTexture.value != null ? volume.noiseTexture.value : fallbackNoise,
                    volume.redHueRangeA.value,
                    volume.redHueRangeB.value,
                    volume.blueHueRangeA.value,
                    volume.blueHueRangeB.value,
                    volume.greenHueRangeA.value,
                    volume.greenHueRangeB.value,
                    volume.waveEdgeIntensity.value,
                    volume.waveEdgeWidth.value,
                    volume.waveEdgeColor.value,
                    BuildInverseViewProj(camera));
            }

            ColorSpreadShaderParams(
                int step,
                int previousMask,
                int waveAddMask,
                Vector3 center,
                float startTime,
                float growthSpeed,
                float maxRadius,
                float edgeSoftness,
                float noiseScale,
                float noiseStrength,
                Texture noiseTexture,
                Vector4 redHueRangeA,
                Vector4 redHueRangeB,
                Vector4 blueHueRangeA,
                Vector4 blueHueRangeB,
                Vector4 greenHueRangeA,
                Vector4 greenHueRangeB,
                float waveEdgeIntensity,
                float waveEdgeWidth,
                Color waveEdgeColor,
                Matrix4x4 inverseViewProj)
            {
                this.step = step;
                this.previousMask = previousMask;
                this.waveAddMask = waveAddMask;
                this.center = center;
                this.startTime = startTime;
                this.growthSpeed = growthSpeed;
                this.maxRadius = maxRadius;
                this.edgeSoftness = edgeSoftness;
                this.noiseScale = noiseScale;
                this.noiseStrength = noiseStrength;
                this.noiseTexture = noiseTexture;
                this.redHueRangeA = redHueRangeA;
                this.redHueRangeB = redHueRangeB;
                this.blueHueRangeA = blueHueRangeA;
                this.blueHueRangeB = blueHueRangeB;
                this.greenHueRangeA = greenHueRangeA;
                this.greenHueRangeB = greenHueRangeB;
                this.waveEdgeIntensity = waveEdgeIntensity;
                this.waveEdgeWidth = waveEdgeWidth;
                this.waveEdgeColor = waveEdgeColor;
                this.inverseViewProj = inverseViewProj;
            }

            static Matrix4x4 BuildInverseViewProj(Camera camera)
            {
                if (camera == null)
                    return Matrix4x4.identity;

                var gpuProjection = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);
                var viewProjection = gpuProjection * camera.worldToCameraMatrix;
                return viewProjection.inverse;
            }

            public void ApplyToMaterial(Material material)
            {
                material.SetTexture("_NoiseTex", noiseTexture);
                material.SetVector("_Center", center);
                material.SetFloat("_StartTime", startTime);
                material.SetFloat("_GrowthSpeed", growthSpeed);
                material.SetFloat("_MaxRadius", maxRadius);
                material.SetFloat("_EdgeSoftness", edgeSoftness);
                material.SetFloat("_NoiseScale", noiseScale);
                material.SetFloat("_NoiseStrength", noiseStrength);
                material.SetFloat("_Step", step);
                material.SetInt("_PreviousMask", previousMask);
                material.SetInt("_WaveAddMask", waveAddMask);
                material.SetVector("_RedHueRangeA", redHueRangeA);
                material.SetVector("_RedHueRangeB", redHueRangeB);
                material.SetVector("_BlueHueRangeA", blueHueRangeA);
                material.SetVector("_BlueHueRangeB", blueHueRangeB);
                material.SetVector("_GreenHueRangeA", greenHueRangeA);
                material.SetVector("_GreenHueRangeB", greenHueRangeB);
                material.SetFloat("_WaveEdgeIntensity", waveEdgeIntensity);
                material.SetFloat("_WaveEdgeWidth", waveEdgeWidth);
                material.SetColor("_WaveEdgeColor", waveEdgeColor);
                material.SetMatrix("_InverseViewProjMatrix", inverseViewProj);
            }
        }

        sealed class ColorSpreadRenderPass : ScriptableRenderPass
        {
            Material _material;
            ColorSpreadShaderParams _shaderParams;

            public ColorSpreadRenderPass()
            {
                profilingSampler = new ProfilingSampler("Color Spread");
            }

            public void Setup(Material material, in ColorSpreadShaderParams shaderParams)
            {
                _material = material;
                _shaderParams = shaderParams;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_material == null)
                    return;

                var resources = frameData.Get<UniversalResourceData>();

                // Never use activeColorTexture/backBuffer for GetTextureDesc or as blit source —
                // built-in back buffer handles have no valid descriptor and throw ArgumentException.
                var cameraColor = resources.cameraColor;
                if (!cameraColor.IsValid())
                    return;

                _shaderParams.ApplyToMaterial(_material);

                var desc = renderGraph.GetTextureDesc(cameraColor);
                desc.name = "_ColorSpreadTemp";
                desc.clearBuffer = false;
                var temp = renderGraph.CreateTexture(desc);

                renderGraph.AddCopyPass(cameraColor, temp, passName: "Color Spread Copy");

                var blitParams = new BlitMaterialParameters(temp, cameraColor, _material, 0);
                using (var builder = renderGraph.AddBlitPass(blitParams, passName: "Color Spread", returnBuilder: true))
                {
                    if (resources.cameraDepthTexture.IsValid())
                        builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                }

                if (resources.isActiveTargetBackBuffer)
                {
                    var backBuffer = resources.backBufferColor;
                    if (backBuffer.IsValid())
                        renderGraph.AddCopyPass(cameraColor, backBuffer, passName: "Color Spread To BackBuffer");
                }
            }
        }
    }
}
