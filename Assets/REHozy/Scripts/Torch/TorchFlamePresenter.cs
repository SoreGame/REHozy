using REHozy.Audio;
using REHozy.CarryableTools;
using UnityEngine;

namespace REHozy.Torch
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Torch/Torch Flame Presenter")]
    public sealed class TorchFlamePresenter : MonoBehaviour
    {
        [SerializeField] private ParticleSystem[] particles;
        [SerializeField] private Light[] lights;
        [SerializeField] private Transform lightAnchor;
        [SerializeField] private TorchFlameLightSettings torchLightSettings = new();

        [Header("Burn Audio")]
        [Tooltip("Carried torch uses 2D audio so it stays audible with a distant third-person camera.")]
        [SerializeField] private float carriedSpatialBlend;
        [SerializeField] private float worldSpatialBlend = 1f;
        [SerializeField] private float burnMinDistance = 2f;
        [SerializeField] private float burnMaxDistance = 80f;

        private bool _isCarriedTorch;

        private Light _spawnedTorchLight;
        private AudioSource _burnSource;

        public bool IsLit { get; private set; }

        private Transform LightAnchor => lightAnchor != null ? lightAnchor : transform;

        private void Reset()
        {
            particles = GetComponentsInChildren<ParticleSystem>(true);
            lights = GetComponentsInChildren<Light>(true);
            SetLit(false);
        }

        private void Awake()
        {
            if (particles == null || particles.Length == 0)
            {
                particles = GetComponentsInChildren<ParticleSystem>(true);
            }

            if (lights == null || lights.Length == 0)
            {
                lights = GetComponentsInChildren<Light>(true);
            }

            _isCarriedTorch = GetComponentInParent<CarryableToolCore>() != null;
            SetLit(false);
        }

        private void LateUpdate()
        {
            if (!IsLit || _burnSource == null || !_burnSource.isPlaying)
            {
                return;
            }

            _burnSource.transform.position = LightAnchor.position;
        }

        public void SetLit(bool lit)
        {
            var wasLit = IsLit;
            if (wasLit == lit)
            {
                UpdateVisuals(lit);
                return;
            }

            IsLit = lit;
            UpdateVisuals(lit);

            if (lit)
            {
                GameAudio.Play(
                    GameSoundId.TorchIgnite,
                    LightAnchor.position,
                    _isCarriedTorch ? carriedSpatialBlend : worldSpatialBlend);
                StartBurnLoop();
            }
            else
            {
                GameAudio.Play(
                    GameSoundId.TorchExtinguish,
                    LightAnchor.position,
                    _isCarriedTorch ? carriedSpatialBlend : worldSpatialBlend);
                StopBurnLoop();
            }
        }

        private void UpdateVisuals(bool lit)
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            if (particles != null)
            {
                foreach (var ps in particles)
                {
                    if (ps == null)
                    {
                        continue;
                    }

                    if (lit)
                    {
                        if (!ps.gameObject.activeSelf)
                        {
                            ps.gameObject.SetActive(true);
                        }

                        var emission = ps.emission;
                        emission.enabled = true;
                        if (!ps.isPlaying)
                        {
                            ps.Play(true);
                        }
                    }
                    else
                    {
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        var emission = ps.emission;
                        emission.enabled = false;
                    }
                }
            }

            if (lit)
            {
                _spawnedTorchLight = TorchFlameLightFactory.EnsureLight(LightAnchor, torchLightSettings);
            }

            if (_spawnedTorchLight != null)
            {
                TorchFlameLightFactory.ApplyLightSettings(_spawnedTorchLight, torchLightSettings);
                _spawnedTorchLight.enabled = lit;
            }

            if (lights != null)
            {
                foreach (var light in lights)
                {
                    if (light == null || light == _spawnedTorchLight)
                    {
                        continue;
                    }

                    if (lit)
                    {
                        TorchFlameLightFactory.ApplyLightSettings(light, torchLightSettings);
                        var volumetric = light.GetComponent<VolumetricAdditionalLight>();
                        if (volumetric != null)
                        {
                            TorchFlameLightFactory.ApplyVolumetricSettings(volumetric, torchLightSettings);
                        }
                    }

                    light.enabled = lit;
                }
            }
        }

        private void StartBurnLoop()
        {
            if (!GameAudio.TryGetClipEntry(GameSoundId.TorchBurnLoop, out var entry) || entry.clip == null)
            {
                return;
            }

            EnsureBurnSource();

            if (_burnSource.isPlaying && _burnSource.clip == entry.clip && _burnSource.loop)
            {
                return;
            }

            ConfigureBurnSourceSpatial();
            _burnSource.clip = entry.clip;
            _burnSource.volume = entry.volume;
            _burnSource.pitch = entry.pitchRange.x;
            _burnSource.loop = true;
            _burnSource.outputAudioMixerGroup = entry.mixerGroupOverride != null
                ? entry.mixerGroupOverride
                : GameAudio.GetSfxGroup();
            _burnSource.transform.position = LightAnchor.position;
            _burnSource.Play();
        }

        private void StopBurnLoop()
        {
            if (_burnSource == null)
            {
                return;
            }

            _burnSource.Stop();
        }

        private void ConfigureBurnSourceSpatial()
        {
            if (_burnSource == null)
            {
                return;
            }

            _burnSource.spatialBlend = _isCarriedTorch ? carriedSpatialBlend : worldSpatialBlend;
            _burnSource.minDistance = burnMinDistance;
            _burnSource.maxDistance = burnMaxDistance;
        }

        private void EnsureBurnSource()
        {
            if (_burnSource != null)
            {
                return;
            }

            var audioObject = new GameObject("TorchBurnAudio");
            audioObject.transform.SetParent(LightAnchor, false);
            _burnSource = audioObject.AddComponent<AudioSource>();
            _burnSource.playOnAwake = false;
            _burnSource.rolloffMode = AudioRolloffMode.Logarithmic;
            ConfigureBurnSourceSpatial();
        }
    }
}
