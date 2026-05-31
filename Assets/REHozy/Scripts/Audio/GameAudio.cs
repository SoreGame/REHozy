using UnityEngine;
using UnityEngine.Audio;

namespace REHozy.Audio
{
    public static class GameAudio
    {
        static GameAudioController _controller;

        internal static void Register(GameAudioController controller)
        {
            _controller = controller;
        }

        internal static void Unregister(GameAudioController controller)
        {
            if (_controller == controller)
            {
                _controller = null;
            }
        }

        public static bool TryGetClipEntry(GameSoundId id, out GameAudioClipEntry entry)
        {
            entry = null;
            return _controller != null && _controller.TryGetClipEntry(id, out entry);
        }

        public static AudioMixerGroup GetSfxGroup()
        {
            return _controller != null ? _controller.SfxGroup : null;
        }

        public static void Play(GameSoundId id, Vector3 worldPosition, float spatialBlend = 1f)
        {
            _controller?.PlayOneShot(id, worldPosition, spatialBlend);
        }

        public static void StartLoop(GameSoundId id, Vector3 worldPosition)
        {
            _controller?.StartLoop(id, worldPosition);
        }

        public static void StopLoop(GameSoundId id)
        {
            _controller?.StopLoop(id);
        }

        public static void SetRainEnabled(bool enabled, float fadeSeconds = 1f)
        {
            _controller?.SetRainEnabled(enabled, fadeSeconds);
        }

        public static void SetAmbientEnabled(bool enabled, float fadeSeconds = 1f)
        {
            _controller?.SetAmbientEnabled(enabled, fadeSeconds);
        }
    }
}
