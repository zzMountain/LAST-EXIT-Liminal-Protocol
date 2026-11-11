using System;

namespace LastExit.Presentation
{
    public interface ILevelDragFeedback
    {
        public event Action<string> SelectionChanged;
        public event Action Moved;
        public event Action<bool> Released;
        public event Action Rejected;
    }
}
