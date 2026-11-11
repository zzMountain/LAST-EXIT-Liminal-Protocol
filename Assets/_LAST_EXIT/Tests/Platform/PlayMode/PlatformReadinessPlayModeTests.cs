using System.Collections;
using LastExit.Platform;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace LastExit.Tests.Platform.PlayMode
{
    public class PlatformReadinessPlayModeTests
    {
        private GameObject _owner;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_owner != null)
            {
                Object.Destroy(_owner);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator SceneReadyWaitsForAnUnobscuredFrameAndIsIdempotent()
        {
            YandexPlatform platform = CreatePlatform();
            platform.MarkSceneReady();
            platform.MarkSceneReady();
            Assert.That(platform.IsSceneReady, Is.False);
            Assert.That(platform.IsPaused, Is.True);

            while (SplashScreen.isFinished == false)
            {
                yield return null;
            }

            yield return null;
            yield return null;
            Assert.That(platform.IsSceneReady, Is.True);
            Assert.That(platform.IsPaused, Is.False);
            platform.MarkSceneReady();
            Assert.That(platform.IsSceneReady, Is.True);
        }

        [UnityTest]
        public IEnumerator ComponentDisableCancelsPendingReadinessAndEnableRetries()
        {
            YandexPlatform platform = CreatePlatform();
            platform.MarkSceneReady();
            platform.enabled = false;
            yield return null;
            yield return null;
            Assert.That(platform.IsSceneReady, Is.False);
            Assert.That(platform.IsPaused, Is.True);
            platform.enabled = true;
            yield return null;
            yield return null;
            Assert.That(platform.IsSceneReady, Is.True);
            Assert.That(platform.IsPaused, Is.False);
        }

        [UnityTest]
        public IEnumerator ExplicitCancellationRequiresANewSceneRequestAfterEnable()
        {
            YandexPlatform platform = CreatePlatform();
            platform.MarkSceneReady();
            platform.CancelSceneReadiness();
            platform.enabled = false;
            platform.enabled = true;
            yield return null;
            yield return null;
            Assert.That(platform.IsSceneReady, Is.False);
            Assert.That(platform.IsPaused, Is.True);
            platform.MarkSceneReady();
            yield return null;
            yield return null;
            Assert.That(platform.IsSceneReady, Is.True);
            Assert.That(platform.IsPaused, Is.False);
        }

        [UnityTest]
        public IEnumerator CancellingAnAlreadyReadySceneDoesNotRevokeTheIrreversibleSignal()
        {
            YandexPlatform platform = CreatePlatform();
            int pauseChanges = 0;
            platform.PauseChanged += () => pauseChanges++;
            platform.MarkSceneReady();
            yield return null;
            yield return null;
            Assert.That(platform.IsSceneReady, Is.True);
            platform.CancelSceneReadiness();
            platform.MarkSceneReady();
            yield return null;
            Assert.That(platform.IsSceneReady, Is.True);
            Assert.That(platform.IsPaused, Is.False);
            Assert.That(pauseChanges, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator FinishingLoadingDoesNotReleaseExternalPause()
        {
            YandexPlatform platform = CreatePlatform();
            platform.ReceivePause("1");
            platform.MarkSceneReady();
            yield return null;
            yield return null;
            Assert.That(platform.IsSceneReady, Is.True);
            Assert.That(platform.IsPaused, Is.True);
            platform.ReceivePause("0");
            Assert.That(platform.IsPaused, Is.False);
        }

        [UnityTest]
        public IEnumerator GameObjectDeactivatePreservesRequestWithoutCompletingWhileInactive()
        {
            YandexPlatform platform = CreatePlatform();
            platform.MarkSceneReady();
            _owner.SetActive(false);
            yield return null;
            yield return null;
            Assert.That(platform.IsSceneReady, Is.False);
            _owner.SetActive(true);
            yield return null;
            yield return null;
            Assert.That(platform.IsSceneReady, Is.True);
        }

        [UnityTest]
        public IEnumerator DestroyCancelsPendingReadinessWithoutLaterCallbacks()
        {
            YandexPlatform platform = CreatePlatform();
            platform.MarkSceneReady();
            Object.Destroy(_owner);
            yield return null;
            yield return null;
            Assert.That(platform == null, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        private YandexPlatform CreatePlatform()
        {
            _owner = new GameObject("PlatformReadinessTest");
            YandexPlatform platform = _owner.AddComponent<YandexPlatform>();
            platform.Initialize(15);
            return platform;
        }
    }
}
