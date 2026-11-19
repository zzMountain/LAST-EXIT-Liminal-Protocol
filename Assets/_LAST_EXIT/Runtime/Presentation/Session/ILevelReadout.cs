using LastExit.Domain;

namespace LastExit.Presentation
{
    public interface ILevelReadout
    {
        public BoardSnapshot Snapshot { get; }
        public ILevelTutorialReadout Tutorial { get; }
        public bool IsPaused { get; }
        public string LevelId { get; }
        public bool HasNextLevel { get; }
        public int ResultStars { get; }
        public bool CanUseSecurity { get; }
        public bool CanRequestScan { get; }
        public bool IsScanQueued { get; }
        public bool IsCctvAnomaly(string queueId);
        public LaneAccess GetLaneAccess(string queueId);
    }
}
