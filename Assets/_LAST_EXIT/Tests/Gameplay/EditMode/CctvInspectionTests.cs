using LastExit.Presentation;
using NUnit.Framework;

namespace LastExit.Tests.Gameplay
{
    public class CctvInspectionTests
    {
        [Test]
        public void VideoEndsAtExactlyFourActiveSeconds()
        {
            CctvInspection inspection = new CctvInspection();
            inspection.PlayVideo();
            inspection.Advance(3.99f, false);
            Assert.IsTrue(inspection.IsVideo);
            inspection.Advance(.01f, false);
            Assert.IsFalse(inspection.IsVideo);
            Assert.IsFalse(inspection.IsOpen, "Scan returns to the same board without opening selection.");
            Assert.AreEqual(4, inspection.Elapsed);
        }

        [Test]
        public void PausePreservesTimerAndChoiceDoesNotRequireVideo()
        {
            CctvInspection inspection = new CctvInspection();
            inspection.OpenSelection();
            inspection.Select("C");
            inspection.PlayVideo();
            inspection.Advance(1.25f, false);
            inspection.Advance(30, true);
            Assert.AreEqual(1.25f, inspection.Elapsed);
            Assert.AreEqual("C", inspection.SelectedQueue);
            inspection.Select("A");
            Assert.AreEqual("C", inspection.SelectedQueue, "Selection cannot change during video.");
            inspection.Advance(2.75f, false);
            Assert.IsNull(inspection.SelectedQueue);
            inspection.OpenSelection();
            inspection.Select("D");
            Assert.AreEqual("D", inspection.SelectedQueue);
        }

        [Test]
        public void RetryResetsVideoAndTargetWhileRewatchStartsFourSeconds()
        {
            CctvInspection inspection = new CctvInspection();
            inspection.OpenSelection();
            inspection.Select("B");
            inspection.PlayVideo();
            inspection.Advance(3, false);
            inspection.PlayVideo();
            Assert.AreEqual(0, inspection.Elapsed);
            inspection.Reset();
            Assert.IsFalse(inspection.IsOpen);
            Assert.IsFalse(inspection.IsVideo);
            Assert.IsNull(inspection.SelectedQueue);
            Assert.AreEqual(0, inspection.Elapsed);
        }
    }
}
