using UnityEngine;

namespace LastExit.Presentation
{
    public interface ILevelDiagnosticControls
    {
        public Vector2 GetContinuePosition();
        public Vector2 GetRepeatPosition();
        public Vector2 GetRestartPosition();
        public Vector2 GetNextPosition();
        public Vector2 GetScanPosition();
        public Vector2 GetRiskPosition();
        public Vector2 GetQuarantinePosition();
        public Vector2 GetQueuePosition(string queueId);
        public Vector2 GetSelectionPosition();
        public Vector2 GetClosePosition();
        public string CctvPhase { get; }
        public float CctvRemaining { get; }
        public string SelectedQueue { get; }
    }
}
