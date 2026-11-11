using LastExit.Platform;
using NUnit.Framework;
using UnityEngine;

namespace LastExit.Tests.Platform
{
    public class CampaignProgressTests
    {
        [TestCase("{broken")]
        [TestCase("{}")]
        [TestCase("{\"version\":1,\"completedLevels\":[]}")]
        [TestCase("{\"version\":2,\"currentLevel\":0,\"completedLevels\":[]}")]
        [TestCase("{\"version\":1,\"currentLevel\":15,\"completedLevels\":[]}")]
        [TestCase("{\"version\":1,\"currentLevel\":0,\"completedLevels\":[0,0]}")]
        [TestCase("{\"version\":1,\"currentLevel\":0,\"completedLevels\":[-1]}")]
        public void DecodeRejectsMalformedUnsupportedAndInvalidProgress(string json)
        {
            Assert.That(CampaignProgress.TryDecode(json, 15, out CampaignProgress progress), Is.False);
            Assert.That(progress, Is.Null);
        }

        [Test]
        public void DecodeAcceptsFinalCampaignCheckpoint()
        {
            const string Json = "{\"version\":1,\"currentLevel\":14,\"completedLevels\":[0,1,14]}";
            Assert.That(CampaignProgress.TryDecode(Json, 15, out CampaignProgress progress), Is.True);
            Assert.That(progress.currentLevel, Is.EqualTo(14));
            Assert.That(progress.completedLevels, Is.EqualTo(new[] { 0, 1, 14 }));
            Assert.That(progress.bestResults, Is.Empty);
        }

        [Test]
        public void DecodeRetainsMeasuredBestResult()
        {
            const string Json = "{\"version\":1,\"currentLevel\":1,\"completedLevels\":[0],\"bestResults\":[" +
                "{\"levelIndex\":0,\"stars\":3,\"cctvUses\":1,\"emergencyResets\":0,\"lostSeats\":1}]}";
            Assert.That(CampaignProgress.TryDecode(Json, 15, out CampaignProgress progress), Is.True);
            Assert.That(progress.bestResults[0].stars, Is.EqualTo(3));
            Assert.That(progress.bestResults[0].lostSeats, Is.EqualTo(1));
        }

        [Test]
        public void ResultRejectsInvalidStarAward()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new CampaignResult(0, 4, 0, 0, 0));
        }

        [Test]
        public void ImmutableResultRoundTripsThroughOriginalVersionOneWireShape()
        {
            CampaignResult result = new CampaignResult(0, 3, 1, 0, 2);
            CampaignProgress data = new CampaignProgress
            {
                version = 1,
                currentLevel = 1,
                completedLevels = new[] { 0 },
                bestResults = new[]
                {
                    new CampaignResultData
                    {
                        levelIndex = result.LevelIndex,
                        stars = result.Stars,
                        cctvUses = result.CctvUses,
                        emergencyResets = result.EmergencyResets,
                        lostSeats = result.LostSeats
                    }
                }
            };
            string json = JsonUtility.ToJson(data);
            Assert.That(json, Does.Contain("\"levelIndex\":0"));
            Assert.That(json, Does.Contain("\"stars\":3"));
            Assert.That(CampaignProgress.TryDecode(json, 15, out CampaignProgress decoded), Is.True);
            CampaignResultData restored = decoded.bestResults[0];
            CampaignResult readout = new CampaignResult(restored.levelIndex, restored.stars,
                restored.cctvUses, restored.emergencyResets, restored.lostSeats);
            Assert.That(readout.LevelIndex, Is.EqualTo(0));
            Assert.That(readout.Stars, Is.EqualTo(3));
            Assert.That(readout.CctvUses, Is.EqualTo(1));
            Assert.That(readout.EmergencyResets, Is.EqualTo(0));
            Assert.That(readout.LostSeats, Is.EqualTo(2));
        }

        [TestCase("null")]
        [TestCase("{\"levelIndex\":0,\"stars\":0}")]
        [TestCase("{\"levelIndex\":0,\"stars\":4}")]
        [TestCase("{\"levelIndex\":0,\"stars\":3,\"cctvUses\":-1}")]
        [TestCase("{\"levelIndex\":0,\"stars\":3,\"emergencyResets\":-1}")]
        [TestCase("{\"levelIndex\":0,\"stars\":3,\"lostSeats\":-1}")]
        [TestCase("{\"levelIndex\":1,\"stars\":3}")]
        [TestCase("{\"levelIndex\":0,\"stars\":3},{\"levelIndex\":0,\"stars\":3}")]
        public void DecodeRejectsInvalidRawResultBeforeConstructingRuntimeValue(string rawResult)
        {
            string json = "{\"version\":1,\"currentLevel\":1,\"completedLevels\":[0],\"bestResults\":[" +
                rawResult + "]}";
            Assert.That(CampaignProgress.TryDecode(json, 15, out CampaignProgress progress), Is.False);
            Assert.That(progress, Is.Null);
        }

        [Test]
        public void CompletionIsIdempotentAndEditorSessionsDoNotLeakProgress()
        {
            GameObject firstObject = new GameObject("PlatformFirstTest");
            GameObject secondObject = new GameObject("PlatformSecondTest");

            try
            {
                YandexPlatform first = firstObject.AddComponent<YandexPlatform>();
                first.Initialize(15);
                first.RecordCompletedLevel(new CampaignResult(4, 3, 1, 0, 0));
                first.RecordCompletedLevel(new CampaignResult(4, 3, 1, 0, 0));
                Assert.That(first.ResumeLevelIndex, Is.EqualTo(5));
                first.SetCurrentLevel(4);
                first.RecordCompletedLevel(new CampaignResult(4, 3, 1, 0, 0));
                Assert.That(first.ResumeLevelIndex, Is.EqualTo(4));
                YandexPlatform second = secondObject.AddComponent<YandexPlatform>();
                second.Initialize(15);
                Assert.That(second.ResumeLevelIndex, Is.EqualTo(0));
                Assert.That(second.StorageStatus, Is.EqualTo("editor-in-memory"));
            }
            finally
            {
                Object.DestroyImmediate(firstObject);
                Object.DestroyImmediate(secondObject);
            }
        }

        [Test]
        public void PauseAndAdCallbackDuplicatesDoNotRepeatTransitions()
        {
            GameObject platformObject = new GameObject("PlatformCallbacksTest");

            try
            {
                YandexPlatform platform = platformObject.AddComponent<YandexPlatform>();
                int pauseChanges = 0;
                int transitions = 0;
                platform.PauseChanged += () => pauseChanges++;
                platform.Initialize(15);
                platform.ReceivePause("1");
                platform.ReceivePause("1");
                platform.ReceivePause("0");
                Assert.That(pauseChanges, Is.EqualTo(2));
                platform.RecordCompletedLevel(new CampaignResult(0, 3, 0, 0, 0));
                platform.RecordCompletedLevel(new CampaignResult(1, 3, 0, 0, 0));
                platform.RecordCompletedLevel(new CampaignResult(2, 3, 0, 0, 0));
                platform.ShowTransitionAd(() => transitions++);
                platform.ReceiveAdResult("closed");
                platform.ReceiveAdResult("error");
                Assert.That(transitions, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(platformObject);
            }
        }
    }
}
