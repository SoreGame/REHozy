using UnityEngine;

namespace REHozy.Audio
{
    [CreateAssetMenu(fileName = "GameAudioCatalog", menuName = "REHozy/Audio/Game Audio Catalog")]
    public sealed class GameAudioCatalog : ScriptableObject
    {
        [Header("Ambient")]
        public GameAudioClipEntry ambientLoop;

        [Header("Weather")]
        public GameAudioClipEntry rainLoop;

        [Header("Tools — Common")]
        public GameAudioClipEntry toolPickup;
        public GameAudioClipEntry toolReturnHome;

        [Header("Harpoon")]
        public GameAudioClipEntry harpoonImpale;
        public GameAudioClipEntry harpoonDispose;
        public GameAudioClipEntry harpoonBlockedReturn;

        [Header("Shovel")]
        public GameAudioClipEntry shovelDigLoop;

        [Header("Watering")]
        public GameAudioClipEntry waterPourLoop;

        [Header("Torch")]
        public GameAudioClipEntry torchBurnLoop;
        public GameAudioClipEntry torchIgnite;
        public GameAudioClipEntry staticTorchBurnLoop;

        [Header("Decoration")]
        public GameAudioClipEntry propPickup;
        public GameAudioClipEntry propPlace;

        public bool TryGetEntry(GameSoundId id, out GameAudioClipEntry entry)
        {
            entry = id switch
            {
                GameSoundId.AmbientLoop => ambientLoop,
                GameSoundId.RainLoop => rainLoop,
                GameSoundId.ToolPickup => toolPickup,
                GameSoundId.ToolReturnHome => toolReturnHome,
                GameSoundId.HarpoonImpale => harpoonImpale,
                GameSoundId.HarpoonDispose => harpoonDispose,
                GameSoundId.HarpoonBlockedReturn => harpoonBlockedReturn,
                GameSoundId.ShovelDigLoop => shovelDigLoop,
                GameSoundId.WaterPourLoop => waterPourLoop,
                GameSoundId.TorchBurnLoop => torchBurnLoop,
                GameSoundId.TorchIgnite => torchIgnite,
                GameSoundId.StaticTorchBurnLoop => staticTorchBurnLoop,
                GameSoundId.PropPickup => propPickup,
                GameSoundId.PropPlace => propPlace,
                _ => null,
            };

            return entry != null && entry.IsValid;
        }

        public bool TryGetClipEntry(GameSoundId id, out GameAudioClipEntry entry)
        {
            entry = id switch
            {
                GameSoundId.AmbientLoop => ambientLoop,
                GameSoundId.RainLoop => rainLoop,
                GameSoundId.ToolPickup => toolPickup,
                GameSoundId.ToolReturnHome => toolReturnHome,
                GameSoundId.HarpoonImpale => harpoonImpale,
                GameSoundId.HarpoonDispose => harpoonDispose,
                GameSoundId.HarpoonBlockedReturn => harpoonBlockedReturn,
                GameSoundId.ShovelDigLoop => shovelDigLoop,
                GameSoundId.WaterPourLoop => waterPourLoop,
                GameSoundId.TorchBurnLoop => torchBurnLoop,
                GameSoundId.TorchIgnite => torchIgnite,
                GameSoundId.StaticTorchBurnLoop => staticTorchBurnLoop,
                GameSoundId.PropPickup => propPickup,
                GameSoundId.PropPlace => propPlace,
                _ => null,
            };

            return entry != null;
        }

        public void PreloadAllClips()
        {
            PreloadClip(ambientLoop);
            PreloadClip(rainLoop);
            PreloadClip(toolPickup);
            PreloadClip(toolReturnHome);
            PreloadClip(harpoonImpale);
            PreloadClip(harpoonDispose);
            PreloadClip(harpoonBlockedReturn);
            PreloadClip(shovelDigLoop);
            PreloadClip(waterPourLoop);
            PreloadClip(torchBurnLoop);
            PreloadClip(torchIgnite);
            PreloadClip(staticTorchBurnLoop);
            PreloadClip(propPickup);
            PreloadClip(propPlace);
        }

        private static void PreloadClip(GameAudioClipEntry entry)
        {
            if (entry?.clip == null)
            {
                return;
            }

            var clip = entry.clip;
            if (clip.loadState == AudioDataLoadState.Unloaded)
            {
                clip.LoadAudioData();
            }
        }
    }
}
