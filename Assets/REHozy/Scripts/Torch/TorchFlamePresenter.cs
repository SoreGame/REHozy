using System.Collections;
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
        [SerializeField] private float staticAmbientDelay = 0.45f;

        private bool _isCarriedTorch;
        private bool _isStaticTorch;
        private Coroutine _staticAmbientDelayRoutine;

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
            _isStaticTorch = !_isCarriedTorch && GetComponentInParent<StaticTorch>() != null;
            if (_isCarriedTorch)
            {
                PrewarmBurnAudio();
            }

            SetLit(false);
        }

        private void OnDisable()
        {
            if (_staticAmbientDelayRoutine != null)
            {
                StopCoroutine(_staticAmbientDelayRoutine);
                _staticAmbientDelayRoutine = null;
            }
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
                GameAudio.Play(GameSoundId.TorchIgnite, LightAnchor.position);

                if (_isCarriedTorch)
                {
                    StartBurnLoop();
                }
                else if (_isStaticTorch)
                {
                    ScheduleStaticAmbient();
                }
            }
            else
            {
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

        private void ScheduleStaticAmbient()
        {
            if (_staticAmbientDelayRoutine != null)
            {
                StopCoroutine(_staticAmbientDelayRoutine);
            }

            _staticAmbientDelayRoutine = StartCoroutine(StartStaticAmbientAfterIgnite());
        }

        private IEnumerator StartStaticAmbientAfterIgnite()
        {
            var delay = Mathf.Max(staticAmbientDelay, 0f);
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            StaticTorchAmbientAudio.EnsurePlaying(LightAnchor.position);
            _staticAmbientDelayRoutine = null;
        }

        private void PrewarmBurnAudio()
        {
            if (!GameAudio.TryGetClipEntry(GameSoundId.TorchBurnLoop, out var entry) || entry.clip == null)
            {
                return;
            }

            if (entry.clip.loadState == AudioDataLoadState.Unloaded)
            {
                entry.clip.LoadAudioData();
            }

            EnsureBurnSource();
            ApplyBurnSourceSettings(entry);
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

            ApplyBurnSourceSettings(entry);
            _burnSource.Play();
        }

        private void ApplyBurnSourceSettings(GameAudioClipEntry entry)
        {
            _burnSource.spatialBlend = 0f;
            _burnSource.clip = entry.clip;
            _burnSource.volume = entry.volume;
            _burnSource.pitch = entry.pitchRange.x;
            _burnSource.loop = true;
            _burnSource.outputAudioMixerGroup = entry.mixerGroupOverride != null
                ? entry.mixerGroupOverride
                : GameAudio.GetSfxGroup();
        }

        private void StopBurnLoop()
        {
            if (_burnSource == null)
            {
                return;
            }

            _burnSource.Stop();
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
            _burnSource.spatialBlend = 0f;
        }
    }
}
