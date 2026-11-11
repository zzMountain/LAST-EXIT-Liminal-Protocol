using System;

namespace LastExit.Presentation
{
    public interface ILevelAudioPlayback
    {
        public event Action<LevelSound> Played;
        public bool IsPaused { get; }
        public bool IsActivated { get; }
        public int ActiveVoiceCount { get; }
        public void Activate();
        public void Play(LevelSound sound);
        public void SetLoop(LevelSound sound, bool wanted);
        public void SetPaused(bool paused);
        public void ResetTransient();
        public void NotifyDragMotion();
    }
}
