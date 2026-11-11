using System;
using LastExit.Domain;

namespace LastExit.Presentation
{
    public interface IStepCompletion
    {
        public event Action<BoardStep> Completed;
    }
}
