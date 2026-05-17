using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace REHozy.Rendering
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Rendering/Color Spread Controller")]
    [DefaultExecutionOrder(-100)]
    [ExecuteAlways]
    public sealed class ColorSpreadController : MonoBehaviour
    {
        public static ColorSpreadController Instance { get; private set; }

        [SerializeField] Volume volume;
        [SerializeField] ColorSpreadSettings settings;
        [SerializeField] bool effectEnabled = true;

        ColorSpreadStep _currentStep = ColorSpreadStep.Grayscale;

        public ColorSpreadStep CurrentStep => _currentStep;
        public ColorSpreadSettings Settings => settings;
        public bool EffectEnabled => effectEnabled;
        public ColorSpreadRuntimeData RuntimeData { get; } = new();
        public ColorSpreadVolume VolumeComponent =>
            VolumeManager.instance.isInitialized
                ? VolumeManager.instance.stack.GetComponent<ColorSpreadVolume>()
                : null;

        void OnEnable()
        {
            if (Instance != null && Instance != this)
                Debug.LogWarning("Multiple ColorSpreadController instances detected.", this);
            else
                Instance = this;

            if (volume == null)
                volume = FindFirstObjectByType<Volume>();

            ApplySettingsToRuntimeData();
            ApplyEffectEnabledState();
            PushRuntimeDataToState(ColorSpreadStep.Grayscale, transform.position, false);
        }

        void Start()
        {
            if (Application.isPlaying)
                StartCoroutine(SyncVolumeWhenReady());
        }

        void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            ApplySettingsToRuntimeData();
            ApplyEffectEnabledState();
        }
