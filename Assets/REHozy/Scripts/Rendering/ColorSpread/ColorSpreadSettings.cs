using UnityEngine;

namespace REHozy.Rendering
{
    [CreateAssetMenu(fileName = "ColorSpreadSettings", menuName = "REHozy/Rendering/Color Spread Settings")]
    public sealed class ColorSpreadSettings : ScriptableObject
    {
        [Header("Wave")]
        [Min(0f)] public float growthSpeed = 12f;
        [Min(0f)] public float maxRadius = 80f;
        [Min(0.01f)] public float edgeSoftness = 4f;
        [Min(0f)] public float noiseScale = 0.05f;
        [Min(0f)] public float noiseStrength = 6f;
        public Texture2D noiseTexture;

        [Header("Wave Visual")]
        [Min(0f)] public float waveEdgeIntensity = 0.75f;
        [Min(0.1f)] public float waveEdgeWidth = 2.5f;
        public Color waveEdgeColor = new(1f, 0.95f, 0.75f, 1f);

        [Header("Red Hue Ranges (0-1)")]
        public Vector2 redHueRangeA = new(0f, 0.08f);
        public Vector2 redHueRangeB = new(0.92f, 1f);

        [Header("Blue Hue Ranges (0-1)")]
        public Vector2 blueHueRangeA = new(0.52f, 0.68f);
        public Vector2 blueHueRangeB = new(0.52f, 0.68f);

        [Header("Green Hue Ranges (0-1)")]
        public Vector2 greenHueRangeA = new(0.28f, 0.45f);
        public Vector2 greenHueRangeB = new(0.28f, 0.45f);

        public Vector4 GetRedHueRangeA() => ToVector4(redHueRangeA);
        public Vector4 GetRedHueRangeB() => ToVector4(redHueRangeB);
        public Vector4 GetBlueHueRangeA() => ToVector4(blueHueRangeA);
        public Vector4 GetBlueHueRangeB() => ToVector4(blueHueRangeB);
        public Vector4 GetGreenHueRangeA() => ToVector4(greenHueRangeA);
        public Vector4 GetGreenHueRangeB() => ToVector4(greenHueRangeB);

        static Vector4 ToVector4(Vector2 range) => new(range.x, range.y, 0f, 0f);
    }
}
