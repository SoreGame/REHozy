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

        private Light _spawnedTorchLight;

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

            SetLit(false);
        }

        public void SetLit(bool lit)
        {
            IsLit = lit;

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
    }
}
