using System.Collections;

namespace LastExit.Presentation
{
    public interface IQuarantinePlayback
    {
        public IEnumerator Execute(IQuarantineBoard board, string passengerId);
        public void SetPaused(bool paused);
        public void ResetState();
    }
}
