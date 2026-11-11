using System;

namespace LastExit.Presentation
{
    public interface IChamberPhases
    {
        public event Action<string> PhaseChanged;
    }
}
