using System;
using LastExit.Domain;

namespace LastExit.Presentation
{
    public interface ILevelInteraction
    {
        public event Action Changed;
        public BoardSnapshot Snapshot { get; }
        public bool CanSelect(string busId);
        public bool CanMove(string rowId, string busId, float leftX);
        public bool TryMove(string rowId, string busId, int leftX);
    }
}
