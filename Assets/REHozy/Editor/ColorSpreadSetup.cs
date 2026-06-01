using REHozy.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace REHozy.Editor
{
    public static class ColorSpreadSetup
    {
        const string ProfilePath = "Assets/Settings/SampleSceneProfile.asset";
        const string RendererPath = "Assets/Settings/PC_Renderer.asset";
        const string SettingsPath = "Assets/REHozy/Settings/ColorSpreadSettings.asset";
        const string ShaderPath = "Assets/REHozy/Shaders/PostProcessing/ColorSpread.shader";
        const string ExemptMaskShaderPath = "Assets/REHozy/Shaders/PostProcessing/ColorSpreadExemptMask.shader";

        [MenuItem("REHozy/Setup Color Spread")]
        public static void Setup()
        {
            var settings = EnsureSettingsAsset();
            EnsureVolumeComponent();
            EnsureRendererFeature();
            AssignSceneControllers(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("Color Spread setup complete.");
        }

        static void AssignSceneControllers(ColorSpreadSettings settings)
        {
            var controllers = Object.FindObjectsByType<ColorSpreadController>(FindObjectsSortMode.None);
            Volume globalVolume = null;

            foreach (var volume in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if (volume.isGlobal)
                {
                    globalVolume = volume;
                    break;
                }
            }

            foreach (var controller in controllers)
            {
                var serialized = new SerializedObject(controller);
                if (globalVolume != null)
                    serialized.FindProperty("volume").objectReferenceValue = globalVolume;
                if (settings != null)
                    serialized.FindProperty("settings").objectReferenceValue = settings;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(controller);
            }
        }

        static ColorSpreadSettings EnsureSettingsAsset()
        {
            var settings = AssetDatabase.LoadAssetAtPath<ColorSpreadSettings>(SettingsPath);
            if (settings != null)
                return settings;

            settings = ScriptableObject.CreateInstance<ColorSpreadSettings>();
            if (!AssetDatabase.IsValidFolder("Assets/REHozy/Settings"))
                AssetDatabase.CreateFolder("Assets/REHozy", "Settings");
            AssetDatabase.CreateAsset(settings, SettingsPath);
            return settings;
        }

        static void EnsureVolumeComponent()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                Debug.LogError($"Volume profile not found at {ProfilePath}");
                return;
            }

            if (!profile.TryGet<ColorSpreadVolume>(out _))
            {
                profile.Add<ColorSpreadVolume>();
                EditorUtility.SetDirty(profile);
            }
        }

        static void EnsureRendererFeature()
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (rendererData == null)
            {
                Debug.LogError($"Renderer data not found at {RendererPath}");
                return;
            }

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
            {
                Debug.LogError($"Shader not found at {ShaderPath}");
                return;
            }

            var exemptMaskShader = AssetDatabase.LoadAssetAtPath<Shader>(ExemptMaskShaderPath);
            if (exemptMaskShader == null)
            {
                Debug.LogError($"Shader not found at {ExemptMaskShaderPath}");
                return;
            }

            foreach (var feature in rendererData.rendererFeatures)
            {
                if (feature is ColorSpreadRendererFeature existing)
                {
                    if (existing != null)
                    {
                        var serialized = new SerializedObject(existing);
                        serialized.FindProperty("shader").objectReferenceValue = shader;
                        serialized.FindProperty("exemptMaskShader").objectReferenceValue = exemptMaskShader;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(rendererData);
                    }
                    return;
                }
            }

            var newFeature = ScriptableObject.CreateInstance<ColorSpreadRendererFeature>();
            newFeature.name = "ColorSpread";
            var featureSerialized = new SerializedObject(newFeature);
            featureSerialized.FindProperty("shader").objectReferenceValue = shader;
            featureSerialized.FindProperty("exemptMaskShader").objectReferenceValue = exemptMaskShader;
            featureSerialized.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.AddObjectToAsset(newFeature, rendererData);
            rendererData.rendererFeatures.Add(newFeature);
            rendererData.SetDirty();
            EditorUtility.SetDirty(rendererData);
        }
    }
}
