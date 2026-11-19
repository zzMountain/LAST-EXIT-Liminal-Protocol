namespace LastExit.Presentation
{
    public interface IAudioSettings : IAudioLevels
    {
        public void SetMuted(bool muted);
        public void SetAtmosphereVolume(float volume);
        public void SetEffectsVolume(float volume);
    }
}
