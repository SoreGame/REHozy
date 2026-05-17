using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace REHozy.Rendering
{
    [Serializable]
    [VolumeComponentMenu("REHozy/Color Spread")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class ColorSpreadVolume : VolumeComponent, IPostProcessComponent
    {
        [Header("Runtime State")]
        public ClampedIntParameter step = new(0, 0, 4);
        public ClampedIntParameter previousStep = new(0, 0, 4);
        public ClampedIntParameter unlockedMask = new(0, 0, 7);
        public ClampedIntParameter previousMask = new(0, 0, 7);
        public ClampedIntParameter waveAddMask = new(0, 0, 7);
        public Vector3Parameter center = new(Vector3.zero);
        public FloatParameter startTime = new(0f);

        [Header("Wave")]
        public MinFloatParameter growthSpeed = new(12f, 0f);
        public MinFloatParameter maxRadius = new(80f, 0f);
        public MinFloatParameter edgeSoftness = new(4f, 0.01f);
        public MinFloatParameter noiseScale = new(0.05f, 0f);
        public MinFloatParameter noiseStrength = new(6f, 0f);
        public TextureParameter noiseTexture = new(null);

        [Header("Hue Ranges")]
        public Vector4Parameter redHueRangeA = new(new Vector4(0f, 0.08f, 0f, 0f));
        public Vector4Parameter redHueRangeB = new(new Vector4(0.92f, 1f, 0f, 0f));
        public Vector4Parameter blueHueRangeA = new(new Vector4(0.52f, 0.68f, 0f, 0f));
        public Vector4Parameter blueHueRangeB = new(new Vector4(0.52f, 0.68f, 0f, 0f));
        public Vector4Parameter greenHueRangeA = new(new Vector4(0.28f, 0.45f, 0f, 0f));
        public Vector4Parameter greenHueRangeB = new(new Vector4(0.28f, 0.45f, 0f, 0f));

        [Header("Wave Visual")]
        public ClampedFloatParameter waveEdgeIntensity = new(0.75f, 0f, 2f);
        public ClampedFloatParameter waveEdgeWidth = new(2.5f, 0.1f, 20f);
        public ColorParameter waveEdgeColor = new(new Color(1f, 0.95f, 0.75f, 1f), true, false, true);

        public bool IsActive() => active && step.value <= (int)ColorSpreadStep.FullColor;

        [Obsolete("Unused")]
        public bool IsTileCompatible() => false;
    }
}
