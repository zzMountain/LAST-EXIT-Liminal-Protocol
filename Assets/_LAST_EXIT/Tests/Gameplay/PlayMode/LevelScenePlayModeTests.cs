using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using LastExit.ArtLab.Preview;
using LastExit.Domain;
using LastExit.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace LastExit.Tests.Gameplay.PlayMode
{
    public class LevelScenePlayModeTests : InputTestFixture
    {
        private const float AnimationTimeoutSeconds = 60;
        private LevelComposition _composition;
        private LevelBoardView _board;
        private LevelHud _hud;
        private PreviewPointerInput _pointer;
        private Camera _camera;
        private Mouse _mouse;
        private Touchscreen _touch;
        private RenderTexture _viewport;
        private BoardStep _pending;
        private TextAsset _fixtureContent;

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            _pending = null;
            _mouse = InputSystem.AddDevice<Mouse>("Level01TestMouse");
            _touch = InputSystem.AddDevice<Touchscreen>("Level01TestTouch");
        }

        [UnitySetUp]
        public IEnumerator LoadPlayableScene()
        {
            yield return SceneManager.LoadSceneAsync("StationL07", LoadSceneMode.Single);
            _composition = GetSceneComponent<LevelComposition>();
            _board = GetSceneComponent<LevelBoardView>();
            _hud = GetSceneComponent<LevelHud>();
            _pointer = GetSceneComponent<PreviewPointerInput>();
            _camera = GetSceneComponent<Camera>();
            _composition.SendMessage("OnApplicationFocus", true);
            _composition.SendMessage("OnApplicationPause", false);
            _composition.Game.StepRequested += OnStepRequested;
            SetViewport(1600, 900);
            yield return null;
            Assert.AreEqual(2, _composition.Game.Snapshot.Rows.Count);
            Assert.AreEqual("0/2", GetOccupancy("L01-R1").text);
            Assert.AreEqual("4", GetText("Queue A").text);

            foreach (RowSnapshot row in _composition.Game.Snapshot.Rows)
            {
                BusSnapshot bus = row.Bus;
                Bounds bounds = _board.GetBusBounds(bus.Id);
                float expectedMin = _board.GetWorldX(bus.LeftX) - _board.CellSize * .5f;
                float expectedMax = _board.GetWorldX(bus.LeftX + bus.FootprintCells) - _board.CellSize * .5f;
                Assert.That(bounds.min.x, Is.EqualTo(expectedMin).Within(.02f), bus.Id + " left body edge");
                Assert.That(bounds.max.x, Is.EqualTo(expectedMax).Within(.02f), bus.Id + " right body edge");
                Assert.That(_board.GetDoor(bus.Id).x,
                    Is.EqualTo(_board.GetWorldX(bus.LeftX + 1)).Within(.02f), bus.Id + " door alignment");
            }
        }

        [TearDown]
        public override void TearDown()
        {
            try
            {
                if (_composition != null && _composition.Game != null)
                {
                    _composition.Game.StepRequested -= OnStepRequested;
                    _composition.SendMessage("OnApplicationFocus", true);
                    _composition.SendMessage("OnApplicationPause", false);
                    _composition.Restart();
                }
            }
            finally
            {
                try
                {
                    if (_camera != null)
                    {
                        _camera.targetTexture = null;
                    }

                    if (_viewport != null)
                    {
                        _viewport.Release();
                        UnityEngine.Object.Destroy(_viewport);
                        _viewport = null;
                    }

                    if (_fixtureContent != null)
                    {
                        UnityEngine.Object.Destroy(_fixtureContent);
                    }
                }
                finally
                {
                    base.TearDown();
                }
            }
        }

        [UnityTest]
        public IEnumerator MouseAndTouchCanMoveAnotherBusWhilePassengerKeepsWalking()
        {
            ConfigureSpareBusFixture();

            foreach (bool touch in new[] { false, true })
            {
                SetViewport(touch ? 360 : 1600, touch ? 640 : 900);
                yield return null;
                Drag("L01-R1", 1, touch);
                yield return Await(() => _composition.Game.PendingStep != null &&
                    _composition.Game.PendingStep.Kind == BoardStepKind.Board);
                BoardStep walking = _composition.Game.PendingStep;
                Transform passenger = _board.GetPassenger(walking.PassengerId);
                Vector3 passengerPosition = passenger.position;
                int boardCount = CountTrace("Board:");
                Assert.IsFalse(_composition.Game.CanSelect(walking.BusId));
                Assert.IsTrue(_composition.Game.CanSelect("L01-T"));
                Drag("L01-T", 3, touch);
                Assert.AreSame(walking, _composition.Game.PendingStep);
                Assert.AreEqual(3, _composition.Game.Snapshot.Rows[1].Bus.LeftX);
                Assert.That(_board.GetBus("L01-T").position.x,
                    Is.EqualTo(_board.GetWorldX(3)).Within(.01f));
                Assert.AreEqual(passengerPosition, passenger.position,
                    "Dragging another bus must not reset or teleport the active passenger.");
                Assert.AreEqual(boardCount, CountTrace("Board:"));
                Assert.AreEqual(1, _composition.Game.BoardingSteps.Count,
                    "A safe drag may open B, but the current batch membership must remain unchanged.");
                Assert.AreEqual("A", _composition.Game.BoardingSteps[0].QueueId);
                yield return new WaitForSeconds(.1f);
                Assert.AreEqual(boardCount, CountTrace("Board:"));
                Assert.AreEqual(1, _composition.Game.BoardingSteps.Count,
                    "The newly available B head must wait for the next common beat.");
                yield return Await(() => CountTrace("Board:") > boardCount);
                Assert.That(_composition.Game.Trace, Does.Contain("Board:" + walking.PassengerId));
                Assert.AreEqual(3, _composition.Game.Snapshot.Rows[1].Bus.LeftX);
                Assert.AreEqual(2, _composition.Game.BoardingSteps.Count);
                Assert.AreEqual("L01-A-02", _composition.Game.BoardingSteps[0].PassengerId);
                Assert.AreEqual("L01-B-01", _composition.Game.BoardingSteps[1].PassengerId);
                _composition.Restart();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator BoardingCompletionCancelsHeldBusPreviewBeforeNextPassengerPath()
        {
            ConfigureSpareBusFixture();
            Drag("L01-R1", 1, false);
            yield return Await(() => _composition.Game.PendingStep != null &&
                _composition.Game.PendingStep.Kind == BoardStepKind.Board);
            BoardStep walking = _composition.Game.PendingStep;
            Vector3 origin = _board.GetBus("L01-T").position;
            Vector3 center = _board.GetBusBounds("L01-T").center;
            Vector2 start = Project(center);
            Vector2 end = Project(center + Vector3.right * _board.CellSize);
            QueueMouse(start, true);
            QueueMouse(end, true);
            Assert.That(Vector3.Distance(origin, _board.GetBus("L01-T").position), Is.GreaterThan(.1f));
            yield return Await(() => _composition.Game.PendingStep != walking);
            Assert.That(Vector3.Distance(origin, _board.GetBus("L01-T").position), Is.LessThan(.01f));
            Assert.IsNull(GetSceneComponent<LevelBusDrag>().SelectedBusId);
            QueueMouse(end, false);
            Assert.AreEqual(5, _composition.Game.Snapshot.Rows[1].Bus.LeftX);
            Assert.That(_composition.Game.Trace, Does.Not.Contain("Moved:L01-T:6"));
        }

        [UnityTest]
        public IEnumerator BoardingBatchWaitsForEveryWalkAndHopBeforeDispatchingNextHeads()
        {
            Transform firstA = _board.GetPassenger("L01-A-01");
            Transform firstB = _board.GetPassenger("L01-B-01");
            PassengerBoardingFeedback firstFeedback = firstA.GetComponent<PassengerBoardingFeedback>();
            PassengerBoardingFeedback lastFeedback = firstB.GetComponent<PassengerBoardingFeedback>();
            Vector3 originA = firstA.position;
            Vector3 originB = firstB.position;
            Drag("L01-R1", 1, false);
            yield return Await(() => _composition.Game.BoardingSteps.Count == 2);
            Assert.AreEqual("A", _composition.Game.BoardingSteps[0].QueueId);
            Assert.AreEqual("B", _composition.Game.BoardingSteps[1].QueueId);
            yield return new WaitForSeconds(.15f);
            Assert.AreEqual(0, CountTrace("Board:"), "Both batch heads must begin before either arrival.");
            Assert.That(Vector3.Distance(originA, firstA.position), Is.GreaterThan(.05f));
            Assert.That(Vector3.Distance(originB, firstB.position), Is.GreaterThan(.05f));
            Assert.IsTrue(firstA.GetComponent<PassengerLocomotion>().IsMoving);
            Assert.IsTrue(firstB.GetComponent<PassengerLocomotion>().IsMoving);
            Assert.IsFalse(_composition.Game.CanSelect("L01-R1"));
            Assert.IsFalse(_composition.Game.CanSelect("L01-T"));
            float deadline = Time.realtimeSinceStartup + AnimationTimeoutSeconds;
            bool sawFirstHop = false;

            while (CountTrace("Board:L01-A-01") == 0 && Time.realtimeSinceStartup < deadline)
            {
                sawFirstHop |= firstFeedback.IsPlaying;
                Vector3 previous = firstB.position;
                yield return null;
                Assert.That(Vector3.Distance(previous, firstB.position),
                    Is.LessThan(Time.deltaTime * 20 + .05f),
                    "Completing another lane must not teleport or reset the lower passenger.");
                Assert.That(firstB.position.z, Is.LessThanOrEqualTo(previous.z + .001f));
            }

            Assert.IsTrue(sawFirstHop, "The early batch head must finish its door hop before committing.");
            Assert.AreEqual(1, CountTrace("Board:L01-A-01"));
            Assert.AreEqual(0, CountTrace("Board:L01-B-01"), "The lower door has a longer real walking path.");
            Assert.IsTrue(firstB.GetComponent<PassengerLocomotion>().IsMoving);
            Assert.That(Vector3.Distance(originB, firstB.position), Is.GreaterThan(.1f));

            while ((lastFeedback.IsPlaying == false || firstB.position.y <= .02f) &&
                Time.realtimeSinceStartup < deadline)
            {
                Assert.AreEqual(1, _composition.Game.BoardingSteps.Count,
                    "The completed A lane must wait for the remaining B walk and hop.");
                Assert.AreEqual("L01-B-01", _composition.Game.BoardingSteps[0].PassengerId);
                Assert.AreEqual(0, CountTrace("Board:L01-A-02"));
                yield return null;
            }

            Assert.IsTrue(lastFeedback.IsPlaying);
            Assert.That(firstB.position.y, Is.GreaterThan(.02f));
            Assert.IsFalse(firstB.GetComponent<PassengerLocomotion>().IsMoving);
            Assert.IsTrue(firstB.GetComponentInChildren<LineRenderer>(true).enabled);
            _composition.SendMessage("OnApplicationFocus", false);
            Vector3 pausedHop = firstB.position;
            int pausedTraceCount = _composition.Game.Trace.Count;
            yield return new WaitForSeconds(.2f);
            Assert.AreEqual(pausedHop, firstB.position);
            Assert.AreEqual(pausedTraceCount, _composition.Game.Trace.Count);
            Assert.IsTrue(lastFeedback.IsPlaying);
            Assert.AreEqual(1, _composition.Game.BoardingSteps.Count);
            Assert.AreEqual("L01-B-01", _composition.Game.BoardingSteps[0].PassengerId);
            _composition.SendMessage("OnApplicationFocus", true);
            yield return Await(() => CountTrace("Board:L01-B-01") == 1);
            Assert.AreEqual(1, CountTrace("Board:L01-A-01"));
            Assert.AreEqual(2, _composition.Game.BoardingSteps.Count,
                "Only after every first-batch head arrives may both second heads start together.");
            Assert.AreEqual("L01-A-02", _composition.Game.BoardingSteps[0].PassengerId);
            Assert.AreEqual("L01-B-02", _composition.Game.BoardingSteps[1].PassengerId);
            Assert.AreEqual(0, CountTrace("Board:L01-A-02"));
            Assert.AreEqual(0, CountTrace("Board:L01-B-02"));
            Assert.That(_composition.Game.Snapshot.Rows[1].Bus.Occupied, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator PauseResumesBothWalkersAndRetryCancelsEveryOldCallback()
        {
            Drag("L01-R1", 1, false);
            yield return Await(() => _composition.Game.BoardingSteps.Count == 2);
            List<BoardStep> oldSteps = new List<BoardStep>(_composition.Game.BoardingSteps);
            Transform firstA = _board.GetPassenger(oldSteps[0].PassengerId);
            Transform firstB = _board.GetPassenger(oldSteps[1].PassengerId);
            yield return new WaitForSeconds(.15f);
            _composition.SendMessage("OnApplicationFocus", false);
            Vector3 pausedA = firstA.position;
            Vector3 pausedB = firstB.position;
            int traceCount = _composition.Game.Trace.Count;
            SetViewport(360, 640);
            yield return new WaitForSeconds(.25f);
            Assert.AreEqual(pausedA, firstA.position);
            Assert.AreEqual(pausedB, firstB.position);
            Assert.AreEqual(traceCount, _composition.Game.Trace.Count);
            Assert.AreEqual(2, _composition.Game.BoardingSteps.Count);
            _composition.SendMessage("OnApplicationFocus", true);
            yield return new WaitForSeconds(.1f);
            Assert.That(Vector3.Distance(pausedA, firstA.position), Is.GreaterThan(.05f));
            Assert.That(Vector3.Distance(pausedB, firstB.position), Is.GreaterThan(.05f));
            int oldGeneration = _composition.Game.Snapshot.Generation;
            _composition.Restart();

            foreach (BoardStep oldStep in oldSteps)
            {
                _composition.Game.Complete(oldStep);
            }

            yield return new WaitForSeconds(3);
            Assert.That(_composition.Game.Snapshot.Generation, Is.GreaterThan(oldGeneration));
            AssertInitialAttempt();
            Assert.AreEqual(0, _composition.Game.BoardingSteps.Count);
            Assert.IsFalse(_board.GetPassenger("L01-A-01").GetComponent<PassengerLocomotion>().IsMoving);
            Assert.IsFalse(_board.GetPassenger("L01-B-01").GetComponent<PassengerLocomotion>().IsMoving);
        }

        [UnityTest]
        public IEnumerator BoardingRefreshProjectsInspectionTargetOntoNextQueueHead()
        {
            string source = Resources.Load<TextAsset>("Levels/level_04").text;
            source = source.Replace("\"initial_bus_id\": \"P04-R\"", "\"initial_bus_id\": \"P04-G\"");
            int reserveStart = source.IndexOf("\"next_bus_queue\"", StringComparison.Ordinal);
            Assert.That(reserveStart, Is.GreaterThan(0));
            source = source.Substring(0, reserveStart)
                + source.Substring(reserveStart).Replace("\"P04-G\"", "\"P04-R\"");
            ConfigureAttemptFixture(source);
            Assert.AreEqual("P04-G", _composition.Game.Snapshot.Rows[0].Bus.Id);
            Drag("P04-G", 7, false);
            yield return Await(() => CountTrace("Board:P04-D-01") == 1);
            _composition.SendMessage("OnApplicationFocus", false);
            Assert.AreEqual("P04-D-02", _composition.Game.Snapshot.Queues[3].RemainingPassengers[0].Id);
            Assert.DoesNotThrow(() => _hud.Refresh(),
                "The boarded head is removed before HUD projection refreshes the next head.");
            Canvas.ForceUpdateCanvases();
            yield return null;
            LevelSecurityPanel security = GetSceneComponent<LevelSecurityPanel>();
            Vector2 target = RectTransformUtility.WorldToScreenPoint(_camera,
                security.GetQueueControl("D").transform.position);
            Vector2 passenger = _camera.WorldToScreenPoint(_board.GetPassengerBounds("P04-D-02").center);
            Assert.That(Vector2.Distance(target, passenger), Is.LessThan(1.5f),
                "The inspection target must track the new physical head after its predecessor boards.");
            Assert.AreEqual("3", GetText("Queue D").text);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CapacityBadgesClearBusSilhouettesAtBothOrientations()
        {
            Vector2Int[] sizes = { new Vector2Int(1600, 900), new Vector2Int(360, 640) };

            foreach (Vector2Int size in sizes)
            {
                SetViewport(size.x, size.y);
                yield return null;

                foreach (RowSnapshot row in _composition.Game.Snapshot.Rows)
                {
                    Bounds bounds = _board.GetBusBounds(row.Bus.Id);
                    float bodyTop = float.MinValue;

                    for (int x = 0; x < 2; x++)
                    {
                        for (int y = 0; y < 2; y++)
                        {
                            for (int z = 0; z < 2; z++)
                            {
                                Vector3 corner = new Vector3(x == 0 ? bounds.min.x : bounds.max.x,
                                    y == 0 ? bounds.min.y : bounds.max.y, z == 0 ? bounds.min.z : bounds.max.z);
                                bodyTop = Mathf.Max(bodyTop, _camera.WorldToScreenPoint(corner).y);
                            }
                        }
                    }

                    Text count = GetOccupancy(row.Bus.Id);
                    RectTransform badge = (RectTransform)count.transform.parent;
                    Vector3[] corners = new Vector3[4];
                    badge.GetWorldCorners(corners);
                    float badgeBottom = RectTransformUtility.WorldToScreenPoint(_camera, corners[0]).y;
                    Assert.That(badgeBottom - bodyTop, Is.GreaterThanOrEqualTo(3),
                        row.Bus.Id + " badge must clear the complete body silhouette at " + size);
                    Assert.That(count.fontSize * _hud.GetComponent<Canvas>().scaleFactor,
                        Is.GreaterThanOrEqualTo(12), "Capacity text must remain legible in real pixels.");
                }
            }

            Assert.AreEqual(0, _composition.Game.Trace.Count, "Framing must not mutate the attempt.");
        }

        [UnityTest]
        public IEnumerator DoorStopPreviewAndMagnetMatchMouseAndTouchRelease()
        {
            foreach (bool touch in new[] { false, true })
            {
                SetViewport(touch ? 360 : 1600, touch ? 640 : 900);
                yield return null;
                Vector3 origin = _board.GetBus("L01-R1").position;
                Vector3 center = _board.GetBusBounds("L01-R1").center;
                Vector2 from = Project(center);
                Vector2 outsideMagnet = Project(center - Vector3.right * _board.CellSize * 1.28f);

                if (touch)
                {
                    QueueTouch(from, InputTouchPhase.Began);
                    QueueTouch(outsideMagnet, InputTouchPhase.Moved);
                }
                else
                {
                    QueueMouse(from, true);
                    QueueMouse(outsideMagnet, true);
                }

                yield return null;
                Assert.That(GetImage("Stop Door").color.r, Is.GreaterThan(.9f));
                StringAssert.Contains("недоступна", GetText("Drag Hint").text);
                Assert.AreEqual(0, _composition.Game.Trace.Count);

                if (touch)
                {
                    QueueTouch(outsideMagnet, InputTouchPhase.Ended);
                }
                else
                {
                    QueueMouse(outsideMagnet, false);
                }

                Assert.That(Vector3.Distance(origin, _board.GetBus("L01-R1").position), Is.LessThan(.001f));
                Assert.IsFalse(GetImage("Stop Door").gameObject.activeInHierarchy);
                Vector2 insideMagnet = Project(center - Vector3.right * _board.CellSize * 1.55f);

                if (touch)
                {
                    QueueTouch(from, InputTouchPhase.Began);
                    QueueTouch(insideMagnet, InputTouchPhase.Moved);
                }
                else
                {
                    QueueMouse(from, true);
                    QueueMouse(insideMagnet, true);
                }

                yield return null;
                Image stop = GetImage("Stop Door");
                Assert.That(stop.color.g, Is.GreaterThan(.9f));
                Assert.That(stop.color.r, Is.LessThan(.6f));
                StringAssert.Contains("Отпустите", GetText("Drag Hint").text);
                float stopScreenX = RectTransformUtility.WorldToScreenPoint(_camera, stop.rectTransform.position).x;
                float laneScreenX = _camera.WorldToScreenPoint(new Vector3(_board.GetWorldX(2), 0, 2)).x;
                Assert.That(stopScreenX, Is.EqualTo(laneScreenX).Within(1));
                Assert.IsTrue(stop.gameObject.activeInHierarchy, "Door highlight must remain visible during drag.");
                Assert.AreEqual(3, _composition.Game.Snapshot.Rows[0].Bus.LeftX);

                if (touch)
                {
                    QueueTouch(insideMagnet, InputTouchPhase.Ended);
                }
                else
                {
                    QueueMouse(insideMagnet, false);
                }

                Assert.AreEqual(1, _composition.Game.Snapshot.Rows[0].Bus.LeftX);
                Assert.IsFalse(GetImage("Stop Door").gameObject.activeInHierarchy);
                yield return Await(() => CountTrace("Board:") == 1);
                _composition.Restart();
            }
        }

        [UnityTest]
        public IEnumerator BoardingWalkAnimatesRigAndAdvancesQueueWithoutTeleporting()
        {
            Transform passenger = _board.GetPassenger("L01-A-01");
            Vector3 origin = passenger.position;
            Transform tail = _board.GetPassenger("L01-A-03");
            Vector3 tailOrigin = tail.position;
            Animator animator = passenger.GetComponentInChildren<Animator>();
            Transform leg = null;

            foreach (Transform bone in passenger.GetComponentsInChildren<Transform>())
            {
                if (bone.name == "LowerLeg.L")
                {
                    leg = bone;
                }
            }

            Assert.IsNotNull(leg);
            Assert.IsTrue(_composition.Game.TryMove("R1", "L01-R1", 1));
            yield return new WaitForSeconds(.25f);
            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Walk"));
            Assert.That(Vector3.Distance(origin, passenger.position), Is.GreaterThan(.05f));
            Assert.That(passenger.position.z, Is.GreaterThan(_board.GetDoor("L01-R1").z));
            Quaternion pose = leg.localRotation;
            float greatestPoseChange = 0;
            float sampleUntil = Time.time + .22f;

            while (Time.time < sampleUntil)
            {
                greatestPoseChange = Mathf.Max(greatestPoseChange, Quaternion.Angle(pose, leg.localRotation));
                yield return null;
            }

            Assert.That(greatestPoseChange, Is.GreaterThan(5));
            _composition.SendMessage("OnApplicationFocus", false);
            Vector3 pausedPosition = passenger.position;
            Quaternion pausedPose = leg.localRotation;
            yield return new WaitForSeconds(.2f);
            Assert.That(Vector3.Distance(pausedPosition, passenger.position), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(pausedPose, leg.localRotation), Is.LessThan(.1f));
            _composition.SendMessage("OnApplicationFocus", true);
            yield return Await(() => CountTrace("Board:") == 1);
            Assert.IsTrue(tail.GetComponent<PassengerLocomotion>().IsMoving);
            Assert.That(tail.position.z, Is.GreaterThan(tailOrigin.z - 1.2f));
            yield return new WaitForSeconds(.25f);
            Assert.That(tail.position.z, Is.LessThan(tailOrigin.z - .02f));
            Assert.That(tail.position.y, Is.EqualTo(0).Within(.001f));
            _composition.Restart();
            yield return new WaitForSeconds(.3f);
            AssertInitialAttempt();
            Assert.IsFalse(_board.GetPassenger("L01-A-01").GetComponent<PassengerLocomotion>().IsMoving);
        }

        [UnityTest]
        public IEnumerator MouseLandscapeRunsActualAnimationsToVictory()
        {
            yield return PlayToVictory(false);
        }

        [UnityTest]
        public IEnumerator EmulatedTouchPortraitRunsSameStateAndTraceToVictory()
        {
            SetViewport(360, 640);
            yield return null;
            yield return PlayToVictory(true);
        }

        [UnityTest]
        public IEnumerator RestartDuringBoardDepartureAndArrivalCancelsOldAnimations()
        {
            BoardStepKind[] interrupted = { BoardStepKind.Board, BoardStepKind.Depart, BoardStepKind.Arrive };

            foreach (BoardStepKind kind in interrupted)
            {
                _pending = null;
                Assert.IsTrue(_composition.Game.TryMove("R1", "L01-R1", 1));
                yield return Await(() => _pending != null && _pending.Kind == kind);
                yield return null;
                int oldGeneration = _composition.Game.Snapshot.Generation;
                _composition.Restart();
                Assert.That(_composition.Game.Snapshot.Generation, Is.GreaterThan(oldGeneration));
                yield return new WaitForSeconds(1.3f);
                AssertInitialAttempt();
            }
        }

        [UnityTest]
        public IEnumerator FocusPauseFreezesBoardingAndOrientationChangePreservesState()
        {
            Assert.IsTrue(_composition.Game.TryMove("R1", "L01-R1", 1));
            yield return Await(() => _pending != null && _pending.Kind == BoardStepKind.Board);
            yield return new WaitForSeconds(.1f);
            int generation = _composition.Game.Snapshot.Generation;
            int traceCount = _composition.Game.Trace.Count;
            Transform passenger = _board.GetPassenger(_pending.PassengerId);
            _composition.SendMessage("OnApplicationFocus", false);
            Vector3 pausedPosition = passenger.position;
            SetViewport(360, 640);
            yield return new WaitForSeconds(.25f);
            Assert.IsTrue(_composition.Game.IsPaused);
            Assert.That(Vector3.Distance(pausedPosition, passenger.position), Is.LessThan(.0001f));
            Assert.AreEqual(traceCount, _composition.Game.Trace.Count);
            Assert.IsFalse(_composition.Game.TryMove("R2", "L01-T", 3));
            _composition.SendMessage("OnApplicationFocus", true);
            yield return Await(() => _composition.Game.Tutorial.Step == TutorialStep.ArrivalExplanation);
            yield return Await(() => _composition.Game.PendingStep == null);
            Assert.AreEqual(generation, _composition.Game.Snapshot.Generation);
            Assert.AreEqual(4, CountTrace("Board:"));
            Assert.AreEqual(2, CountTrace("BusDeparted:"));
            Assert.AreEqual(1, CountTrace("BusArrived:"));
            Assert.AreEqual("2", GetText("Queue A").text);
            Assert.AreEqual("0", GetText("Queue B").text);
        }

        [UnityTest]
        public IEnumerator CancelledTouchRestoresConfirmedPositionWithoutIssuingMove()
        {
            Vector3 initial = _board.GetBus("L01-R1").position;
            Vector3 center = _board.GetBusBounds("L01-R1").center;
            QueueTouch(Project(center), InputTouchPhase.Began);
            QueueTouch(Project(center + Vector3.right * _board.CellSize), InputTouchPhase.Moved);
            Assert.That(_board.GetBus("L01-R1").position.x, Is.GreaterThan(initial.x + .5f * _board.CellSize));
            Assert.AreEqual(3, _composition.Game.Snapshot.Rows[0].Bus.LeftX);
            QueueTouch(Project(center + Vector3.right * _board.CellSize), InputTouchPhase.Canceled);
            yield return null;
            Assert.That(Vector3.Distance(initial, _board.GetBus("L01-R1").position), Is.LessThan(.001f));
            Assert.AreEqual(0, _composition.Game.Trace.Count);
        }

        [UnityTest]
        public IEnumerator SmallBusKeepsFortyFourPixelTouchTargetAtPhoneAndShortLandscape()
        {
            foreach (Vector2Int viewport in new[] { new Vector2Int(360, 640), new Vector2Int(844, 300) })
            {
                SetViewport(viewport.x, viewport.y);
                yield return null;
                Assert.IsFalse(GetText("Blocker B").enabled,
                    "Compact viewport must keep the tutorial in the HUD when the inline caption cannot fit.");
                Vector2 center = Project(_board.GetBusBounds("L01-R1").center);
                Vector2 padded = center + Vector2.up * 21;
                QueueTouch(padded, InputTouchPhase.Began);
                Assert.AreEqual("L01-R1", GetSceneComponent<LevelBusDrag>().SelectedBusId);
                QueueTouch(padded, InputTouchPhase.Canceled);
                Assert.AreEqual(0, _composition.Game.Trace.Count);
            }
        }

        [UnityTest]
        public IEnumerator FullLandscapeBlockerCaptionFitsBetweenQueueCountAndBusBadge()
        {
            SetViewport(1600, 900);
            yield return null;
            Text blocker = GetText("Blocker B");
            Assert.IsTrue(blocker.enabled);
            RectTransform caption = blocker.rectTransform;
            RectTransform queue = GetText("Queue B").rectTransform;
            RectTransform badge = (RectTransform)GetOccupancy("L01-R1").transform.parent;
            Assert.Less(caption.anchoredPosition.y + caption.rect.height * .5f,
                queue.anchoredPosition.y - queue.rect.height * .5f);
            Assert.Greater(caption.anchoredPosition.y - caption.rect.height * .5f,
                badge.anchoredPosition.y + badge.rect.height * .5f);
        }

        [UnityTest]
        public IEnumerator FocusLossDuringMouseDragRestoresConfirmedPositionAndRequiresFreshPress()
        {
            Vector3 initial = _board.GetBus("L01-R1").position;
            Vector3 center = _board.GetBusBounds("L01-R1").center;
            QueueMouse(Project(center), true);
            QueueMouse(Project(center + Vector3.right * _board.CellSize), true);
            _pointer.SendMessage("OnApplicationFocus", false);
            _composition.SendMessage("OnApplicationFocus", false);
            Assert.That(Vector3.Distance(initial, _board.GetBus("L01-R1").position), Is.LessThan(.001f));
            Assert.IsFalse(GetImage("Stop Door").gameObject.activeInHierarchy);
            _composition.SendMessage("OnApplicationFocus", true);
            QueueMouse(Project(center + Vector3.right * _board.CellSize), false);
            yield return null;
            Assert.AreEqual(0, _composition.Game.Trace.Count);
            Drag("L01-R1", 1, false);
            yield return Await(() => CountTrace("Board:") > 0);
            Assert.AreEqual(1, _composition.Game.Snapshot.Rows[0].Bus.LeftX);
        }

        [UnityTest]
        public IEnumerator RotationDuringMouseDragCancelsReleaseAndAllowsFreshPortraitGesture()
        {
            Vector3 initial = _board.GetBus("L01-R1").position;
            Vector3 center = _board.GetBusBounds("L01-R1").center;
            QueueMouse(Project(center), true);
            QueueMouse(Project(center + Vector3.right * _board.CellSize), true);
            Assert.That(_board.GetBus("L01-R1").position.x, Is.GreaterThan(initial.x + .5f * _board.CellSize));
            SetViewport(360, 640);
            yield return null;
            QueueMouse(Project(center + Vector3.right * _board.CellSize), false);
            yield return null;
            Assert.That(Vector3.Distance(initial, _board.GetBus("L01-R1").position), Is.LessThan(.001f));
            Assert.AreEqual(0, _composition.Game.Trace.Count);
            Assert.AreEqual(3, _composition.Game.Snapshot.Rows[0].Bus.LeftX);
            Drag("L01-R1", 1, false);
            yield return Await(() => CountTrace("Board:") > 0);
            Assert.AreEqual(1, _composition.Game.Snapshot.Rows[0].Bus.LeftX);
        }

        [UnityTest]
        public IEnumerator DisableAndReenableDuringBoardingRestartsWithoutOldCallbacks()
        {
            foreach (bool deactivateObject in new[] { false, true })
            {
                _composition.Restart();
                Assert.IsTrue(_composition.Game.TryMove("R1", "L01-R1", 1));
                yield return Await(() => _pending != null && _pending.Kind == BoardStepKind.Board);
                yield return new WaitForSeconds(.1f);
                int previousGeneration = _composition.Game.Snapshot.Generation;

                if (deactivateObject)
                {
                    _composition.gameObject.SetActive(false);
                }
                else
                {
                    _composition.enabled = false;
                }

                yield return new WaitForSeconds(.1f);

                if (deactivateObject)
                {
                    _composition.gameObject.SetActive(true);
                }
                else
                {
                    _composition.enabled = true;
                }

                Assert.That(_composition.Game.Snapshot.Generation, Is.GreaterThan(previousGeneration));
                yield return new WaitForSeconds(1.3f);
                AssertInitialAttempt();
                LevelBusDrag drag = GetSceneComponent<LevelBusDrag>();
                string activation = deactivateObject ? "GameObject reactivation" : "component re-enable";
                Assert.IsTrue(_pointer.isActiveAndEnabled, activation + ": pointer reader must be enabled.");
                Assert.IsTrue(drag.isActiveAndEnabled, activation + ": drag subscriber must be enabled.");
                Assert.IsTrue(_composition.Game.CanSelect("L01-R1"), activation + ": the first bus must be selectable.");
                Assert.IsFalse(_mouse.leftButton.isPressed, activation + ": the previous gesture must be released.");
                Vector3 center = _board.GetBusBounds("L01-R1").center;
                float deltaX = _board.GetWorldX(1) - _board.GetBus("L01-R1").position.x;
                Vector2 from = Project(center);
                Vector2 to = Project(center + Vector3.right * deltaX);

                foreach (RectTransform region in _hud.PointerRegions)
                {
                    bool blocked = region.gameObject.activeInHierarchy &&
                        RectTransformUtility.RectangleContainsScreenPoint(region, from, _camera);
                    Assert.IsFalse(blocked, activation + ": bus press is blocked by " + region.name);
                }

                int presses = 0;
                Action<Vector2> countPress = position => presses++;
                _pointer.Pressed += countPress;

                try
                {
                    QueueMouse(from, true);
                    Assert.AreEqual(1, presses, activation + ": a fresh mouse press must reach the pointer reader.");
                    Assert.AreEqual("L01-R1", drag.SelectedBusId, activation + ": press must select the rebuilt bus.");
                    QueueMouse((from + to) * .5f, true);
                    Assert.That(_board.GetBus("L01-R1").position.x,
                        Is.EqualTo(_board.GetWorldX(2)).Within(.01f), activation + ": held movement must show its preview.");
                    QueueMouse(to, false);
                    Assert.AreEqual(1, _composition.Game.Snapshot.Rows[0].Bus.LeftX,
                        activation + ": release must commit the requested stop.");
                }
                finally
                {
                    _pointer.Pressed -= countPress;
                }

                yield return Await(() => CountTrace("Board:") > 0);
                Assert.AreEqual(1, _composition.Game.Snapshot.Rows[0].Bus.Occupied);
            }
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator CampaignLandscapeCompletesFourLevelsAndResetsInterruptedAttempts()
        {
            yield return PlayCampaign(false);
        }

        [UnityTest]
        public IEnumerator LaterCampaignRowsAndLongBodiesFitBothOrientationsAndKeepDoorAlignment()
        {
            foreach (int level in new[] { 7, 10, 13, 15 })
            {
                ConfigureAttemptFixture(Resources.Load<TextAsset>("Levels/level_" + level.ToString("00")).text);

                foreach (bool portrait in new[] { false, true })
                {
                    SetViewport(portrait ? 360 : 1600, portrait ? 640 : 900);
                    yield return null;
                    yield return null;
                    Assert.AreEqual(level < 13 ? 4 : 5, _composition.Game.Snapshot.Rows.Count);

                    if (portrait)
                    {
                        RectTransform isolation = GetText("Isolation Area").rectTransform;
                        RectTransform queue = GetText("Queue A").rectTransform;

                        if (isolation.gameObject.activeSelf)
                        {
                            Assert.That(isolation.anchoredPosition.x + isolation.rect.width * .5f,
                                Is.LessThan(queue.anchoredPosition.x - queue.rect.width * .5f));
                        }
                    }

                    foreach (RowSnapshot row in _composition.Game.Snapshot.Rows)
                    {
                        BusSnapshot bus = row.Bus;
                        Bounds bounds = _board.GetBusBounds(bus.Id);
                        Assert.That(bounds.size.x, Is.EqualTo(bus.FootprintCells * _board.CellSize).Within(.04f));
                        Assert.That(_board.GetDoor(bus.Id).x,
                            Is.EqualTo(_board.GetWorldX(bus.LeftX + 1)).Within(.02f));
                        Vector3 center = _camera.WorldToViewportPoint(bounds.center);
                        Assert.That(center.x, Is.InRange(0f, 1f));
                        Assert.That(center.y, Is.InRange(0f, 1f));
                        Assert.That(center.z, Is.GreaterThan(0));
                    }
                }
            }
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator CampaignPortraitCompletesFourLevelsAndResetsInterruptedAttempts()
        {
            SetViewport(360, 640);
            yield return null;
            yield return PlayCampaign(true);
        }

        [UnityTest]
        public IEnumerator BlockedBusShowsImmediateHintAndRetryClearsIt()
        {
            string source = Resources.Load<TextAsset>("Levels/level_01").text;
            ConfigureAttemptFixture(source.Replace("LEVEL-01", "LEVEL-02")
                .Replace("\"level_index\": 1", "\"level_index\": 2"));

            foreach (bool touch in new[] { false, true })
            {
                SetViewport(touch ? 360 : 1600, touch ? 640 : 900);
                yield return null;
                Drag("L01-R1", 1, touch);
                yield return Await(() => _composition.Game.BoardingSteps.Count > 0);
                int traceCount = _composition.Game.Trace.Count;
                Drag("L01-R1", 3, touch);
                Text hint = GetText("Drag Hint");
                Assert.IsTrue(hint.gameObject.activeInHierarchy);
                StringAssert.Contains("Автобус занят", hint.text);
                Assert.AreEqual(traceCount, _composition.Game.Trace.Count);
                Assert.AreEqual(1, _composition.Game.Snapshot.Rows[0].Bus.LeftX);
                _composition.SendMessage("OnApplicationFocus", false);
                yield return new WaitForSeconds(1.3f);
                StringAssert.Contains("Автобус занят", hint.text);
                _composition.SendMessage("OnApplicationFocus", true);
                yield return new WaitForSeconds(1.3f);
                Assert.That(hint.text, Does.Not.Contain("Автобус занят"));
                _hud.ShowRejectedMove();
                _composition.Restart();
                Assert.That(hint.text, Does.Not.Contain("Автобус занят"));
                yield return null;
            }
        }

        private void ConfigureSpareBusFixture()
        {
            string source = Resources.Load<TextAsset>("Levels/level_01").text;
            int busStart = source.IndexOf("\"id\": \"L01-T\"", StringComparison.Ordinal);
            int position = source.IndexOf("\"initial_left_x\": 3", busStart, StringComparison.Ordinal);
            Assert.That(busStart, Is.GreaterThan(0));
            Assert.That(position, Is.GreaterThan(busStart));
            string marker = "\"initial_left_x\": ";
            int valueStart = position + marker.Length;
            ConfigureAttemptFixture(source.Substring(0, valueStart) + "5" + source.Substring(valueStart + 1));
            Assert.AreEqual(5, _composition.Game.Snapshot.Rows[1].Bus.LeftX);
            Assert.IsNull(_composition.Game.PendingStep);
        }

        private void ConfigureAttemptFixture(string source)
        {
            if (_fixtureContent != null)
            {
                UnityEngine.Object.Destroy(_fixtureContent);
            }

            _fixtureContent = new TextAsset(source);
            _composition.Game.StepRequested -= OnStepRequested;
            _composition.enabled = false;
            _hud.Shutdown();
            _board.Clear();
            BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(LevelComposition).GetField("_levels", fields).SetValue(_composition, new[] { _fixtureContent });
            typeof(LevelComposition).GetField("_levelIndex", fields).SetValue(_composition, 0);
            typeof(LevelComposition).GetField("_randomizeArrivals", fields).SetValue(_composition, false);
            _composition.SendMessage("LoadAttempt");
            _composition.enabled = true;
            _composition.Game.StepRequested += OnStepRequested;
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

        private static CampaignWitness LoadCampaignWitness(LevelGameSession game, int level)
        {
            string path = game.ArrivalVariantId == "fixed"
                ? "Levels/level_" + level.ToString("00")
                : "ArrivalVariants/" + game.LevelId + "/" + game.ArrivalVariantId;
            TextAsset content = Resources.Load<TextAsset>(path);
            Assert.IsNotNull(content, "Missing actual attempt witness: " + path);
            CampaignWitness witness = JsonUtility.FromJson<CampaignWitness>(content.text);
            Assert.IsNotNull(witness);
            Assert.IsNotNull(witness.reference_solution);
            Assert.That(witness.reference_solution.Length, Is.GreaterThan(0));
            return witness;
        }

        private IEnumerator PlayToVictory(bool touch)
        {
            AssertInitialBlockerVisible();
            Assert.IsFalse(_composition.Game.TryMove("R2", "L01-T", 3));
            Drag("L01-R1", 2, touch);
            Assert.AreEqual(3, _composition.Game.Snapshot.Rows[0].Bus.LeftX);
            Assert.AreEqual(0, _composition.Game.Trace.Count);
            _composition.Game.ContinueTutorial();
            Assert.IsFalse(_composition.Game.Tutorial.IsComplete);
            Drag("L01-R1", 1, touch);
            yield return Await(() => CountTrace("Board:") == 1);
            Assert.AreEqual("1/2", GetOccupancy("L01-R1").text);
            Assert.AreEqual("3", GetText("Queue A").text);
            StringAssert.Contains("В очередях 5", GetText("Scene Counts").text);
            Assert.IsFalse(_composition.Game.CanSelect("L01-T"));
            yield return Await(() => _pending != null && _pending.Kind == BoardStepKind.Depart);
            Assert.AreEqual("L01-R1", _composition.Game.Snapshot.Rows[0].Bus.Id);
            Assert.AreEqual(2, _composition.Game.Snapshot.Rows[0].Bus.Occupied);
            Assert.AreEqual(1, _composition.Game.Snapshot.NextBusIds.Count);
            Assert.AreEqual("2/2", GetOccupancy("L01-R1").text);
            Assert.AreEqual(0, CountTrace("BusDeparted:"));
            yield return Await(() => _composition.Game.Tutorial.Step == TutorialStep.ArrivalExplanation);
            yield return Await(() => _composition.Game.PendingStep == null);
            Assert.AreEqual("L01-R2", _composition.Game.Snapshot.Rows[0].Bus.Id);
            Assert.AreEqual(0, _composition.Game.Snapshot.NextBusIds.Count);
            Assert.AreEqual("2", GetText("Queue A").text);
            Assert.AreEqual("0", GetText("Queue B").text);
            Assert.AreEqual(4, CountTrace("Board:"));
            Assert.IsNull(_composition.Game.Snapshot.Rows[1].Bus);
            Assert.IsTrue(_composition.Game.CanSelect("L01-R2"));
            Button continueButton = GetButton("Continue");
            Assert.IsTrue(continueButton.gameObject.activeInHierarchy);

            if (touch)
            {
                continueButton.onClick.Invoke();
                Assert.IsTrue(_composition.Game.Tutorial.IsComplete);
            }

            Drag("L01-R2", 1, touch);
            Assert.IsTrue(_composition.Game.Tutorial.IsComplete);
            Assert.IsFalse(continueButton.gameObject.activeInHierarchy);
            yield return Await(() => CountTrace("Victory") == 1);
            Assert.IsTrue(_composition.Game.Snapshot.IsVictory);
            Assert.AreEqual(6, CountTrace("Board:"));
            Assert.AreEqual(3, CountTrace("BusDeparted:"));
            Assert.AreEqual(1, CountTrace("BusArrived:"));
            Assert.IsNull(_composition.Game.Snapshot.Rows[0].Bus);
            Assert.IsNull(_composition.Game.Snapshot.Rows[1].Bus);
            Assert.AreEqual(0, _composition.Game.Snapshot.Queues[0].RemainingPassengers.Count);
            Assert.AreEqual(0, _composition.Game.Snapshot.Queues[1].RemainingPassengers.Count);
            Assert.IsTrue(GetText("Victory Title").gameObject.activeInHierarchy);
            Assert.AreEqual("Уровень пройден", GetText("Victory Title").text);
            GetButton("Repeat").onClick.Invoke();
            yield return null;
            AssertInitialAttempt();
        }

        private void AssertInitialAttempt()
        {
            Assert.IsFalse(_composition.Game.Snapshot.IsVictory);
            Assert.AreEqual(TutorialStep.AlignFirstBus, _composition.Game.Tutorial.Step);
            Assert.AreEqual("L01-R1", _composition.Game.Snapshot.Rows[0].Bus.Id);
            Assert.AreEqual(3, _composition.Game.Snapshot.Rows[0].Bus.LeftX);
            Assert.AreEqual(0, _composition.Game.Snapshot.Rows[0].Bus.Occupied);
            Assert.AreEqual(1, _composition.Game.Snapshot.NextBusIds.Count);
            Assert.AreEqual(4, _composition.Game.Snapshot.Queues[0].RemainingPassengers.Count);
            Assert.AreEqual(2, _composition.Game.Snapshot.Queues[1].RemainingPassengers.Count);
            Assert.AreEqual(0, _composition.Game.Trace.Count);
            Assert.AreEqual("0/2", GetOccupancy("L01-R1").text);
            Assert.That(_board.GetBus("L01-R1").position.x, Is.EqualTo(_board.GetWorldX(3)).Within(.001f));
            Assert.That(_board.GetPassenger("L01-A-01").position.x, Is.EqualTo(_board.GetWorldX(2)).Within(.001f));
            AssertInitialBlockerVisible();
        }

        private void AssertInitialBlockerVisible()
        {
            LaneAccess access = _composition.Game.GetLaneAccess("B");
            Assert.AreEqual("L01-R1", access.BusId);
            Assert.AreEqual("R1", access.FirstRowId);
            Assert.AreEqual(LaneAccessReason.RouteMismatch, access.Reason);
            Vector3 passenger = _board.GetPassenger("L01-B-01").position;
            Bounds upper = _board.GetBusBounds("L01-R1");
            Bounds lower = _board.GetBusBounds("L01-T");
            Assert.That(passenger.x, Is.InRange(upper.min.x, upper.max.x));
            Assert.That(_board.GetDoor("L01-R1").x, Is.EqualTo(passenger.x).Within(.02f));
            Assert.That(_board.GetDoor("L01-T").x, Is.EqualTo(passenger.x).Within(.02f));
            Text blocker = GetText("Blocker B");
            Assert.IsTrue(blocker.gameObject.activeInHierarchy);
            StringAssert.Contains("R1", blocker.text);
            StringAssert.Contains("МАРШРУТ", blocker.text);

            foreach (Vector3 point in new[] { passenger, upper.center, lower.center })
            {
                Vector3 screen = _camera.WorldToScreenPoint(point);
                Assert.That(screen.x, Is.InRange(0, _viewport.width), "Initial blocker or queue is outside viewport.");
                Assert.That(screen.y, Is.InRange(0, _viewport.height), "Initial blocker or queue is outside viewport.");
                Assert.That(screen.z, Is.GreaterThan(0));
            }
        }

        private IEnumerator Await(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + AnimationTimeoutSeconds;

            while (condition() == false && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.IsTrue(condition(), "Timed out waiting for the real scene animation/state transition. " +
                "Level=" + _composition.Game.LevelId + "; Variant=" + _composition.Game.ArrivalVariantId +
                "; Paused=" + _composition.Game.IsPaused + "; Tutorial=" + _composition.Game.Tutorial.Step +
                "; Phase=" + (_composition.Game.PendingStep == null ? "Ready" :
                    _composition.Game.PendingStep.Kind.ToString()) +
                "; Trace=" + string.Join(",", _composition.Game.Trace));
        }

        private void Drag(string busId, int leftX, bool touch)
        {
            Vector3 center = _board.GetBusBounds(busId).center;
            float deltaX = _board.GetWorldX(leftX) - _board.GetBus(busId).position.x;
            Vector2 from = Project(center);
            Vector2 to = Project(center + Vector3.right * deltaX);

            if (touch)
            {
                QueueTouch(from, InputTouchPhase.Began);
                QueueTouch((from + to) * .5f, InputTouchPhase.Moved);
                QueueTouch(to, InputTouchPhase.Ended);
            }
            else
            {
                QueueMouse(from, true);
                QueueMouse((from + to) * .5f, true);
                QueueMouse(to, false);
            }
        }

        private void QueueMouse(Vector2 position, bool pressed)
        {
            MouseState state = new MouseState { position = position };
            InputSystem.QueueStateEvent(_mouse, state.WithButton(MouseButton.Left, pressed));
            InputSystem.Update();
            _mouse.MakeCurrent();
            _pointer.SendMessage("Update");
        }

        private void QueueTouch(Vector2 position, InputTouchPhase phase)
        {
            TouchState state = new TouchState { touchId = 1, position = position, phase = phase };
            InputSystem.QueueStateEvent(_touch, state);
            InputSystem.Update();
            _touch.MakeCurrent();
            _pointer.SendMessage("Update");
        }

        private Vector2 Project(Vector3 position)
        {
            return _camera.WorldToScreenPoint(position);
        }

        private void SetViewport(int width, int height)
        {
            if (_viewport != null)
            {
                _camera.targetTexture = null;
                _viewport.Release();
                UnityEngine.Object.Destroy(_viewport);
            }

            _viewport = new RenderTexture(width, height, 24);
            _camera.targetTexture = _viewport;
            _camera.aspect = (float)width / height;
            _hud.Refresh();
            Canvas.ForceUpdateCanvases();
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

        private Text GetOccupancy(string busId)
        {
            foreach (Text text in _hud.GetComponentsInChildren<Text>(true))
            {
                if (text.name == "Occupancy" && text.transform.parent.name == "Bus " + busId)
                {
                    return text;
                }
            }

            Assert.Fail("Missing occupancy badge for " + busId);
            return null;
        }

        private Image GetImage(string name)
        {
            foreach (Image image in _hud.GetComponentsInChildren<Image>(true))
            {
                if (image.name == name)
                {
                    return image;
                }
            }

            Assert.Fail("Missing HUD image " + name);
            return null;
        }

        private Text GetText(string name)
        {
            foreach (Text text in _hud.GetComponentsInChildren<Text>(true))
            {
                if (text.name == name)
                {
                    return text;
                }
            }

            Assert.Fail("Missing scene text " + name);
            return null;
        }

        private Button GetButton(string name)
        {
            foreach (Button button in _hud.GetComponentsInChildren<Button>(true))
            {
                if (button.name == name)
                {
                    return button;
                }
            }

            Assert.Fail("Missing scene button " + name);
            return null;
        }

        private string GetAttemptSignature()
        {
            string signature = _composition.Game.ArrivalVariantId;
            BoardSnapshot snapshot = _composition.Game.Snapshot;

            foreach (RowSnapshot row in snapshot.Rows)
            {
                signature += "|" + row.Id + ":";

                if (row.Bus != null)
                {
                    signature += row.Bus.Id + ":" + row.Bus.Route + ":" + row.Bus.FootprintCells
                        + ":" + row.Bus.Capacity + ":" + row.Bus.LeftX + ":" + row.Bus.Occupied;
                }

            }

            foreach (QueueSnapshot queue in snapshot.Queues)
            {
                signature += "|" + queue.Id + ":" + queue.X;

                foreach (PassengerDefinition passenger in queue.RemainingPassengers)
                {
                    signature += ">" + passenger.Id + ":" + passenger.Route + ":" + passenger.Kind;
                }
            }

            signature += "|FIFO:" + string.Join(",", snapshot.NextBusIds) + ":Lost:" + snapshot.LostSeats;
            return signature + "|" + snapshot.CctvCharges + ":" + snapshot.CctvUses
                + ":" + snapshot.Stability + ":" + snapshot.EmergencyResets + ":" + snapshot.IsVictory
                + ":" + snapshot.IsDefeat;
        }

        private BusSnapshot GetCurrentBus(string rowId)
        {
            foreach (RowSnapshot row in _composition.Game.Snapshot.Rows)
            {
                if (row.Id == rowId)
                {
                    Assert.IsNotNull(row.Bus, "Witness addresses an empty row: " + rowId);
                    return row.Bus;
                }
            }

            Assert.Fail("Witness addresses an unknown row: " + rowId);
            return null;
        }

        private IEnumerator ExecuteCampaignAction(CampaignAction action, bool touch)
        {
            switch (action.action)
            {
                case "move":
                    Drag(GetCurrentBus(action.row).Id, action.left_x, touch);
                    Assert.IsNotNull(_composition.Game.PendingStep, "Actual input must issue the witnessed move.");
                    Assert.AreEqual(BoardStepKind.Move, _composition.Game.PendingStep.Kind);
                    break;
                case "scan":
                    _composition.Game.Scan();
                    Assert.IsNull(_composition.Game.PendingStep);
                    Assert.That(_composition.Game.Snapshot.CctvUses, Is.GreaterThan(0));
                    yield return Await(() => GetSceneComponent<LevelSecurityPanel>().IsVideo == false);
                    break;
                case "quarantine":
                    SelectQuarantine(action.queue);
                    Assert.IsNotNull(_composition.Game.PendingStep);
                    Assert.AreEqual(BoardStepKind.Quarantine, _composition.Game.PendingStep.Kind);
                    break;
                default:
                    Assert.Fail("Unsupported witness action: " + action.action);
                    yield break;
            }

            yield return Await(() => _composition.Game.PendingStep == null);

            if (_composition.Game.Tutorial.Step == TutorialStep.ArrivalExplanation)
            {
                GetButton("Continue").onClick.Invoke();
            }
        }

        private IEnumerator PlayCampaign(bool touch)
        {
            for (int level = 1; level <= 4; level++)
            {
                Assert.AreEqual("LEVEL-" + level.ToString("00"), _composition.Game.LevelId);
                Assert.IsFalse(_composition.Game.Snapshot.IsVictory);
                Assert.AreEqual(level < 15, _composition.Game.HasNextLevel);
                yield return null;
                SaveCampaignFrame("LEVEL-" + level.ToString("00"));

                if (level == 1)
                {
                    AssertInitialBlockerVisible();
                    Assert.AreEqual("fixed", _composition.Game.ArrivalVariantId);
                }
                else
                {
                    Assert.AreNotEqual("fixed", _composition.Game.ArrivalVariantId);
                }

                foreach (RowSnapshot activeRow in _composition.Game.Snapshot.Rows)
                {
                    BusSnapshot activeBus = activeRow.Bus;
                    Bounds bounds = _board.GetBusBounds(activeBus.Id);
                    Assert.That(bounds.size.x,
                        Is.EqualTo(activeBus.FootprintCells * _board.CellSize).Within(.04f));
                    Assert.That(_board.GetDoor(activeBus.Id).x,
                        Is.EqualTo(_board.GetWorldX(activeBus.LeftX + 1)).Within(.02f));
                }

                CampaignWitness witness = LoadCampaignWitness(_composition.Game, level);
                string initialSignature = GetAttemptSignature();
                int width = _viewport.width;
                int height = _viewport.height;
                SetViewport(height, width);
                yield return null;
                Assert.AreEqual(initialSignature, GetAttemptSignature(),
                    "Rotation must preserve the selected arrival variant.");
                SetViewport(width, height);
                yield return null;
                Assert.AreEqual(initialSignature, GetAttemptSignature());
                LevelGameSession attempt = _composition.Game;
                _composition.NextLevel();
                Assert.AreSame(attempt, _composition.Game, "Next requires victory.");
                CampaignAction firstMove = null;

                foreach (CampaignAction action in witness.reference_solution)
                {
                    if (action.action == "move")
                    {
                        firstMove = action;
                        break;
                    }
                }

                Assert.IsNotNull(firstMove, "Every playable variant needs an actual drag.");
                int generation = _composition.Game.Snapshot.Generation;
                Drag(GetCurrentBus(firstMove.row).Id, firstMove.left_x, touch);
                Assert.IsNotNull(_composition.Game.PendingStep);
                yield return null;
                _composition.Restart();
                yield return null;
                Assert.That(_composition.Game.Snapshot.Generation, Is.GreaterThan(generation));
                Assert.AreEqual(initialSignature, GetAttemptSignature(),
                    "Restart must keep the selected arrival variant.");
                Assert.AreEqual(0, _composition.Game.Trace.Count);
                Assert.IsNull(_composition.Game.PendingStep);
                Assert.AreEqual(level == 1, _composition.Game.Tutorial.IsComplete == false);

                if (level == 4)
                {
                    CampaignAction isolation = null;

                    foreach (CampaignAction action in witness.reference_solution)
                    {
                        if (action.action == "quarantine")
                        {
                            isolation = action;
                            break;
                        }

                        yield return ExecuteCampaignAction(action, touch);
                    }

                    Assert.IsNotNull(isolation, "LEVEL-04 witness must include explicit quarantine.");
                    SelectQuarantine(isolation.queue);
                    BoardStep oldIsolation = _composition.Game.PendingStep;
                    Assert.IsNotNull(oldIsolation);
                    Assert.AreEqual(BoardStepKind.Quarantine, oldIsolation.Kind);
                    yield return new WaitForSeconds(.1f);
                    _composition.Restart();
                    _composition.Game.Complete(oldIsolation);
                    yield return new WaitForSeconds(.2f);
                    Assert.AreEqual(initialSignature, GetAttemptSignature(),
                        "Quarantine reset must restore the same variant.");
                    Assert.AreEqual(1, _composition.Game.Snapshot.CctvCharges);
                    Assert.AreEqual(3, _composition.Game.Snapshot.Stability);
                    int remainingPassengers = 0;

                    foreach (QueueSnapshot queue in _composition.Game.Snapshot.Queues)
                    {
                        foreach (PassengerDefinition passenger in queue.RemainingPassengers)
                        {
                            Assert.AreEqual("human", passenger.Kind);
                        }

                        remainingPassengers += queue.RemainingPassengers.Count;
                    }

                    Assert.AreEqual(17, remainingPassengers);
                    Assert.AreEqual(0, _composition.Game.Trace.Count);
                    Assert.IsNull(_composition.Game.PendingStep);
                }

                foreach (CampaignAction action in witness.reference_solution)
                {
                    yield return ExecuteCampaignAction(action, touch);
                }

                Assert.IsTrue(_composition.Game.Snapshot.IsVictory, "LEVEL-" + level);
                Assert.AreEqual("Victory", _composition.Game.Trace[_composition.Game.Trace.Count - 1]);

                if (level == 4)
                {
                    Assert.AreEqual(1, _composition.Game.Snapshot.CctvUses);
                    Assert.AreEqual(3, _composition.Game.Snapshot.Stability);
                    Assert.AreEqual(0, _composition.Game.Snapshot.EmergencyResets);
                }

                if (level < 4)
                {
                    GetButton("Next Level").onClick.Invoke();
                    yield return null;
                }
            }
        }

        private void SelectQuarantine(string queueId)
        {
            LevelSecurityPanel security = GetSceneComponent<LevelSecurityPanel>();

            if (security.IsInspectionOpen == false)
            {
                security.GetSelectionControl().onClick.Invoke();
            }

            security.GetQueueControl(queueId).onClick.Invoke();
            security.GetQuarantineControl().onClick.Invoke();
        }

        private void SaveCampaignFrame(string levelId)
        {
            RenderTexture previous = RenderTexture.active;
            Texture2D capture = new Texture2D(_viewport.width, _viewport.height, TextureFormat.RGB24, false);

            try
            {
                RenderTexture.active = _viewport;
                capture.ReadPixels(new Rect(0, 0, _viewport.width, _viewport.height), 0, 0);
                capture.Apply();
                string path = "TestResults/Campaign-" + levelId + "-" + _viewport.width + "x" +
                    _viewport.height + ".png";
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                System.IO.File.WriteAllBytes(path, capture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.Destroy(capture);
            }
        }

        private void OnStepRequested(BoardStep step)
        {
            _pending = step;
        }

        // QA-only DTOs mirror the external snake_case witness; runtime gameplay never reads it.
#pragma warning disable CS0649
        [Serializable]
        private class CampaignWitness
        {
            public CampaignAction[] reference_solution;
        }

        [Serializable]
        private class CampaignAction
        {
            public string action;
            public string row;
            public int left_x;
            public string queue;
        }
#pragma warning restore CS0649
    }
}
