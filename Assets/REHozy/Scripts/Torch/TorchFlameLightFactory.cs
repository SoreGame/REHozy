using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace REHozy.Torch
{
    public static class TorchFlameLightFactory
    {
        private const string LightChildName = "TorchFlameLight";

        public static Light EnsureLight(Transform anchor, TorchFlameLightSettings settings)
        {
            if (anchor == null)
            {
                return null;
            }

            settings ??= TorchFlameLightSettings.Default;

            var lightTransform = anchor.Find(LightChildName);
            if (lightTransform == null)
            {
                var lightGo = new GameObject(LightChildName);
                lightTransform = lightGo.transform;
                lightTransform.SetParent(anchor, false);
                lightTransform.localPosition = Vector3.zero;
                lightTransform.localRotation = Quaternion.identity;
                lightTransform.localScale = Vector3.one;
            }

            var light = lightTransform.GetComponent<Light>();
            if (light == null)
            {
                light = lightTransform.gameObject.AddComponent<Light>();
            }

            if (lightTransform.GetComponent<UniversalAdditionalLightData>() == null)
            {
                lightTransform.gameObject.AddComponent<UniversalAdditionalLightData>();
            }

            ApplyLightSettings(light, settings);

            var volumetric = lightTransform.GetComponent<VolumetricAdditionalLight>();
            if (volumetric == null)
            {
                volumetric = lightTransform.gameObject.AddComponent<VolumetricAdditionalLight>();
            }

            ApplyVolumetricSettings(volumetric, settings);
            return light;
        }

        public static void ApplyLightSettings(Light light, TorchFlameLightSettings settings)
        {
            if (light == null || settings == null)
            {
                return;
            }

            light.type = LightType.Point;
            light.color = settings.Color;
            light.intensity = settings.Intensity;
            light.range = settings.Range;
            light.shadows = LightShadows.None;
            light.cullingMask = -1;
            light.renderingLayerMask = 1;
        }

        public static void ApplyVolumetricSettings(VolumetricAdditionalLight volumetric, TorchFlameLightSettings settings)
        {
            if (volumetric == null || settings == null)
            {
                return;
            }

            volumetric.Anisotropy = settings.Anisotropy;
            volumetric.Scattering = settings.Scattering;
            volumetric.Radius = settings.VolumetricRadius;
        }
    }
}
