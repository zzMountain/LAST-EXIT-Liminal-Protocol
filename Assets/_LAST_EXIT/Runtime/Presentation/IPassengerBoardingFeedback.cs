using System.Collections;

namespace LastExit.Presentation
{
    public interface IPassengerBoardingFeedback
    {
        public IEnumerator Play();
        public void SetPaused(bool paused);
        public void ResetFeedback();
    }
}
