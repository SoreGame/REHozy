using System;
using UnityEngine;

namespace REHozy.Torch
{
    [Serializable]
    public sealed class TorchFlameLightSettings
    {
        [SerializeField] private Color color = new(1f, 0.55f, 0.15f, 1f);
        [SerializeField] private float intensity = 1f;
        [SerializeField] private float range = 10f;
        [SerializeField] private float anisotropy = 0.25f;
        [SerializeField] private float scattering = 1f;
        [SerializeField] private float volumetricRadius = 0.2f;

        public Color Color => color;
        public float Intensity => intensity;
        public float Range => range;
        public float Anisotropy => anisotropy;
        public float Scattering => scattering;
        public float VolumetricRadius => volumetricRadius;

        public static TorchFlameLightSettings Default => new();
    }
}
