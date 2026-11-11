using System;
using System.Collections.Generic;
using System.IO;
using LastExit.Content;
using LastExit.Domain;
using LastExit.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace LastExit.Tests.Gameplay
{
    public class ContentTutorialTests
    {
        private string _json;

        [SetUp]
        public void LoadSource()
        {
            string projectPath = Directory.GetParent(Application.dataPath).FullName;
            string sourcePath = Path.Combine(projectPath, "SourceData", "Levels", "level_01.json");
            _json = File.ReadAllText(sourcePath).Replace("\r\n", "\n");
        }

        [Test]
        public void ImportedResourceMatchesSourceBytes()
        {
            string projectPath = Directory.GetParent(Application.dataPath).FullName;
            string sourcePath = Path.Combine(projectPath, "SourceData", "Levels", "level_01.json");
            string importedPath = Path.Combine(Application.dataPath, "_LAST_EXIT", "Resources", "Levels", "level_01.json");
            CollectionAssert.AreEqual(File.ReadAllBytes(sourcePath), File.ReadAllBytes(importedPath));
        }

        [Test]
        public void ParsesActualSnakeCaseLevelWithReservedClassField()
        {
            LevelDefinition level = LevelJsonImporter.Parse(_json);
            Assert.AreEqual("LEVEL-01", level.Id);
            Assert.AreEqual(12, level.Width);
            Assert.AreEqual(2, level.Queues.Count);
            Assert.AreEqual(4, level.Queues[0].Passengers.Count);
            Assert.AreEqual(2, level.Queues[1].Passengers.Count);
            Assert.AreEqual(3, level.Buses.Count);
            Assert.AreEqual("MINI", level.Buses[0].Class);
            Assert.AreEqual(2, level.Buses[0].FootprintCells);
            Assert.AreEqual(2, level.Buses[0].Capacity);
            Assert.AreEqual(3, level.Buses[0].InitialLeftX);
            Assert.AreEqual(0, level.Buses[1].InitialLeftX);
            Assert.AreEqual(3, level.Buses[2].InitialLeftX);
            Assert.AreEqual("L01-R2", level.NextBusIds[0]);
        }

        [Test]
        public void DoesNotUseReferenceSolutionAsGameplayData()
        {
            int referenceIndex = _json.IndexOf("\"reference_solution\"", StringComparison.Ordinal);
            int arrayStart = _json.IndexOf('[', referenceIndex);
            int arrayEnd = _json.IndexOf(']', arrayStart);
            Assert.That(arrayStart, Is.GreaterThan(referenceIndex));
            Assert.That(arrayEnd, Is.GreaterThan(arrayStart));
            string jsonWithoutSolution = _json.Substring(0, arrayStart)
                + "\"deliberately not a solution\"" + _json.Substring(arrayEnd + 1);
            LevelDefinition level = LevelJsonImporter.Parse(jsonWithoutSolution);
            Assert.AreEqual("L01-R1", level.Rows[0].InitialBusId);
            Assert.AreEqual(3, level.Buses[0].InitialLeftX);
        }

        [TestCase("\"schema_version\": \"0.4\"", "\"schema_version\": \"0.2\"")]
        [TestCase("\"level_index\": 1", "\"level_index\": 2")]
        [TestCase("\"width\": 12", "\"width\": 0")]
        [TestCase("\"kind\": \"human\"", "\"kind\": \"echo\"")]
        [TestCase("\"capacity\": 2", "\"capacity\": 3")]
        [TestCase("\"cctv_charges\": 0", "\"cctv_charges\": -1")]
        [TestCase("\"target_cctv_uses\": 0", "\"target_cctv_uses\": 1")]
        [TestCase("\"cctv_charges\": 0,", "")]
        [TestCase("\"A\": 2,", "")]
        [TestCase(",\n      \"initial_left_x\": 3", "")]
        [TestCase("\"L01-R2\"", "\"L01-R1\"")]
        public void RejectsUnsupportedOrInconsistentContent(string original, string replacement)
        {
            string invalidJson = _json.Replace(original, replacement);
            Assert.AreNotEqual(_json, invalidJson, "The regression fixture must alter its intended JSON field.");
            Assert.Throws<ArgumentException>(() => LevelJsonImporter.Parse(invalidJson));
        }

        [Test]
        public void AcceptsExplicitZeroCoordinatesInsteadOfTreatingThemAsMissing()
        {
            string zeroCoordinates = _json.Replace("\"A\": 2", "\"A\": 0")
                .Replace("\"initial_left_x\": 3", "\"initial_left_x\": 0");
            LevelDefinition level = LevelJsonImporter.Parse(zeroCoordinates);
            Assert.AreEqual(0, level.Corridors["A"]);
            Assert.AreEqual(0, level.Buses[0].InitialLeftX);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("{}")]
        public void RejectsMissingLevel(string json)
        {
            Assert.Throws<ArgumentException>(() => LevelJsonImporter.Parse(json));
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void LaterLevelsStartAndRestartWithoutFirstLevelTutorialRestrictions(int index)
        {
            TextAsset content = Resources.Load<TextAsset>("Levels/level_" + index.ToString("00"));
            Assert.IsNotNull(content);
            LevelGameSession game = new LevelGameSession(LevelJsonImporter.Parse(content.text), index < 4);
            Assert.AreEqual("LEVEL-" + index.ToString("00"), game.LevelId);
            Assert.IsTrue(game.Tutorial.IsComplete);
            Assert.IsTrue(game.HasNextLevel == (index < 4));
            game.Restart();
            Assert.IsTrue(game.Tutorial.IsComplete);
        }

        [TestCase(5, "головы")]
        [TestCase(6, "выезда")]
        [TestCase(7, "четыре")]
        [TestCase(8, "все головы")]
        [TestCase(10, "LONG")]
        [TestCase(13, "пять")]
        public void CampaignIntroductionExplainsExistingRulesWithoutBlockingInput(int levelIndex, string keyword)
        {
            TextAsset content = Resources.Load<TextAsset>("Levels/level_" + levelIndex.ToString("00"));
            LevelDefinition level = LevelJsonImporter.Parse(content.text);
            LevelGameSession game = new LevelGameSession(level, levelIndex < 15);
            Assert.IsTrue(game.Tutorial.IsComplete);
            StringAssert.Contains(keyword, game.Tutorial.Text);

            foreach (RowDefinition row in level.Rows)
            {
                Assert.IsTrue(game.CanSelect(row.InitialBusId));
            }
        }

        [Test]
        public void CompletionFromAnotherAttemptCannotAcknowledgeMatchingToken()
        {
            LevelDefinition level = LevelJsonImporter.Parse(_json);
            LevelGameSession oldAttempt = new LevelGameSession(level);
            LevelGameSession current = new LevelGameSession(level);
            oldAttempt.TryMove("R1", "L01-R1", 1);
            current.TryMove("R1", "L01-R1", 1);
            BoardStep pending = current.PendingStep;
            current.Complete(oldAttempt.PendingStep);
            Assert.AreSame(pending, current.PendingStep);
            Assert.AreEqual(0, current.Trace.Count);
        }

        [Test]
        public void TutorialOnlyAcceptsFirstBusAtQueueADoorPosition()
        {
            LevelTutorial tutorial = new LevelTutorial(LevelJsonImporter.Parse(_json));
            Assert.AreEqual(TutorialStep.AlignFirstBus, tutorial.Step);
            Assert.AreEqual("L01-R1", tutorial.FirstBusId);
            Assert.AreEqual("R1", tutorial.FirstRowId);
            Assert.AreEqual(1, tutorial.TargetLeftX);
            Assert.IsTrue(tutorial.CanSelect("L01-R1"));
            Assert.IsTrue(tutorial.CanMove("L01-R1", 1));
            Assert.IsFalse(tutorial.CanMove("L01-R1", 0));
            Assert.IsFalse(tutorial.CanSelect("L01-T"));
            Assert.IsFalse(tutorial.CanMove("L01-T", 1));
            tutorial.Continue();
            Assert.AreEqual(TutorialStep.AlignFirstBus, tutorial.Step);
        }

        [Test]
        public void TutorialFollowsBoardEventsAndWaitsForArrivalAcknowledgement()
        {
            LevelDefinition level = LevelJsonImporter.Parse(_json);
            LevelTutorial tutorial = new LevelTutorial(level);
            BoardSession board = new BoardSession(level);
            Assert.IsTrue(board.TryMoveBus("R1", tutorial.TargetLeftX));
            CompleteNext(board, tutorial, BoardStepKind.Move);
            Assert.AreEqual(TutorialStep.AlignFirstBus, tutorial.Step);
            tutorial.Observe(board.PendingStep);
            Assert.AreEqual(TutorialStep.Boarding, tutorial.Step);
            Assert.IsFalse(tutorial.CanSelect(tutorial.FirstBusId));
            CompleteNext(board, tutorial, BoardStepKind.Board);
            CompleteNext(board, tutorial, BoardStepKind.Board);
            CompleteNext(board, tutorial, BoardStepKind.Board);
            CompleteNext(board, tutorial, BoardStepKind.Board);
            tutorial.Observe(board.PendingStep);
            Assert.AreEqual(TutorialStep.Departing, tutorial.Step);
            tutorial.Continue();
            Assert.IsFalse(tutorial.IsComplete);
            CompleteNext(board, tutorial, BoardStepKind.Depart);
            CompleteNext(board, tutorial, BoardStepKind.Depart);
            tutorial.Observe(board.PendingStep);
            Assert.AreEqual(TutorialStep.Departing, tutorial.Step);
            CompleteNext(board, tutorial, BoardStepKind.Arrive);
            Assert.AreEqual(TutorialStep.ArrivalExplanation, tutorial.Step);
            Assert.IsNull(board.PendingStep);
            Assert.IsTrue(tutorial.CanSelect("L01-R2"));
            Assert.IsTrue(tutorial.CanMove("L01-R2", 1));
            Assert.IsFalse(tutorial.CanMove("L01-R2", 2));
            Assert.IsFalse(tutorial.CanSelect("L01-R1"));
            Assert.IsFalse(tutorial.CanMove("L01-T", 3));
            tutorial.Continue();
            Assert.IsTrue(tutorial.IsComplete);
            Assert.IsTrue(tutorial.CanSelect("L01-R2"));
            Assert.IsTrue(tutorial.CanMove("L01-T", 3));
            tutorial.Continue();
            Assert.IsTrue(tutorial.IsComplete);
            tutorial.Reset();
            Assert.AreEqual(TutorialStep.AlignFirstBus, tutorial.Step);
            Assert.IsFalse(tutorial.CanSelect("L01-R2"));
        }

        [Test]
        public void SuccessorMoveContinuesTutorialOnlyAfterAnAcceptedCommand()
        {
            LevelGameSession game = new LevelGameSession(LevelJsonImporter.Parse(_json));
            Assert.IsTrue(game.TryMove("R1", "L01-R1", 1));
            CompleteSessionSteps(game);
            Assert.AreEqual(TutorialStep.ArrivalExplanation, game.Tutorial.Step);
            Assert.IsTrue(game.CanSelect("L01-R2"));
            Assert.IsTrue(game.GetStopPreview("L01-R2", 1).IsAllowed);
            Assert.IsFalse(game.GetStopPreview("L01-R2", 2).IsAllowed);
            Assert.IsFalse(game.TryMove("R1", "L01-R2", 2));
            Assert.IsFalse(game.TryMove("R2", "L01-R2", 1));
            Assert.AreEqual(TutorialStep.ArrivalExplanation, game.Tutorial.Step);

            game.SetPaused(true);
            Assert.IsFalse(game.TryMove("R1", "L01-R2", 1));
            Assert.AreEqual(TutorialStep.ArrivalExplanation, game.Tutorial.Step);
            game.SetPaused(false);
            game.SetSecurityViewing(true);
            Assert.IsFalse(game.TryMove("R1", "L01-R2", 1));
            Assert.AreEqual(TutorialStep.ArrivalExplanation, game.Tutorial.Step);
            game.SetSecurityViewing(false);

            Assert.IsTrue(game.TryMove("R1", "L01-R2", 1));
            Assert.IsTrue(game.Tutorial.IsComplete);
            CompleteSessionSteps(game);
            Assert.IsTrue(game.Snapshot.IsVictory);
            game.Restart();
            Assert.AreEqual(TutorialStep.AlignFirstBus, game.Tutorial.Step);
            Assert.IsFalse(game.CanSelect("L01-R2"));
            Assert.IsFalse(game.TryMove("R1", "L01-R1", 2));
            Assert.IsTrue(game.TryMove("R1", "L01-R1", 1));
        }

        [Test]
        public void ResetDuringBoardingRestoresFirstInstruction()
        {
            LevelDefinition level = LevelJsonImporter.Parse(_json);
            LevelTutorial tutorial = new LevelTutorial(level);
            BoardSession board = new BoardSession(level);
            Assert.IsTrue(board.TryMoveBus("R1", 1));
            CompleteNext(board, tutorial, BoardStepKind.Move);
            tutorial.Observe(board.PendingStep);
            BoardStep oldBoarding = board.PendingStep;
            Assert.AreEqual(TutorialStep.Boarding, tutorial.Step);
            board.Restart();
            tutorial.Reset();
            Assert.IsFalse(board.TryAcknowledge(oldBoarding.Generation, oldBoarding.Token));
            Assert.AreEqual(TutorialStep.AlignFirstBus, tutorial.Step);
            StringAssert.Contains("красн", tutorial.Text.ToLowerInvariant());
            StringAssert.Contains("бирюз", tutorial.Text.ToLowerInvariant());
            Assert.IsTrue(tutorial.CanMove("L01-R1", 1));
        }

        [Test]
        public void SessionAllowsSafeBusMoveDuringTutorialWalkWithoutReplacingDispatchedStep()
        {
            LevelGameSession game = new LevelGameSession(CreateSpareBusLevel());
            int requestedSteps = 0;
            game.StepRequested += step => requestedSteps++;
            Assert.IsTrue(game.TryMove("R1", "L01-R1", 1));
            game.Complete(game.PendingStep);
            BoardStep walking = game.PendingStep;
            int previousRequests = requestedSteps;
            Assert.AreEqual(BoardStepKind.Board, walking.Kind);
            Assert.IsFalse(game.CanSelect("L01-R1"));
            Assert.IsTrue(game.CanSelect("L01-T"));
            Assert.IsTrue(game.CanMove("R2", "L01-T", 7.2f));
            Assert.IsFalse(game.CanMove("R1", "L01-T", 7));
            Assert.IsTrue(game.GetStopPreview("L01-T", 7).IsAllowed);
            Assert.IsTrue(game.TryMove("R2", "L01-T", 7));
            Assert.AreSame(walking, game.PendingStep);
            Assert.AreEqual(previousRequests, requestedSteps, "A second coroutine must not replace the walk.");
            game.SetPaused(true);
            Assert.IsFalse(game.CanSelect("L01-T"));
            Assert.IsFalse(game.TryMove("R2", "L01-T", 7));
            game.SetPaused(false);
            game.SetSecurityViewing(true);
            Assert.IsFalse(game.CanSelect("L01-T"));
            game.SetSecurityViewing(false);
            game.Complete(walking);
            Assert.AreEqual(1, game.Snapshot.Rows[0].Bus.Occupied);
            Assert.AreEqual(7, game.Snapshot.Rows[1].Bus.LeftX);
        }

        [Test]
        public void SessionRejectsSpoofedRowsUnknownBusesAndAlreadyDepartedBus()
        {
            LevelGameSession game = new LevelGameSession(LevelJsonImporter.Parse(_json));
            Assert.IsFalse(game.CanSelect("missing-bus"));
            Assert.IsFalse(game.TryMove("R2", "L01-R1", 1));
            Assert.AreEqual(0, game.Trace.Count);
            Assert.AreEqual(3, game.Snapshot.Rows[1].Bus.LeftX);
            Assert.IsTrue(game.TryMove("R1", "L01-R1", 1));
            int completedSteps = 0;

            while (game.PendingStep != null && completedSteps < 10)
            {
                BoardStep pending = game.PendingStep;
                game.Complete(pending);
                completedSteps++;
            }

            Assert.IsNull(game.PendingStep);
            Assert.AreEqual(TutorialStep.ArrivalExplanation, game.Tutorial.Step);
            game.ContinueTutorial();
            Assert.IsTrue(game.CanSelect("L01-R2"));
            Assert.IsFalse(game.CanSelect("L01-R1"));
            Assert.IsFalse(game.CanSelect("missing-bus"));
            Assert.IsFalse(game.TryMove("R1", "L01-R1", 1));
            Assert.IsFalse(game.TryMove("R2", "L01-R2", 1));
            Assert.AreEqual("L01-R2", game.Snapshot.Rows[0].Bus.Id);
            Assert.IsNull(game.Snapshot.Rows[1].Bus);
        }

        [Test]
        public void StopPreviewUsesTutorialRestrictionAndActualDoorAccess()
        {
            LevelGameSession game = new LevelGameSession(LevelJsonImporter.Parse(_json));
            BusStopPreview target = game.GetStopPreview("L01-R1", 1);
            Assert.IsTrue(target.IsAllowed);
            Assert.IsTrue(target.CanBoard);
            Assert.AreEqual("A", target.DoorAccess.QueueId);
            Assert.AreEqual("L01-R1", target.DoorAccess.BusId);
            BusStopPreview wrongRoute = game.GetStopPreview("L01-R1", 3);
            Assert.IsFalse(wrongRoute.IsAllowed);
            Assert.IsFalse(wrongRoute.CanBoard);
            Assert.AreEqual(LaneAccessReason.RouteMismatch, wrongRoute.DoorAccess.Reason);
            BusStopPreview otherBus = game.GetStopPreview("L01-T", 3);
            Assert.IsFalse(otherBus.IsAllowed);
            Assert.IsFalse(otherBus.CanBoard);
            Assert.AreEqual(LaneAccessReason.RouteMismatch, otherBus.DoorAccess.Reason);
            Assert.AreEqual("L01-R1", otherBus.DoorAccess.BusId);
            game.SetPaused(true);
            Assert.IsFalse(game.GetStopPreview("L01-R1", 1).IsAllowed);
        }

        [TestCase(-1)]
        [TestCase(11)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void StopPreviewSafelyRejectsOutOfBoundsPosition(int leftX)
        {
            LevelGameSession game = new LevelGameSession(LevelJsonImporter.Parse(_json));
            BusStopPreview preview = game.GetStopPreview("L01-R1", leftX);
            Assert.IsNotNull(preview);
            Assert.AreEqual(leftX, preview.LeftX);
            Assert.IsFalse(preview.IsAllowed);
            Assert.IsFalse(preview.CanBoard);
            Assert.IsNull(preview.DoorAccess);
        }

        [Test]
        public void StopPreviewIsReadOnlyAndMissingBusIsSafe()
        {
            LevelGameSession game = new LevelGameSession(LevelJsonImporter.Parse(_json));
            int generation = game.Snapshot.Generation;
            int changes = 0;
            int requests = 0;
            game.Changed += () => changes++;
            game.StepRequested += step => requests++;
            Assert.IsNull(game.GetStopPreview("missing-bus", 1));
            Assert.IsNull(game.GetStopPreview(null, 1));
            Assert.IsNotNull(game.GetStopPreview("L01-R1", 1));
            Assert.IsNotNull(game.GetStopPreview("L01-R1", 3));
            Assert.IsNotNull(game.GetStopPreview("L01-T", 3));
            Assert.AreEqual(generation, game.Snapshot.Generation);
            Assert.AreEqual(3, game.Snapshot.Rows[0].Bus.LeftX);
            Assert.AreEqual(3, game.Snapshot.Rows[1].Bus.LeftX);
            Assert.AreEqual(0, game.Snapshot.Rows[0].Bus.Occupied);
            Assert.AreEqual(4, game.Snapshot.Queues[0].RemainingPassengers.Count);
            Assert.AreEqual(2, game.Snapshot.Queues[1].RemainingPassengers.Count);
            Assert.IsNull(game.PendingStep);
            Assert.AreEqual(0, game.Trace.Count);
            Assert.AreEqual(0, changes);
            Assert.AreEqual(0, requests);
        }

        [Test]
        public void ActualFirstLevelStartsWithRedBodyBlockingMatchingLowerBus()
        {
            LevelGameSession game = new LevelGameSession(LevelJsonImporter.Parse(_json));
            LaneAccess blocked = game.GetLaneAccess("B");
            Assert.AreEqual("L01-R1", blocked.BusId);
            Assert.AreEqual("R1", blocked.FirstRowId);
            Assert.AreEqual(LaneAccessReason.RouteMismatch, blocked.Reason);
            Assert.IsFalse(blocked.CanBoard);
            BusSnapshot lower = game.Snapshot.Rows[1].Bus;
            Assert.AreEqual("T", lower.Route);
            Assert.AreEqual(game.Snapshot.Queues[1].X, lower.LeftX + 1);
            Assert.AreEqual(0, lower.Occupied);
            Assert.IsNull(game.PendingStep);
            Assert.AreEqual(0, game.Trace.Count);
        }

        [Test]
        public void MovingRedBodyClearOpensLowerBusBeforeAnyDeparture()
        {
            BoardSession board = new BoardSession(LevelJsonImporter.Parse(_json));
            Assert.AreEqual("L01-R1", board.GetLaneAccess("B").BusId);
            Assert.AreEqual(LaneAccessReason.RouteMismatch, board.GetLaneAccess("B").Reason);
            Assert.IsTrue(board.TryMoveBus("R1", 0));
            BoardStep move = board.PendingStep;
            Assert.AreEqual(BoardStepKind.Move, move.Kind);
            Assert.IsTrue(board.TryAcknowledge(move.Generation, move.Token));
            BoardSnapshot snapshot = board.GetSnapshot();
            Assert.AreEqual("L01-R1", snapshot.Rows[0].Bus.Id);
            Assert.AreEqual(0, snapshot.Rows[0].Bus.LeftX);
            Assert.AreEqual(0, snapshot.Rows[0].Bus.Occupied);
            Assert.AreEqual(1, snapshot.NextBusIds.Count);
            Assert.AreEqual("L01-T", board.GetLaneAccess("B").BusId);
            Assert.AreEqual("R2", board.GetLaneAccess("B").FirstRowId);
            Assert.IsTrue(board.GetLaneAccess("B").CanBoard);
            Assert.AreEqual(BoardStepKind.Board, board.PendingStep.Kind);
            Assert.AreEqual("B", board.PendingStep.QueueId);
            Assert.AreEqual("L01-T", board.PendingStep.BusId);
            CollectionAssert.AreEqual(new[] { "Moved:L01-R1:0" }, board.Trace);
        }

        [Test]
        public void RedShiftOpensLowerDoorAndRestartRestoresInitialBlocker()
        {
            LevelGameSession game = new LevelGameSession(LevelJsonImporter.Parse(_json));
            Assert.IsTrue(game.TryMove("R1", "L01-R1", 1));
            BoardStep oldMove = game.PendingStep;
            LaneAccess opened = game.GetLaneAccess("B");
            Assert.AreEqual("L01-T", opened.BusId);
            Assert.AreEqual("R2", opened.FirstRowId);
            Assert.IsTrue(opened.CanBoard);
            CompleteSessionSteps(game);
            Assert.AreEqual(2, game.Snapshot.Queues[0].RemainingPassengers.Count);
            Assert.AreEqual(0, game.Snapshot.Queues[1].RemainingPassengers.Count);
            Assert.IsNull(game.Snapshot.Rows[1].Bus);
            game.ContinueTutorial();
            Assert.IsNull(game.GetStopPreview("L01-R1", 1));
            BusStopPreview wrongDoor = game.GetStopPreview("L01-R2", 0);
            Assert.IsTrue(wrongDoor.IsAllowed);
            Assert.IsFalse(wrongDoor.CanBoard);
            Assert.IsNull(wrongDoor.DoorAccess);
            Assert.IsTrue(game.TryMove("R1", "L01-R2", 1));
            CompleteSessionSteps(game);
            Assert.IsTrue(game.Snapshot.IsVictory);
            CollectionAssert.AreEqual(new[]
            {
                "Moved:L01-R1:1", "Board:L01-A-01", "Board:L01-B-01", "Board:L01-A-02",
                "Board:L01-B-02", "BusDeparted:L01-R1", "BusDeparted:L01-T", "BusArrived:L01-R2",
                "Moved:L01-R2:1", "Board:L01-A-03", "Board:L01-A-04", "BusDeparted:L01-R2", "Victory"
            }, game.Trace);
            game.Restart();
            game.Complete(oldMove);
            Assert.AreEqual(TutorialStep.AlignFirstBus, game.Tutorial.Step);
            Assert.AreEqual(3, game.Snapshot.Rows[0].Bus.LeftX);
            Assert.AreEqual(3, game.Snapshot.Rows[1].Bus.LeftX);
            Assert.AreEqual(2, game.Snapshot.Queues[1].RemainingPassengers.Count);
            Assert.AreEqual("L01-R1", game.GetLaneAccess("B").BusId);
            Assert.AreEqual(LaneAccessReason.RouteMismatch, game.GetLaneAccess("B").Reason);
            Assert.IsNull(game.PendingStep);
            Assert.AreEqual(0, game.Trace.Count);
        }

        [Test]
        public void SessionDispatchesEachLaneOnceAndWaitsForTheBatchBeforeTheNextHeads()
        {
            LevelGameSession game = new LevelGameSession(LevelJsonImporter.Parse(_json));
            List<BoardStep> requests = new List<BoardStep>();
            game.StepRequested += requests.Add;
            Assert.IsTrue(game.TryMove("R1", "L01-R1", 1));
            game.Complete(game.PendingStep);
            Assert.AreEqual(2, game.BoardingSteps.Count);
            BoardStep firstA = game.BoardingSteps[0];
            BoardStep firstB = game.BoardingSteps[1];
            Assert.AreEqual("A", firstA.QueueId);
            Assert.AreEqual("B", firstB.QueueId);
            Assert.AreSame(firstA, game.PendingStep);
            Assert.AreEqual(3, requests.Count);
            game.Publish();
            game.SetPaused(true);
            game.Complete(firstB);
            Assert.AreEqual(0, game.Snapshot.Rows[1].Bus.Occupied);
            game.SetPaused(false);
            game.SetSecurityViewing(true);
            game.SetSecurityViewing(false);
            Assert.AreEqual(3, requests.Count, "Publishing and pause/resume must not restart either walk.");
            game.Complete(firstB);
            Assert.AreEqual(1, game.Snapshot.Rows[1].Bus.Occupied);
            Assert.AreEqual(0, game.Snapshot.Rows[0].Bus.Occupied);
            Assert.AreSame(firstA, game.PendingStep);
            Assert.AreEqual(1, game.BoardingSteps.Count);
            Assert.AreEqual(3, requests.Count, "The completed lane waits for the current batch.");
            game.Complete(firstB);
            game.Publish();
            Assert.AreEqual(3, requests.Count, "Duplicate completions must neither commit nor redispatch.");
            game.Complete(firstA);
            Assert.AreEqual(2, game.BoardingSteps.Count);
            Assert.AreEqual("L01-A-02", game.BoardingSteps[0].PassengerId);
            BoardStep secondB = game.BoardingSteps[1];
            Assert.AreEqual("L01-B-02", secondB.PassengerId);
            Assert.AreEqual(5, requests.Count);
            Assert.AreSame(game.BoardingSteps[0], requests[3]);
            Assert.AreSame(secondB, requests[4]);
            game.Complete(firstA);
            game.Publish();
            Assert.AreEqual(5, requests.Count, "Completed prior-batch tokens cannot redispatch.");
            game.Restart();
            game.Complete(firstA);
            game.Complete(secondB);
            Assert.AreEqual(0, game.Trace.Count);
            Assert.AreEqual(0, game.BoardingSteps.Count);
            Assert.IsNull(game.PendingStep);
            Assert.IsTrue(game.TryMove("R1", "L01-R1", 1));
            game.Complete(game.PendingStep);
            Assert.AreEqual(2, game.BoardingSteps.Count);
            Assert.AreEqual(8, requests.Count, "The next attempt must dispatch its own move and both walks.");
        }

        [Test]
        public void AllArrivalWitnessesPreserveSettledStateAndLaneOrderAcrossCompletionSchedules()
        {
            for (int level = 2; level <= 4; level++)
            {
                for (int variant = 0; variant < 16; variant++)
                {
                    string path = "ArrivalVariants/LEVEL-" + level.ToString("00") + "/v" + variant.ToString("00");
                    TextAsset content = Resources.Load<TextAsset>(path);
                    Assert.IsNotNull(content, path);
                    LevelDefinition definition = LevelJsonImporter.Parse(content.text);
                    AcknowledgementWitness witness = JsonUtility.FromJson<AcknowledgementWitness>(content.text);
                    List<string> baseline = ReplayCompletionSchedule(definition, witness, 0, path);

                    for (int schedule = 1; schedule < 3; schedule++)
                    {
                        CollectionAssert.AreEqual(baseline,
                            ReplayCompletionSchedule(definition, witness, schedule, path),
                            path + " settled state, global events and per-lane chronology; schedule=" + schedule);
                    }
                }
            }
        }

        private static List<string> ReplayCompletionSchedule(LevelDefinition definition,
            AcknowledgementWitness witness, int schedule, string context)
        {
            BoardSession board = new BoardSession(definition);
            List<string> settled = new List<string>();
            int completionIndex = 0;
            Assert.IsNotNull(witness.reference_solution, context);

            foreach (AcknowledgementCommand command in witness.reference_solution)
            {
                Assert.IsNull(board.PendingStep, context);
                bool accepted = false;

                switch (command.action)
                {
                    case "move":
                        accepted = board.TryMoveBus(command.row, command.left_x);
                        break;
                    case "scan":
                        accepted = board.TryScan();
                        break;
                    case "quarantine":
                        accepted = board.TryQuarantine(command.queue);
                        break;
                    default:
                        Assert.Fail(context + " unsupported action: " + command.action);
                        break;
                }

                Assert.IsTrue(accepted, context + " rejected " + command.action);
                int steps = 0;

                while (board.PendingStep != null && steps < 256)
                {
                    BoardStep step = board.PendingStep;

                    if (board.BoardingSteps.Count > 0)
                    {
                        int index = schedule == 1 ? board.BoardingSteps.Count - 1
                            : schedule == 2 ? completionIndex % board.BoardingSteps.Count : 0;
                        step = board.BoardingSteps[index];
                    }

                    Assert.IsTrue(board.TryAcknowledge(step.Generation, step.Token), context);
                    completionIndex++;
                    steps++;
                }

                Assert.IsNull(board.PendingStep, context + " did not settle.");
                settled.Add(GetSettledSignature(board, definition));
            }

            Assert.IsTrue(board.GetSnapshot().IsVictory, context);
            Assert.IsFalse(board.GetSnapshot().IsDefeat, context);
            Assert.AreEqual(definition.InitialStability, board.GetSnapshot().Stability, context);
            Assert.AreEqual(0, board.GetSnapshot().EmergencyResets, context);
            return settled;
        }

        private static string GetSettledSignature(BoardSession board, LevelDefinition definition)
        {
            BoardSnapshot snapshot = board.GetSnapshot();
            string result = snapshot.IsVictory + ":" + snapshot.IsDefeat + ":" + snapshot.HasSecurityCheck
                + ":" + snapshot.CctvCharges + ":" + snapshot.CctvUses + ":" + snapshot.Stability
                + ":" + snapshot.EmergencyResets + ":" + snapshot.LostSeats + ":" + snapshot.IsolationCount
                + ":" + snapshot.LastIsolationResult + "|FIFO:" + string.Join(",", snapshot.NextBusIds);

            foreach (RowSnapshot row in snapshot.Rows)
            {
                result += "|" + row.Id + ":";

                if (row.Bus != null)
                {
                    result += row.Bus.Id + ":" + row.Bus.LeftX + ":" + row.Bus.Occupied + ":" + row.Bus.LostSeats;
                }
            }

            foreach (QueueSnapshot queue in snapshot.Queues)
            {
                result += "|" + queue.Id + ":";

                foreach (PassengerDefinition passenger in queue.RemainingPassengers)
                {
                    result += passenger.Id + ",";
                }
            }

            result += "|Global:";

            foreach (string entry in board.Trace)
            {
                if (entry.StartsWith("Board:", StringComparison.Ordinal) == false)
                {
                    result += entry + ",";
                }
            }

            foreach (QueueDefinition queue in definition.Queues)
            {
                HashSet<string> passengers = new HashSet<string>();

                foreach (PassengerDefinition passenger in queue.Passengers)
                {
                    passengers.Add(passenger.Id);
                }

                result += "|Board " + queue.Id + ":";

                foreach (string entry in board.Trace)
                {
                    if (entry.StartsWith("Board:", StringComparison.Ordinal) && passengers.Contains(entry.Substring(6)))
                    {
                        result += entry + ",";
                    }
                }
            }

            return result;
        }

        private LevelDefinition CreateSpareBusLevel()
        {
            LevelDefinition source = LevelJsonImporter.Parse(_json);
            List<BusDefinition> buses = new List<BusDefinition>();

            foreach (BusDefinition bus in source.Buses)
            {
                buses.Add(new BusDefinition(bus.Id, bus.Route, bus.Class, bus.FootprintCells,
                    bus.Capacity, bus.Id == "L01-T" ? 5 : bus.InitialLeftX));
            }

            return new LevelDefinition(source.Id, source.Width,
                new Dictionary<string, int> { { "A", 2 }, { "B", 4 }, { "C", 6 }, { "D", 8 } },
                source.Queues, buses, source.Rows, source.CctvCharges, source.InitialStability,
                source.TargetCctvUses, source.NextBusIds);
        }

        private static void CompleteSessionSteps(LevelGameSession game)
        {
            int completed = 0;

            while (game.PendingStep != null && completed < 10)
            {
                game.Complete(game.PendingStep);
                completed++;
            }

            Assert.IsNull(game.PendingStep);
        }

        private static void CompleteNext(BoardSession board, LevelTutorial tutorial, BoardStepKind expected)
        {
            BoardStep step = board.PendingStep;
            Assert.IsNotNull(step);
            Assert.AreEqual(expected, step.Kind);
            tutorial.Observe(step);
            Assert.IsTrue(board.TryAcknowledge(step.Generation, step.Token));
            tutorial.Complete(step);
        }

#pragma warning disable CS0649
        [Serializable]
        private class AcknowledgementWitness
        {
            public AcknowledgementCommand[] reference_solution;
        }

        [Serializable]
        private class AcknowledgementCommand
        {
            public string action;
            public string row;
            public int left_x;
            public string queue;
        }
#pragma warning restore CS0649
    }
}
