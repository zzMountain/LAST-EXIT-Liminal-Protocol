namespace LastExit.Presentation
{
    public interface IAudioLevels : IAudioMuteReadout
    {
        public float AtmosphereVolume { get; }
        public float EffectsVolume { get; }
    }
}
