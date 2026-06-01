using System;
using UnityEngine;
using UnityEngine.Audio;

namespace REHozy.Audio
{
    [Serializable]
    public sealed class GameAudioClipEntry
    {
        public AudioClip clip;
        [Min(0f)] public float volume = 1f;
        public Vector2 pitchRange = Vector2.one;
        public bool loop;
        public AudioMixerGroup mixerGroupOverride;

        public bool IsValid => clip != null;

        public float SamplePitch()
        {
            if (pitchRange.x >= pitchRange.y)
            {
                return pitchRange.x;
            }

            return UnityEngine.Random.Range(pitchRange.x, pitchRange.y);
        }
    }
}
