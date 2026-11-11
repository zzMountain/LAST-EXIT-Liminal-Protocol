using System;
using System.Collections;
using System.Collections.Generic;
using LastExit.Domain;
using LastExit.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LastExit.Tests.Gameplay.PlayMode
{
    public class BoardingFeedbackPlayModeTests
    {
        private const string FirstPassenger = "L01-A-01";
        private const string FirstBoardTrace = "Board:" + FirstPassenger;
        private const float TimeoutSeconds = 30;
        private LevelComposition _composition;
        private LevelBoardView _board;

        [UnitySetUp]
        public IEnumerator LoadPlayableScene()
        {
            yield return SceneManager.LoadSceneAsync("StationL07", LoadSceneMode.Single);
            _composition = GetSceneComponent<LevelComposition>();
            _board = GetSceneComponent<LevelBoardView>();
            _composition.SendMessage("OnApplicationFocus", true);
            _composition.SendMessage("OnApplicationPause", false);
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            if (_composition != null && _composition.Game != null)
            {
                _composition.SendMessage("OnApplicationFocus", true);
                _composition.SendMessage("OnApplicationPause", false);
                _composition.Restart();
            }
        }

        [UnityTest]
        public IEnumerator PassengerWalksToDoorThenHopsBeforeBoardingCommits()
        {
            Transform passenger = _board.GetPassenger(FirstPassenger);
            PassengerBoardingFeedback feedback = passenger.GetComponent<PassengerBoardingFeedback>();
            LineRenderer ring = GetRing(passenger);
            Assert.IsNotNull(feedback);
            Assert.IsFalse(feedback.IsPlaying);
            Assert.IsFalse(ring.enabled);
            Assert.IsTrue(_composition.Game.TryMove("R1", "L01-R1", 1));
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            bool sawLift = false;
            bool sawRing = false;

            while (CountTrace(FirstBoardTrace) == 0 && Time.realtimeSinceStartup < deadline)
            {
                Assert.IsTrue(passenger.gameObject.activeSelf);

                if (feedback.IsPlaying)
                {
                    Vector3 door = _board.GetDoor("L01-R1");
                    Assert.That(passenger.position.x, Is.EqualTo(door.x).Within(.002f));
                    Assert.That(passenger.position.z, Is.EqualTo(door.z).Within(.002f));
                    Assert.IsFalse(passenger.GetComponent<PassengerLocomotion>().IsMoving);
                    sawLift |= passenger.position.y > .02f;
                    sawRing |= ring.enabled;
                }
                else
                {
                    Assert.That(passenger.position.y, Is.EqualTo(0).Within(.002f),
                        "Walking passengers must remain on the floor before reaching the door.");
                }

                yield return null;
            }

            Assert.IsTrue(sawLift, "The passenger must visibly hop before the boarding commit.");
            Assert.IsTrue(sawRing, "The boarding ring must be visible before the passenger disappears.");
            Assert.AreEqual(1, CountTrace(FirstBoardTrace));
            Assert.That(_composition.Game.Snapshot.Rows[0].Bus.Occupied, Is.GreaterThanOrEqualTo(1));
        }

        [UnityTest]
        public IEnumerator FocusPauseFreezesHopAndRingThenResumesOneBoardingCommit()
        {
            Transform passenger = _board.GetPassenger(FirstPassenger);
            PassengerBoardingFeedback feedback = passenger.GetComponent<PassengerBoardingFeedback>();
            LineRenderer ring = GetRing(passenger);
            Assert.IsTrue(_composition.Game.TryMove("R1", "L01-R1", 1));
            yield return Await(() => feedback.IsPlaying && passenger.position.y > .02f);
            _composition.SendMessage("OnApplicationFocus", false);
            Vector3 pausedPosition = passenger.position;
            Vector3[] pausedRing = new Vector3[ring.positionCount];
            ring.GetPositions(pausedRing);
            Color pausedColor = ring.startColor;
            float pausedWidth = ring.startWidth;
            int traceCount = _composition.Game.Trace.Count;
            yield return new WaitForSeconds(.35f);
            Assert.IsTrue(_composition.Game.IsPaused);
            Assert.IsTrue(feedback.IsPlaying);
            Assert.AreEqual(pausedPosition, passenger.position);
            Vector3[] currentRing = new Vector3[ring.positionCount];
            ring.GetPositions(currentRing);
            CollectionAssert.AreEqual(pausedRing, currentRing);
            Assert.AreEqual(pausedColor, ring.startColor);
            Assert.AreEqual(pausedWidth, ring.startWidth);
            Assert.IsTrue(ring.enabled);
            Assert.AreEqual(traceCount, _composition.Game.Trace.Count);
            Assert.AreEqual(0, CountTrace(FirstBoardTrace));
            _composition.SendMessage("OnApplicationFocus", true);
            yield return Await(() => CountTrace(FirstBoardTrace) == 1);
            yield return new WaitForSeconds(.35f);
            Assert.AreEqual(1, CountTrace(FirstBoardTrace));
        }

        [UnityTest]
        public IEnumerator RetryDuringHopCancelsOldCallbacksAndCreatesNeutralFloorPassengers()
        {
            Transform oldPassenger = _board.GetPassenger(FirstPassenger);
            PassengerBoardingFeedback oldFeedback = oldPassenger.GetComponent<PassengerBoardingFeedback>();
            Assert.IsTrue(_composition.Game.TryMove("R1", "L01-R1", 1));
            yield return Await(() => oldFeedback.IsPlaying && oldPassenger.position.y > .02f);
            List<BoardStep> oldSteps = new List<BoardStep>(_composition.Game.BoardingSteps);
            int oldGeneration = _composition.Game.Snapshot.Generation;
            _composition.Restart();

            foreach (BoardStep oldStep in oldSteps)
            {
                _composition.Game.Complete(oldStep);
            }

            yield return new WaitForSeconds(3);
            Assert.That(_composition.Game.Snapshot.Generation, Is.GreaterThan(oldGeneration));
            Assert.AreEqual(0, _composition.Game.Trace.Count);
            Assert.AreEqual(0, _composition.Game.BoardingSteps.Count);
            Assert.AreEqual(0, _composition.Game.Snapshot.Rows[0].Bus.Occupied);
            Assert.AreEqual(3, _composition.Game.Snapshot.Rows[0].Bus.LeftX);

            foreach (QueueSnapshot queue in _composition.Game.Snapshot.Queues)
            {
                foreach (PassengerDefinition definition in queue.RemainingPassengers)
                {
                    Transform passenger = _board.GetPassenger(definition.Id);
                    Assert.IsTrue(passenger.gameObject.activeSelf);
                    Assert.That(passenger.position.y, Is.EqualTo(0).Within(.002f));
                    Assert.IsFalse(passenger.GetComponent<PassengerBoardingFeedback>().IsPlaying);
                    Assert.IsFalse(GetRing(passenger).enabled);
                }
            }
        }

        [UnityTest]
        public IEnumerator BoardingRingDoesNotExpandPhysicalBoundsOrStopAnotherLane()
        {
            Transform first = _board.GetPassenger(FirstPassenger);
            Transform second = _board.GetPassenger("L01-B-01");
            PassengerBoardingFeedback feedback = first.GetComponent<PassengerBoardingFeedback>();
            Assert.IsTrue(_composition.Game.TryMove("R1", "L01-R1", 1));
            yield return Await(() => feedback.IsPlaying && first.position.y > .02f);
            Assert.IsTrue(GetRing(first).enabled);
            Assert.IsTrue(second.GetComponent<PassengerLocomotion>().IsMoving);
            Bounds physical = GetPhysicalBounds(first);
            Bounds selectable = _board.GetPassengerBounds(FirstPassenger);
            Assert.That(Vector3.Distance(physical.center, selectable.center), Is.LessThan(.002f));
            Assert.That(Vector3.Distance(physical.size, selectable.size), Is.LessThan(.002f),
                "The floor ring must not become part of passenger bounds used by the HUD and inspection.");
            Vector3 secondPosition = second.position;
            yield return new WaitForSeconds(.05f);
            Assert.That(Vector3.Distance(secondPosition, second.position), Is.GreaterThan(.01f));
            yield return Await(() => CountTrace(FirstBoardTrace) == 1);
            Assert.AreEqual(0, CountTrace("Board:L01-B-01"));
            Assert.IsTrue(second.GetComponent<PassengerLocomotion>().IsMoving);
            yield return Await(() => CountTrace("Board:L01-B-01") == 1);
            Assert.AreEqual(1, CountTrace(FirstBoardTrace));
        }

        private static T GetSceneComponent<T>() where T : Component
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);

                if (component != null)
                {
                    return component;
                }
            }

            Assert.Fail("Missing scene component " + typeof(T).Name);
            return null;
        }

        private static LineRenderer GetRing(Transform passenger)
        {
            LineRenderer ring = passenger.GetComponentInChildren<LineRenderer>(true);
            Assert.IsNotNull(ring, "The shared passenger template needs a serialized boarding ring.");
            return ring;
        }

        private static Bounds GetPhysicalBounds(Transform passenger)
        {
            List<Renderer> physical = new List<Renderer>();

            foreach (Renderer renderer in passenger.GetComponentsInChildren<Renderer>())
            {
                if ((renderer is LineRenderer) == false)
                {
                    physical.Add(renderer);
                }
            }

            Assert.That(physical.Count, Is.GreaterThan(0));
            Bounds bounds = physical[0].bounds;

            for (int index = 1; index < physical.Count; index++)
            {
                bounds.Encapsulate(physical[index].bounds);
            }

            return bounds;
        }

        private IEnumerator Await(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;

            while (condition() == false && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.IsTrue(condition(), "Timed out waiting for boarding feedback. Trace=" +
                string.Join(",", _composition.Game.Trace));
        }

        private int CountTrace(string prefix)
        {
            int count = 0;

            foreach (string entry in _composition.Game.Trace)
            {
                if (entry.StartsWith(prefix, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
