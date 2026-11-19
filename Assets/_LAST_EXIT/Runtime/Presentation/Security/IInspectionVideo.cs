using System;

namespace LastExit.Presentation
{
    public interface IInspectionVideo
    {
        public event Action<bool> VideoChanged;
    }
}
