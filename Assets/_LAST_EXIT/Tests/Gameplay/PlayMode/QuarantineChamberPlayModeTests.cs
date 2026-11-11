using System;
using System.Collections;
using System.Linq;
using LastExit.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LastExit.Tests.Gameplay.PlayMode
{
    public class QuarantineChamberPlayModeTests
    {
        private LevelComposition _composition;
        private QuarantineChamber _chamber;
        private LevelBoardView _board;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync("StationL07");
            _composition = GetComponent<LevelComposition>();
            _chamber = GetComponent<QuarantineChamber>();
            _board = GetComponent<LevelBoardView>();
            _composition.SendMessage("OnApplicationFocus", true);
            _composition.SendMessage("OnApplicationPause", false);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DoorRemainsClosedWithoutCommandAndHumanResultCommitsAfterClose()
        {
            Assert.AreEqual("Closed", _chamber.Phase);
            Transform leaf = GetLeaf();
            Assert.AreEqual(Vector3.zero, leaf.localPosition);
            yield return new WaitForSeconds(.5f);
            Assert.AreEqual("Closed", _chamber.Phase);
            int stability = _composition.Game.Snapshot.Stability;
            int heads = _composition.Game.Snapshot.Queues[0].RemainingPassengers.Count;
            _composition.Game.Quarantine("A");
            yield return Await(() => _chamber.Phase == "Threshold");
            Assert.That(Vector3.Distance(_board.GetPassenger("L01-A-01").position,
                _chamber.ThresholdPosition), Is.LessThan(.01f));
            Assert.AreEqual(Vector3.zero, leaf.localPosition);
            Assert.AreEqual(stability, _composition.Game.Snapshot.Stability);
            Assert.AreEqual(heads, _composition.Game.Snapshot.Queues[0].RemainingPassengers.Count);
            yield return Await(() => _chamber.Phase == "Opening");
            float openedAt = Time.time;
            yield return Await(() => _chamber.Phase == "Entering");
            Assert.That(leaf.localPosition.x, Is.EqualTo(1.18f).Within(.001f));
            Assert.That(Time.time - openedAt, Is.InRange(.27f, .42f));
            yield return Await(() => _chamber.Phase == "Incinerating");
            Assert.That(_board.GetPassenger("L01-A-01").position.z,
                Is.GreaterThan(_chamber.ThresholdPosition.z + 1));
            float effectAt = Time.time;
            Assert.AreEqual(stability, _composition.Game.Snapshot.Stability);
            yield return Await(() => _chamber.Phase == "Closing");
            Assert.That(Time.time - effectAt, Is.InRange(.57f, .72f));
            Assert.AreEqual(stability, _composition.Game.Snapshot.Stability);
            yield return Await(() => _chamber.Phase == "Closed" && _composition.Game.PendingStep == null);
            Assert.AreEqual(Vector3.zero, leaf.localPosition);
            Assert.AreEqual(stability - 1, _composition.Game.Snapshot.Stability);
            Assert.AreEqual(heads - 1, _composition.Game.Snapshot.Queues[0].RemainingPassengers.Count);
            Assert.AreEqual(1, _composition.Game.Snapshot.LostSeats);
            Assert.AreEqual(0, _composition.Game.Snapshot.Rows[0].Bus.Occupied);
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator EveryChamberPhaseFreezesOnFocusLossAndRetryClearsIt()
        {
            string[] phases = { "Approach", "Threshold", "Opening", "Entering", "Incinerating", "Closing" };

            foreach (string phase in phases)
            {
                _composition.Restart();
                Vector3 origin = _board.GetPassenger("L01-A-01").position;
                _composition.Game.Quarantine("A");
                yield return Await(() => _chamber.Phase == phase);
                _composition.SendMessage("OnApplicationFocus", false);
                float progress = _chamber.Progress;
                Vector3 leafPosition = GetLeaf().localPosition;
                Light flash = _chamber.GetComponentInChildren<Light>();
                float intensity = flash.intensity;
                ParticleSystem[] particles = _chamber.GetComponentsInChildren<ParticleSystem>();
                float[] particleTimes = particles.Select(item => item.time).ToArray();
                Vector3 passenger = _board.GetPassenger("L01-A-01").position;
                yield return new WaitForSeconds(.15f);
                Assert.AreEqual(phase, _chamber.Phase);
                Assert.AreEqual(progress, _chamber.Progress);
                Assert.AreEqual(leafPosition, GetLeaf().localPosition);
                Assert.AreEqual(intensity, flash.intensity);
                CollectionAssert.AreEqual(particleTimes, particles.Select(item => item.time).ToArray());
                Assert.AreEqual(passenger, _board.GetPassenger("L01-A-01").position);
                GetComponent<LevelSequencePlayer>().Cancel();
                Assert.AreEqual("Closed", _chamber.Phase);
                Assert.AreEqual(origin, _board.GetPassenger("L01-A-01").position);
                Assert.IsTrue(_board.GetPassenger("L01-A-01").gameObject.activeSelf);
                int generation = _composition.Game.Snapshot.Generation;
                _composition.Restart();
                Assert.AreEqual("Closed", _chamber.Phase);
                Assert.Greater(_composition.Game.Snapshot.Generation, generation);
                Assert.AreEqual(0, _composition.Game.Snapshot.LostSeats);
                Assert.AreEqual(4, _composition.Game.Snapshot.Queues[0].RemainingPassengers.Count);
                Assert.IsTrue(_board.GetPassenger("L01-A-01").gameObject.activeSelf);
                _composition.SendMessage("OnApplicationFocus", true);
                yield return null;
            }

            _composition.Game.Quarantine("A");
            yield return Await(() => _composition.Game.PendingStep == null);
            Assert.AreEqual(1, _composition.Game.Snapshot.LostSeats);
            _composition.Game.Quarantine("B");
            yield return Await(() => _composition.Game.PendingStep == null);
            Assert.AreEqual(2, _composition.Game.Snapshot.LostSeats);
            Assert.AreEqual("Closed", _chamber.Phase);
        }

        [UnityTest]
        public IEnumerator PauseOnLastTickCannotAdvanceStageOrCommitResult()
        {
            foreach (string phase in new[] { "Opening", "Incinerating", "Closing" })
            {
                _composition.Restart();
                _composition.Game.Quarantine("A");
                yield return Await(() => _chamber.Phase == phase && _chamber.Progress >= 1);
                _composition.SendMessage("OnApplicationFocus", false);
                int heads = _composition.Game.Snapshot.Queues[0].RemainingPassengers.Count;
                yield return new WaitForSeconds(.1f);
                Assert.AreEqual(phase, _chamber.Phase);
                Assert.AreEqual(heads, _composition.Game.Snapshot.Queues[0].RemainingPassengers.Count);
                _composition.Restart();
                _composition.SendMessage("OnApplicationFocus", true);
                yield return null;
            }
        }

        private Transform GetLeaf()
        {
            return _chamber.GetComponentsInChildren<Transform>()
                .Single(item => item.name == "LE_Q3_DOOR_PANEL");
        }

        private static T GetComponent<T>() where T : Component
        {
            return SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
        }

        private static IEnumerator Await(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 25;

            while (condition() == false && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.IsTrue(condition(), "Quarantine transition timed out.");
        }
    }
}
