using REHozy.Audio;
using UnityEngine;

namespace REHozy.Torch
{
    /// <summary>
    /// One shared burn ambience for all static torches — starts after the first one is lit.
    /// </summary>
    public static class StaticTorchAmbientAudio
    {
        private static bool _ambientStarted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _ambientStarted = false;
        }

        public static void EnsurePlaying(Vector3 referencePosition)
        {
            if (_ambientStarted)
            {
                return;
            }

            if (!GameAudio.TryGetClipEntry(GameSoundId.StaticTorchBurnLoop, out var entry) || entry.clip == null)
            {
                return;
            }

            _ambientStarted = true;
            GameAudio.StartLoop(GameSoundId.StaticTorchBurnLoop, referencePosition);
        }
    }
}
