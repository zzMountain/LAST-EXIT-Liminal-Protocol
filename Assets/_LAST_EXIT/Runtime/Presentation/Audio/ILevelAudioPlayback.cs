namespace LastExit.Presentation
{
    public interface ILevelAudioPlayback
    {
        public bool IsActivated { get; }
        public void Activate();
        public void Play(LevelSound sound);
        public void SetLoop(LevelSound sound, bool wanted);
        public void SetPaused(bool paused);
        public void ResetTransient();
        public void NotifyDragMotion();
    }
}
