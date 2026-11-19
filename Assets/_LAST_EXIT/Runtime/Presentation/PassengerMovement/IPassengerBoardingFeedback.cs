using System.Collections;

namespace LastExit.Presentation
{
    public interface IPassengerBoardingFeedback
    {
        public IEnumerator Play();
        public IEnumerator PlayImpact();
        public void SetPaused(bool paused);
        public void ResetFeedback();
    }
}
