using System;
using UnityEngine;

namespace LastExit.Presentation
{
    public class AudioPreferences : IAudioSettings
    {
        private const string MutedKey = "LastExit.Audio.Muted";
        private const string AtmosphereKey = "LastExit.Audio.Atmosphere";
        private const string EffectsKey = "LastExit.Audio.Effects";
        private const float DefaultAtmosphereVolume = 0.32f;
        private const float DefaultEffectsVolume = 0.8f;

        public AudioPreferences()
        {
            IsMuted = PlayerPrefs.GetInt(MutedKey, 0) != 0;
            AtmosphereVolume = ClampVolume(PlayerPrefs.GetFloat(AtmosphereKey, DefaultAtmosphereVolume),
                DefaultAtmosphereVolume);
            EffectsVolume = ClampVolume(PlayerPrefs.GetFloat(EffectsKey, DefaultEffectsVolume),
                DefaultEffectsVolume);
        }

        public event Action Changed;

        public bool IsMuted { get; private set; }
        public float AtmosphereVolume { get; private set; }
        public float EffectsVolume { get; private set; }

        public void SetMuted(bool muted)
        {
            if (IsMuted == muted)
            {
                return;
            }

            IsMuted = muted;
            PlayerPrefs.SetInt(MutedKey, muted ? 1 : 0);
            PublishChange();
        }

        public void SetAtmosphereVolume(float volume)
        {
            float clampedVolume = ClampVolume(volume, DefaultAtmosphereVolume);

            if (AtmosphereVolume == clampedVolume)
            {
                return;
            }

            AtmosphereVolume = clampedVolume;
            PlayerPrefs.SetFloat(AtmosphereKey, clampedVolume);
            PublishChange();
        }

        public void SetEffectsVolume(float volume)
        {
            float clampedVolume = ClampVolume(volume, DefaultEffectsVolume);

            if (EffectsVolume == clampedVolume)
            {
                return;
            }

            EffectsVolume = clampedVolume;
            PlayerPrefs.SetFloat(EffectsKey, clampedVolume);
            PublishChange();
        }

        private static float ClampVolume(float volume, float fallback)
        {
            return float.IsNaN(volume) ? fallback : Mathf.Clamp01(volume);
        }

        private void PublishChange()
        {
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