#endif

        public void SetEffectEnabled(bool enabled)
        {
            if (effectEnabled == enabled)
                return;

            effectEnabled = enabled;
            ApplyEffectEnabledState();
            TrySyncToVolumeStack();
        }

        public void SetStep(ColorSpreadStep step, Vector3 worldOrigin)
        {
            SetStep(step, worldOrigin, true);
        }

        public void ResetEffect()
        {
            SetStep(ColorSpreadStep.Grayscale, transform.position);
        }

        public void RefreshFromSettings()
        {
            ApplySettingsToRuntimeData();
            TrySyncToVolumeStack();
        }

        public float GetCurrentEffectRadius()
        {
            if (settings == null)
                return 0f;

            var elapsed = Time.time - RuntimeData.startTime;
            return Mathf.Min(elapsed * RuntimeData.growthSpeed, RuntimeData.maxRadius);
        }

        public void SetStep(ColorSpreadStep step, Vector3 worldOrigin, bool restartWave)
        {
            PushRuntimeDataToState(step, worldOrigin, restartWave);
            TrySyncToVolumeStack();
        }

        void PushRuntimeDataToState(ColorSpreadStep step, Vector3 worldOrigin, bool restartWave)
        {
            ApplySettingsToRuntimeData();

            var oldStep = _currentStep;
            var oldMask = RuntimeData.unlockedMask;
            _currentStep = step;
            RuntimeData.effectEnabled = effectEnabled;
            RuntimeData.step = (int)step;
            RuntimeData.center = worldOrigin;

            if (step == ColorSpreadStep.Grayscale)
            {
                RuntimeData.unlockedMask = 0;
                RuntimeData.previousMask = 0;
                RuntimeData.waveAddMask = 0;
                RuntimeData.previousStep = 0;
                RuntimeData.startTime = Time.time;
                return;
            }

            var addMask = ColorSpreadPaletteMask.FromStep(step);
            RuntimeData.waveAddMask = addMask;

            if (restartWave && step == oldStep)
                RuntimeData.previousMask = oldMask & ~addMask;
            else
                RuntimeData.previousMask = oldMask;

            RuntimeData.unlockedMask = oldMask | addMask;
            RuntimeData.previousStep = (int)oldStep;

            if (restartWave || RuntimeData.startTime <= 0f)
                RuntimeData.startTime = Time.time;
        }

        void ApplyEffectEnabledState()
        {
            RuntimeData.effectEnabled = effectEnabled;
        }

        void ApplySettingsToRuntimeData()
        {
            if (settings == null)
                return;

            RuntimeData.growthSpeed = settings.growthSpeed;
            RuntimeData.maxRadius = settings.maxRadius;
            RuntimeData.edgeSoftness = settings.edgeSoftness;
            RuntimeData.noiseScale = settings.noiseScale;
            RuntimeData.noiseStrength = settings.noiseStrength;
            RuntimeData.noiseTexture = settings.noiseTexture;
            RuntimeData.redHueRangeA = settings.GetRedHueRangeA();
            RuntimeData.redHueRangeB = settings.GetRedHueRangeB();
            RuntimeData.blueHueRangeA = settings.GetBlueHueRangeA();
            RuntimeData.blueHueRangeB = settings.GetBlueHueRangeB();
            RuntimeData.greenHueRangeA = settings.GetGreenHueRangeA();
            RuntimeData.greenHueRangeB = settings.GetGreenHueRangeB();
            RuntimeData.waveEdgeIntensity = settings.waveEdgeIntensity;
            RuntimeData.waveEdgeWidth = settings.waveEdgeWidth;
            RuntimeData.waveEdgeColor = settings.waveEdgeColor;
        }

        IEnumerator SyncVolumeWhenReady()
        {
            const int maxFrames = 120;
            for (var i = 0; i < maxFrames; i++)
            {
                if (TrySyncToVolumeStack())
                    yield break;
                yield return null;
            }

            Debug.LogWarning(
                "ColorSpread: Volume stack sync skipped. Effect still runs via runtime data. " +
                "For Volume overrides, set Global Volume Weight to 1 or add a dedicated Volume (Weight = 1).",
                this);
        }

        public bool TrySyncToVolumeStack()
        {
            if (!VolumeManager.instance.isInitialized)
                return false;

            var volumeComponent = VolumeManager.instance.stack.GetComponent<ColorSpreadVolume>();
            if (volumeComponent == null)
                return false;

            volumeComponent.active = effectEnabled;
            volumeComponent.step.Override(RuntimeData.step);
            volumeComponent.previousStep.Override(RuntimeData.previousStep);
            volumeComponent.unlockedMask.Override(RuntimeData.unlockedMask);
            volumeComponent.previousMask.Override(RuntimeData.previousMask);
            volumeComponent.waveAddMask.Override(RuntimeData.waveAddMask);
            volumeComponent.center.Override(RuntimeData.center);
            volumeComponent.startTime.Override(RuntimeData.startTime);
            volumeComponent.growthSpeed.Override(RuntimeData.growthSpeed);
            volumeComponent.maxRadius.Override(RuntimeData.maxRadius);
            volumeComponent.edgeSoftness.Override(RuntimeData.edgeSoftness);
            volumeComponent.noiseScale.Override(RuntimeData.noiseScale);
            volumeComponent.noiseStrength.Override(RuntimeData.noiseStrength);

            if (RuntimeData.noiseTexture != null)
                volumeComponent.noiseTexture.Override(RuntimeData.noiseTexture);

            volumeComponent.redHueRangeA.Override(RuntimeData.redHueRangeA);
            volumeComponent.redHueRangeB.Override(RuntimeData.redHueRangeB);
            volumeComponent.blueHueRangeA.Override(RuntimeData.blueHueRangeA);
            volumeComponent.blueHueRangeB.Override(RuntimeData.blueHueRangeB);
            volumeComponent.greenHueRangeA.Override(RuntimeData.greenHueRangeA);
            volumeComponent.greenHueRangeB.Override(RuntimeData.greenHueRangeB);
            volumeComponent.waveEdgeIntensity.Override(RuntimeData.waveEdgeIntensity);
            volumeComponent.waveEdgeWidth.Override(RuntimeData.waveEdgeWidth);
            volumeComponent.waveEdgeColor.Override(RuntimeData.waveEdgeColor);
            return true;
        }
    }
}
