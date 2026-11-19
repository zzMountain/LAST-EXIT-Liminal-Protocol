using System;

namespace LastExit.Platform
{
    public interface ICampaignPlatform
    {
        public event Action ProgressReady;
        public event Action PauseChanged;

        public bool IsPaused { get; }
        public int ResumeLevelIndex { get; }

        public void Initialize(int levelCount);
        public void MarkSceneReady();
        public void CancelSceneReadiness();
        public void SetGameplayActive(bool active);
        public void RecordCompletedLevel(CampaignResult result);
        public void SetCurrentLevel(int levelIndex);
        public void ShowTransitionAd(Action completed);
    }
}
