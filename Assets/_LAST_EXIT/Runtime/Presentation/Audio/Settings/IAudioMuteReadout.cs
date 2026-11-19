using System;

namespace LastExit.Presentation
{
    public interface IAudioMuteReadout
    {
        public event Action Changed;
        public bool IsMuted { get; }
    }
}
