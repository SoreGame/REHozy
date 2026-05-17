using UnityEngine;

namespace REHozy.Rendering
{
    public sealed class ColorSpreadRuntimeData
    {
        public bool effectEnabled = true;
        public int step;
        public int previousStep;
        public int unlockedMask;
        public int previousMask;
        public int waveAddMask;
        public Vector3 center;
        public float startTime;
        public float growthSpeed = 12f;
        public float maxRadius = 80f;
        public float edgeSoftness = 4f;
        public float noiseScale = 0.05f;
        public float noiseStrength = 6f;
        public Texture noiseTexture;
        public Vector4 redHueRangeA = new(0f, 0.08f, 0f, 0f);
        public Vector4 redHueRangeB = new(0.92f, 1f, 0f, 0f);
        public Vector4 blueHueRangeA = new(0.52f, 0.68f, 0f, 0f);
        public Vector4 blueHueRangeB = new(0.52f, 0.68f, 0f, 0f);
        public Vector4 greenHueRangeA = new(0.28f, 0.45f, 0f, 0f);
        public Vector4 greenHueRangeB = new(0.28f, 0.45f, 0f, 0f);
        public float waveEdgeIntensity = 0.75f;
        public float waveEdgeWidth = 2.5f;
        public Color waveEdgeColor = new(1f, 0.95f, 0.75f, 1f);

        public bool ShouldRender => effectEnabled && step <= (int)ColorSpreadStep.FullColor;
    }
}
