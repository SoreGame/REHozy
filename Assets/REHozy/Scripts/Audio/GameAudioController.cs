using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace REHozy.Audio
{
    [DisallowMultipleComponent]
    [AddComponentMenu("REHozy/Audio/Game Audio Controller")]
    public sealed class GameAudioController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private GameAudioCatalog catalog;
        [SerializeField] private AudioMixer audioMixer;

        [Header("Mixer Groups")]
        [SerializeField] private AudioMixerGroup ambientGroup;
        [SerializeField] private AudioMixerGroup weatherGroup;
        [SerializeField] private AudioMixerGroup sfxGroup;

        [Header("Startup")]
        [SerializeField] private bool playAmbientOnStart = true;
        [SerializeField] private bool rainEnabledOnStart = true;

        [Header("3D Playback")]
        [SerializeField] private int oneShotPoolSize = 8;
        [SerializeField] private float minDistance = 1f;
        [SerializeField] private float maxDistance = 25f;

        [Header("Fade")]
        [SerializeField] private float defaultFadeSeconds = 1f;

        AudioSource _ambientSource;
        AudioSource _rainSource;
        readonly List<AudioSource> _pool = new();
        readonly Dictionary<GameSoundId, AudioSource> _activeLoops = new();
        readonly Dictionary<GameSoundId, Coroutine> _fadeRoutines = new();

        float _ambientTargetVolume = 1f;
        float _rainTargetVolume = 1f;

        void Awake()
        {
            GameAudio.Register(this);
            EnsureSources();
        }

        void OnDestroy()
        {
            GameAudio.Unregister(this);
        }

        void Start()
        {
            if (playAmbientOnStart)
            {
                SetAmbientEnabled(true, 0f);
            }

            SetRainEnabled(rainEnabledOnStart, 0f);
        }

        public AudioMixerGroup SfxGroup => sfxGroup;

        public bool TryGetClipEntry(GameSoundId id, out GameAudioClipEntry entry)
        {
            entry = null;
            return catalog != null && catalog.TryGetClipEntry(id, out entry);
        }

        public void PlayOneShot(GameSoundId id, Vector3 worldPosition, float spatialBlend = 1f)
        {
            if (catalog == null || !catalog.TryGetEntry(id, out var entry))
            {
                return;
            }

            var source = RentPoolSource();
            if (source == null)
            {
                return;
            }

            Configure3DSource(source, entry, worldPosition, loop: false, spatialBlend);
            source.Play();
            StartCoroutine(ReleaseWhenFinished(source));
        }

        public void StartLoop(GameSoundId id, Vector3 worldPosition)
        {
            if (catalog == null || !catalog.TryGetEntry(id, out var entry))
            {
                return;
            }

            if (_activeLoops.TryGetValue(id, out var existing) && existing != null)
            {
                existing.transform.position = worldPosition;
                if (!existing.isPlaying)
                {
                    existing.Play();
                }

                return;
            }

            var source = RentPoolSource();
            if (source == null)
            {
                return;
            }

            Configure3DSource(source, entry, worldPosition, loop: true);
            source.Play();
            _activeLoops[id] = source;
        }

        public void StopLoop(GameSoundId id)
        {
            if (!_activeLoops.TryGetValue(id, out var source) || source == null)
            {
                return;
            }

            source.Stop();
            _activeLoops.Remove(id);
            ReturnPoolSource(source);
        }

        public void SetAmbientEnabled(bool enabled, float fadeSeconds = -1f)
        {
            if (_ambientSource == null)
            {
                return;
            }

            var fade = fadeSeconds >= 0f ? fadeSeconds : defaultFadeSeconds;
            _ambientTargetVolume = enabled ? ResolveEntryVolume(GameSoundId.AmbientLoop) : 0f;
            StartFade(GameSoundId.AmbientLoop, _ambientSource, _ambientTargetVolume, fade, stopWhenSilent: !enabled);
        }

        public void SetRainEnabled(bool enabled, float fadeSeconds = -1f)
        {
            if (_rainSource == null)
            {
                return;
            }

            var fade = fadeSeconds >= 0f ? fadeSeconds : defaultFadeSeconds;
            _rainTargetVolume = enabled ? ResolveEntryVolume(GameSoundId.RainLoop) : 0f;
            StartFade(GameSoundId.RainLoop, _rainSource, _rainTargetVolume, fade, stopWhenSilent: !enabled);
        }

        void EnsureSources()
        {
            _ambientSource = CreateLoopSource("AmbientSource", ambientGroup, spatialBlend: 0f);
            _rainSource = CreateLoopSource("RainSource", weatherGroup, spatialBlend: 0f);

            _pool.Clear();
            for (var i = 0; i < Mathf.Max(oneShotPoolSize, 1); i++)
            {
                var source = CreateLoopSource($"SfxSource_{i}", sfxGroup, spatialBlend: 1f);
                source.gameObject.SetActive(false);
                _pool.Add(source);
            }
        }

        AudioSource CreateLoopSource(string objectName, AudioMixerGroup group, float spatialBlend)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = spatialBlend;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.outputAudioMixerGroup = group;
            return source;
        }

        void Configure3DSource(
            AudioSource source,
            GameAudioClipEntry entry,
            Vector3 worldPosition,
            bool loop,
            float spatialBlend = 1f)
        {
            source.gameObject.SetActive(true);
            source.transform.position = worldPosition;
            source.clip = entry.clip;
            source.volume = entry.volume;
            source.pitch = entry.SamplePitch();
            source.loop = loop || entry.loop;
            source.spatialBlend = spatialBlend;
            source.outputAudioMixerGroup = entry.mixerGroupOverride != null ? entry.mixerGroupOverride : sfxGroup;
        }

        AudioSource RentPoolSource()
        {
            for (var i = 0; i < _pool.Count; i++)
            {
                var source = _pool[i];
                if (source != null && !source.isPlaying && !_activeLoops.ContainsValue(source))
                {
                    return source;
                }
            }

            var extra = CreateLoopSource($"SfxSource_Extra_{_pool.Count}", sfxGroup, spatialBlend: 1f);
            _pool.Add(extra);
            return extra;
        }

        void ReturnPoolSource(AudioSource source)
        {
            if (source == null)
            {
                return;
            }

            source.Stop();
            source.clip = null;
            source.loop = false;
            source.gameObject.SetActive(false);
        }

        IEnumerator ReleaseWhenFinished(AudioSource source)
        {
            if (source == null)
            {
                yield break;
            }

            while (source.isPlaying)
            {
                yield return null;
            }

            if (!_activeLoops.ContainsValue(source))
            {
                ReturnPoolSource(source);
            }
        }

        void StartFade(GameSoundId id, AudioSource source, float targetVolume, float fadeSeconds, bool stopWhenSilent)
        {
            if (source == null)
            {
                return;
            }

            if (!source.isPlaying && targetVolume > 0f)
            {
                if (!TryPrepareLoopSource(id, source))
                {
                    return;
                }

                source.volume = 0f;
                source.Play();
            }

            if (_fadeRoutines.TryGetValue(id, out var running) && running != null)
            {
                StopCoroutine(running);
            }

            _fadeRoutines[id] = StartCoroutine(FadeSource(id, source, targetVolume, fadeSeconds, stopWhenSilent));
        }

        bool TryPrepareLoopSource(GameSoundId id, AudioSource source)
        {
            if (catalog == null)
            {
                return false;
            }

            var entry = id switch
            {
                GameSoundId.AmbientLoop => catalog.ambientLoop,
                GameSoundId.RainLoop => catalog.rainLoop,
                _ => null,
            };

            if (entry == null || entry.clip == null)
            {
                return false;
            }

            source.clip = entry.clip;
            source.loop = true;
            source.pitch = entry.pitchRange.x;
            source.volume = entry.volume;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = id == GameSoundId.RainLoop
                ? weatherGroup != null ? weatherGroup : entry.mixerGroupOverride
                : ambientGroup != null ? ambientGroup : entry.mixerGroupOverride;
            return true;
        }

        float ResolveEntryVolume(GameSoundId id)
        {
            if (catalog == null)
            {
                return 1f;
            }

            var entry = id switch
            {
                GameSoundId.AmbientLoop => catalog.ambientLoop,
                GameSoundId.RainLoop => catalog.rainLoop,
                _ => null,
            };

            if (entry != null && entry.clip != null)
            {
                return entry.volume;
            }

            if (catalog.TryGetEntry(id, out var sfxEntry))
            {
                return sfxEntry.volume;
            }

            return 1f;
        }

        IEnumerator FadeSource(GameSoundId id, AudioSource source, float targetVolume, float fadeSeconds, bool stopWhenSilent)
        {
            var startVolume = source.volume;
            var duration = Mathf.Max(fadeSeconds, 0f);

            if (duration <= 0f)
            {
                source.volume = targetVolume;
            }
            else
            {
                var elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    var t = Mathf.Clamp01(elapsed / duration);
                    source.volume = Mathf.Lerp(startVolume, targetVolume, t);
                    yield return null;
                }

                source.volume = targetVolume;
            }

            if (stopWhenSilent && targetVolume <= 0f)
            {
                source.Stop();
                source.clip = null;
            }

            _fadeRoutines.Remove(id);
        }

#if UNITY_EDITOR
        public void SetEditorReferences(
            GameAudioCatalog catalogAsset,
            AudioMixer mixer,
            AudioMixerGroup ambient,
            AudioMixerGroup weather,
            AudioMixerGroup sfx)
        {
            catalog = catalogAsset;
            audioMixer = mixer;
            ambientGroup = ambient;
            weatherGroup = weather;
            sfxGroup = sfx;
        }
#endif
    }
}
