using System.Collections.Generic;
using LastExit.Domain;

namespace LastExit.Presentation
{
    public interface ILevelTrace
    {
        public BoardSnapshot Snapshot { get; }
        public TutorialStep TutorialStep { get; }
        public bool IsPaused { get; }
        public string LevelId { get; }
        public bool HasNextLevel { get; }
        public string ArrivalVariantId { get; }
        public int GridWidth { get; }
        public IReadOnlyList<string> Trace { get; }
        public BoardStep PendingStep { get; }
        public IReadOnlyList<BoardStep> BoardingSteps { get; }
    }
}
