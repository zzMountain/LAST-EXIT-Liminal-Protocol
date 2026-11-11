using System;
using LastExit.Domain;

namespace LastExit.Presentation
{
    public interface ILevelAudioReadout
    {
        public event Action Changed;
        public event Action<BoardStep> StepRequested;
        public BoardSnapshot Snapshot { get; }
        public bool IsPaused { get; }
    }
}
