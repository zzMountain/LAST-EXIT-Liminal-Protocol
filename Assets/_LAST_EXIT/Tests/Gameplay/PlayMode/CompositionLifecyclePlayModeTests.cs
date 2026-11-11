using System.Collections;
using System.Reflection;
using LastExit.Platform;
using LastExit.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LastExit.Tests.Gameplay.PlayMode
{
    public class CompositionLifecyclePlayModeTests
    {
        private const string Progress = "{\"version\":1,\"currentLevel\":12,\"completedLevels\":[],\"bestResults\":[]}";
        private GameObject _owner;
        private LevelComposition _composition;
        private YandexPlatform _platform;

        [UnitySetUp]
        public IEnumerator LoadPendingComposition()
        {
            yield return SceneManager.LoadSceneAsync("StationL07");
            LevelComposition source = null;

            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                LevelComposition candidate = root.GetComponentInChildren<LevelComposition>(true);

                if (candidate != null)
                {
                    source = candidate;
                    break;
                }
            }

            Assert.That(source, Is.Not.Null);
            source.gameObject.SetActive(false);
            _owner = Object.Instantiate(source.gameObject);
            _owner.name = "PendingCompositionLifecycleTest";
            _composition = _owner.GetComponent<LevelComposition>();
            _platform = _owner.GetComponent<YandexPlatform>();
            Assert.That(_platform, Is.Not.Null);
            // Simulate the real WebGL interval before the asynchronous SDK progress callback.
            // Editor Initialize normally supplies progress synchronously.
            SetField(_platform, "_initialized", true);
            SetField(_platform, "_levelCount", 15);
            _owner.SetActive(true);
            Assert.That(_composition.Game, Is.Null);
        }

        [UnityTearDown]
        public IEnumerator DestroyOwner()
        {
            if (_owner != null)
            {
                Object.Destroy(_owner);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator ProgressWhileCompositionDisabledWaitsForEnableBeforeCreatingAndReadyingScene()
        {
            _composition.enabled = false;
            _platform.ReceiveProgress(Progress);
            yield return null;
            yield return null;
            Assert.That(_composition.Game, Is.Null);
            Assert.That(_platform.IsSceneReady, Is.False);
            _composition.enabled = true;
            yield return null;
            yield return null;
            Assert.That(_composition.Game.LevelId, Is.EqualTo("LEVEL-13"));
            Assert.That(_platform.IsSceneReady, Is.True);
            Assert.That(_composition.Game.IsPaused, Is.False);
        }

        [UnityTest]
        public IEnumerator DisableCancelsPendingSceneReadyAndPausesUntilEnabled()
        {
            _platform.ReceiveProgress(Progress);
            _composition.enabled = false;
            yield return null;
            yield return null;
            Assert.That(_platform.IsSceneReady, Is.False);
            Assert.That(_composition.Game.IsPaused, Is.True);
            _composition.enabled = true;
            yield return null;
            yield return null;
            Assert.That(_platform.IsSceneReady, Is.True);
            Assert.That(_composition.Game.IsPaused, Is.False);
        }

        [UnityTest]
        public IEnumerator DestroyedCompositionDoesNotConsumeLateProgressOrMarkSceneReady()
        {
            Object.Destroy(_composition);
            yield return null;
            _platform.ReceiveProgress(Progress);
            yield return null;
            yield return null;
            Assert.That(_platform.IsProgressReady, Is.True);
            Assert.That(_platform.IsSceneReady, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        // Insert these members into CompositionLifecyclePlayModeTests.
        // Uses the existing real serialized YandexPlatform and SetField helper.
        // Editor ads finish synchronously. Only the accepted/pending WebGL callback checkpoint
        // is staged here; these tests do not claim to exercise victory gating or the SDK.

        [UnityTest]
        public IEnumerator PendingAdSurvivesCompositionReenableUntilItsTerminalCallback()
        {
            _platform.ReceiveProgress(Progress);
            LevelGameSession pendingAttempt = _composition.Game;
            Assert.That(pendingAttempt.LevelId, Is.EqualTo("LEVEL-13"));
            int pendingGeneration = pendingAttempt.Snapshot.Generation;
            StagePendingTransitionAd();

            _composition.enabled = false;
            _composition.enabled = true;
            Assert.That(_composition.Game, Is.SameAs(pendingAttempt));
            Assert.That(_composition.Game.Snapshot.Generation, Is.EqualTo(pendingGeneration),
                "Re-enabling during an accepted ad transition must not restart the old attempt.");
            yield return WaitForAdTestSceneReadiness();
            Assert.That(_composition.Game, Is.SameAs(pendingAttempt));
            Assert.That(_composition.Game.Snapshot.Generation, Is.EqualTo(pendingGeneration));
            Assert.That(_composition.Game.IsPaused, Is.True,
                "The accepted transition stays paused until its terminal ad callback.");

            _platform.ReceiveAdResult("closed");
            LevelGameSession nextAttempt = _composition.Game;
            Assert.That(nextAttempt, Is.Not.SameAs(pendingAttempt));
            Assert.That(nextAttempt.LevelId, Is.EqualTo("LEVEL-14"));
            Assert.That(_platform.ResumeLevelIndex, Is.EqualTo(13));
            int nextGeneration = nextAttempt.Snapshot.Generation;
            _platform.ReceiveAdResult("closed");
            yield return null;
            yield return null;
            Assert.That(_composition.Game, Is.SameAs(nextAttempt));
            Assert.That(_composition.Game.Snapshot.Generation, Is.EqualTo(nextGeneration));
            Assert.That(_composition.Game.LevelId, Is.EqualTo("LEVEL-14"));
            Assert.That(_platform.ResumeLevelIndex, Is.EqualTo(13),
                "Duplicate terminal callbacks must not advance the campaign a second time.");
            Assert.That(_composition.Game.IsPaused, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AdTerminalWhileCompositionDisabledCreatesNextAttemptWithoutEnableRestart()
        {
            _platform.ReceiveProgress(Progress);
            LevelGameSession pendingAttempt = _composition.Game;
            Assert.That(pendingAttempt.LevelId, Is.EqualTo("LEVEL-13"));
            StagePendingTransitionAd();
            _composition.enabled = false;

            _platform.ReceiveAdResult("closed");
            LevelGameSession nextAttempt = _composition.Game;
            Assert.That(nextAttempt, Is.Not.SameAs(pendingAttempt));
            Assert.That(nextAttempt.LevelId, Is.EqualTo("LEVEL-14"));
            Assert.That(nextAttempt.IsPaused, Is.True);
            Assert.That(_platform.ResumeLevelIndex, Is.EqualTo(13));
            int nextGeneration = nextAttempt.Snapshot.Generation;
            _platform.ReceiveAdResult("closed");
            Assert.That(_composition.Game, Is.SameAs(nextAttempt));
            Assert.That(_platform.ResumeLevelIndex, Is.EqualTo(13));

            _composition.enabled = true;
            Assert.That(_composition.Game, Is.SameAs(nextAttempt));
            Assert.That(_composition.Game.Snapshot.Generation, Is.EqualTo(nextGeneration),
                "The completed transition must clear the restart debt before re-enable.");
            yield return WaitForAdTestSceneReadiness();
            Assert.That(_composition.Game, Is.SameAs(nextAttempt));
            Assert.That(_composition.Game.Snapshot.Generation, Is.EqualTo(nextGeneration));
            Assert.That(_composition.Game.LevelId, Is.EqualTo("LEVEL-14"));
            Assert.That(_platform.ResumeLevelIndex, Is.EqualTo(13));
            Assert.That(_composition.Game.IsPaused, Is.False);
            _platform.ReceiveAdResult("closed");
            yield return null;
            Assert.That(_composition.Game, Is.SameAs(nextAttempt));
            Assert.That(_composition.Game.Snapshot.Generation, Is.EqualTo(nextGeneration));
            Assert.That(_platform.ResumeLevelIndex, Is.EqualTo(13));
            LogAssert.NoUnexpectedReceived();
        }

        private void StagePendingTransitionAd()
        {
            const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo platformOwner = typeof(LevelComposition).GetField("_campaignPlatform", Fields);
            FieldInfo transitioning = typeof(LevelComposition).GetField("_transitioning", Fields);
            MethodInfo terminal = typeof(LevelComposition).GetMethod("CompleteNextLevel", Fields);
            Assert.That(platformOwner, Is.Not.Null);
            Assert.That(transitioning, Is.Not.Null);
            Assert.That(terminal, Is.Not.Null);
            Assert.That(platformOwner.GetValue(_composition), Is.SameAs(_platform),
                "Keep the real serialized platform as the composition's campaign owner.");
            System.Action completed = (System.Action)System.Delegate.CreateDelegate(
                typeof(System.Action), _composition, terminal);
            transitioning.SetValue(_composition, true);
            SetField(_platform, "_adPending", true);
            SetField(_platform, "_transitionCompleted", completed);
        }

        private IEnumerator WaitForAdTestSceneReadiness()
        {
            const float TimeoutSeconds = 5;
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;

            while (_platform.IsSceneReady == false && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(_platform.IsSceneReady, Is.True,
                "Timed out waiting for the real platform readiness coroutine.");
        }

        private static void SetField(YandexPlatform platform, string name, object value)
        {
            FieldInfo field = typeof(YandexPlatform).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(platform, value);
        }
    }
}
