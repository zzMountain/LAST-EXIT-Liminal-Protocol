using System;
using System.Collections.Generic;
using LastExit.Domain;
using NUnit.Framework;

namespace LastExit.Tests.Gameplay
{
    public class BoardSessionTests
    {
        [Test]
        public void Level01MatchesReferenceTraceAndOnlyWinsAfterLastDeparture()
        {
            BoardSession session = new BoardSession(CreateLevel01());
            Assert.That(session.PendingStep, Is.Null);
            Assert.That(session.GetLaneAccess("A").Reason, Is.EqualTo(LaneAccessReason.NoBus));
            Assert.That(session.GetLaneAccess("C").Reason, Is.EqualTo(LaneAccessReason.EmptyQueue));
            Assert.That(session.GetLaneAccess("D").Reason, Is.EqualTo(LaneAccessReason.EmptyQueue));
            Assert.Throws<ArgumentException>(() => session.GetLaneAccess("E"));

            MoveAndDrain(session, "R1", 1);
            BoardSnapshot afterFirstWave = session.GetSnapshot();
            Assert.That(afterFirstWave.Rows[0].Bus.Id, Is.EqualTo("L01-R2"));
            Assert.That(afterFirstWave.Rows[0].Bus.LeftX, Is.Zero);
            Assert.That(afterFirstWave.Queues[0].RemainingPassengers.Count, Is.EqualTo(2));
            MoveAndDrain(session, "R1", 1);
            Assert.That(session.TryMoveBus("R2", 3), Is.True);
            AdvanceUntil(session, BoardStepKind.Depart);
            BoardSnapshot beforeExit = session.GetSnapshot();
            Assert.That(beforeExit.Queues[0].RemainingPassengers, Is.Empty);
            Assert.That(beforeExit.Queues[1].RemainingPassengers, Is.Empty);
            Assert.That(beforeExit.Rows[1].Bus.Occupied, Is.EqualTo(2));
            Assert.That(beforeExit.IsVictory, Is.False);
            Acknowledge(session);
            Assert.That(session.GetSnapshot().IsVictory, Is.True);
            Assert.That(session.PendingStep.Kind, Is.EqualTo(BoardStepKind.Victory));
            Acknowledge(session);
            Assert.That(session.CanMove, Is.False);
            Assert.That(session.TryMoveBus("R2", 1), Is.False);

            CollectionAssert.AreEqual(new[]
            {
                "Moved:L01-R1:1", "Board:L01-A-01", "Board:L01-A-02",
                "BusDeparted:L01-R1", "BusArrived:L01-R2",
                "Moved:L01-R2:1", "Board:L01-A-03", "Board:L01-A-04", "BusDeparted:L01-R2",
                "Moved:L01-T:3", "Board:L01-B-01", "Board:L01-B-02", "BusDeparted:L01-T", "Victory"
            }, session.Trace);
        }

        [Test]
        public void MovingAnotherBusDuringWalkPreservesPassengerAndAcknowledgment()
        {
            BoardSession session = new BoardSession(CreateLevel01());
            Assert.That(session.TryMoveBus("R1", 1), Is.True);
            Acknowledge(session);
            BoardStep walking = session.PendingStep;
            Assert.That(walking.Kind, Is.EqualTo(BoardStepKind.Board));
            Assert.That(session.CanMove, Is.True);
            Assert.That(session.TryMoveBus("R1", 3), Is.False, "The boarding door must remain stationary.");
            Assert.That(session.TryMoveBus("R2", 6), Is.True);
            Assert.That(session.TryMoveBus("R2", 5), Is.True);
            Assert.That(session.PendingStep, Is.SameAs(walking));
            Assert.That(session.GetSnapshot().Rows[1].Bus.LeftX, Is.EqualTo(5));
            Assert.That(session.GetSnapshot().Rows[0].Bus.Occupied, Is.Zero);
            Assert.That(session.GetSnapshot().Queues[0].RemainingPassengers.Count, Is.EqualTo(4));
            Assert.That(session.Trace, Does.Contain("Moved:L01-T:5"));
            Assert.That(session.TryAcknowledge(walking.Generation, walking.Token), Is.True);
            Assert.That(session.GetSnapshot().Rows[0].Bus.Occupied, Is.EqualTo(1));
            Assert.That(session.TryAcknowledge(walking.Generation, walking.Token), Is.False);
            Drain(session);
            Assert.That(session.GetSnapshot().Rows[1].Bus.LeftX, Is.EqualTo(5));
            session.Restart();
            Assert.That(session.GetSnapshot().Rows[1].Bus.LeftX, Is.Zero);
            Assert.That(session.TryAcknowledge(walking.Generation, walking.Token), Is.False);
        }

        [Test]
        public void UpperBusCannotSweepAcrossWalkingLaneEvenWhenFinalPositionIsClear()
        {
            BoardSession session = new BoardSession(CreateTwoRouteLevel(0, 1));
            BoardStep walking = session.PendingStep;
            Assert.That(walking.Kind, Is.EqualTo(BoardStepKind.Board));
            Assert.That(walking.BusId, Is.EqualTo("bottom-r"));
            Assert.That(session.CanMoveBus("R1", 0), Is.True);
            Assert.That(session.CanMoveBus("R1", .1f), Is.False, "Preview must protect the walking corridor.");
            Assert.That(session.TryMoveBus("R1", 1), Is.False);
            Assert.That(session.TryMoveBus("R1", 3), Is.False, "A clear destination cannot skip the passenger.");
            Assert.That(session.TryMoveBus("R2", 3), Is.False);
            Assert.That(session.CanMoveBus("missing", 0), Is.False);
            Assert.That(session.CanMoveBus("R1", float.NaN), Is.False);
            Assert.That(session.PendingStep, Is.SameAs(walking));
            Assert.That(session.GetSnapshot().Rows[0].Bus.LeftX, Is.Zero);
            Acknowledge(session);
            Acknowledge(session);
            Assert.That(session.PendingStep.Kind, Is.EqualTo(BoardStepKind.Depart));
            Assert.That(session.TryMoveBus("R1", 3), Is.False);
            Acknowledge(session);
            Assert.That(session.TryMoveBus("R1", 3), Is.True, "The corridor unlocks after the completed walk.");
        }

        [Test]
        public void UpperBusCanMoveWithoutCrossingTheCurrentWalkingLane()
        {
            BoardSession session = new BoardSession(CreateTwoRouteLevel(5, 1));
            BoardStep walking = session.PendingStep;
            Assert.That(session.TryMoveBus("R1", 4), Is.True);
            Assert.That(session.TryMoveBus("R1", 6), Is.True);
            Assert.That(session.PendingStep, Is.SameAs(walking));
            Assert.That(session.GetLaneAccess("A").BusId, Is.EqualTo("bottom-r"));
            Assert.That(session.GetSnapshot().Rows[0].Bus.LeftX, Is.EqualTo(6));
        }

        [Test]
        public void IndependentLanesBeginTogetherAndEachOwnsOneReservedSeat()
        {
            BoardSession session = new BoardSession(CreateTwoRouteLevel(3, 1));
            Assert.That(session.BoardingSteps.Count, Is.EqualTo(2));
            Assert.That(session.BoardingSteps[0].QueueId, Is.EqualTo("A"));
            Assert.That(session.BoardingSteps[1].QueueId, Is.EqualTo("B"));
            Assert.That(session.PendingStep, Is.SameAs(session.BoardingSteps[0]));
            Assert.That(session.GetSnapshot().Rows[0].Bus.Occupied, Is.Zero);
            Assert.That(session.GetSnapshot().Rows[1].Bus.Occupied, Is.Zero);
            Assert.That(session.TryScan(), Is.False);
            Assert.That(session.TryQuarantine("A"), Is.False);
            Assert.Throws<NotSupportedException>(() => ((IList<BoardStep>)session.BoardingSteps).Clear());
        }

        [Test]
        public void LaterLaneWaitsForEveryCurrentPassengerBeforeTheNextBeat()
        {
            BoardSession session = new BoardSession(CreateTwoRouteLevel(3, 1));
            BoardStep firstLane = session.BoardingSteps[0];
            BoardStep laterLane = session.BoardingSteps[1];
            Assert.That(session.TryAcknowledge(laterLane.Generation, laterLane.Token), Is.True);
            Assert.That(session.BoardingSteps.Count, Is.EqualTo(1));
            Assert.That(session.PendingStep, Is.SameAs(firstLane));
            Assert.That(session.GetSnapshot().Rows[0].Bus.Occupied, Is.EqualTo(1));
            Assert.That(session.GetSnapshot().Rows[1].Bus.Occupied, Is.Zero);
            Assert.That(session.GetSnapshot().Queues[0].RemainingPassengers[0].Id, Is.EqualTo("a1"));
            Assert.That(session.GetSnapshot().Queues[1].RemainingPassengers[0].Id, Is.EqualTo("b2"));
            Assert.That(session.GetLaneAccess("B").Reason, Is.EqualTo(LaneAccessReason.Ready));
            Assert.That(session.TryAcknowledge(laterLane.Generation, laterLane.Token), Is.False);
            Assert.That(session.TryAcknowledge(firstLane.Generation + 1, firstLane.Token), Is.False);
            Assert.That(session.TryAcknowledge(firstLane.Generation, firstLane.Token), Is.True);
            Assert.That(session.BoardingSteps.Count, Is.EqualTo(2));
            Assert.That(session.BoardingSteps[0].PassengerId, Is.EqualTo("a2"));
            Assert.That(session.BoardingSteps[1].PassengerId, Is.EqualTo("b2"));
        }

        [Test]
        public void FullBusWaitsForTheWholeSecondBatchBeforeDeparting()
        {
            BoardSession session = new BoardSession(CreateTwoRouteLevel(3, 1));
            AcknowledgePassenger(session, "b1");
            AcknowledgePassenger(session, "a1");
            BoardStep firstLane = session.BoardingSteps[0];
            Assert.That(firstLane.PassengerId, Is.EqualTo("a2"));
            AcknowledgePassenger(session, "b2");
            Assert.That(session.BoardingSteps.Count, Is.EqualTo(1));
            Assert.That(session.PendingStep, Is.SameAs(firstLane));
            Assert.That(session.GetSnapshot().Rows[0].Bus.Occupied, Is.EqualTo(2));
            Assert.That(session.Trace, Does.Not.Contain("BusDeparted:top-t"));
            Assert.That(session.TryMoveBus("R1", 5), Is.False);
            AcknowledgePassenger(session, "a2");
            Assert.That(session.BoardingSteps, Is.Empty);
            Assert.That(session.PendingStep.Kind, Is.EqualTo(BoardStepKind.Depart));
            Assert.That(session.PendingStep.BusId, Is.EqualTo("top-t"));
            Drain(session);
            Assert.That(session.GetSnapshot().IsVictory, Is.True);
        }

        [Test]
        public void ThreeLinesWaitAtTheCommonBeatWhenTwoArriveBeforeTheFirst()
        {
            LevelDefinition level = CreateLevel(
                new[]
                {
                    new QueueDefinition("A", new[] { Human("a1", "R"), Human("a2", "R") }),
                    new QueueDefinition("B", new[] { Human("b1", "T"), Human("b2", "T") }),
                    new QueueDefinition("C", new[] { Human("c1", "B"), Human("c2", "B") })
                },
                new[] { Mini("red", "R", 1), Mini("turquoise", "T", 3), Mini("blue", "B", 5) },
                new[] { Row("R1", "red"), Row("R2", "turquoise"), Row("R3", "blue") });
            BoardSession session = new BoardSession(level);
            Assert.That(session.BoardingSteps.Count, Is.EqualTo(3));
            BoardStep firstLane = session.BoardingSteps[0];
            AcknowledgePassenger(session, "b1");
            AcknowledgePassenger(session, "c1");
            Assert.That(session.BoardingSteps.Count, Is.EqualTo(1));
            Assert.That(session.PendingStep, Is.SameAs(firstLane));
            Assert.That(session.GetSnapshot().Rows[1].Bus.Occupied, Is.EqualTo(1));
            Assert.That(session.GetSnapshot().Rows[2].Bus.Occupied, Is.EqualTo(1));
            Assert.That(session.GetSnapshot().Rows[0].Bus.Occupied, Is.Zero);
            AcknowledgePassenger(session, "a1");
            Assert.That(session.BoardingSteps.Count, Is.EqualTo(3));
            Assert.That(session.BoardingSteps[0].PassengerId, Is.EqualTo("a2"));
            Assert.That(session.BoardingSteps[1].PassengerId, Is.EqualTo("b2"));
            Assert.That(session.BoardingSteps[2].PassengerId, Is.EqualTo("c2"));
            AcknowledgePassenger(session, "c2");
            AcknowledgePassenger(session, "b2");
            Assert.That(session.BoardingSteps.Count, Is.EqualTo(1));
            Assert.That(session.PendingStep.PassengerId, Is.EqualTo("a2"));
            Assert.That(session.Trace, Does.Not.Contain("BusDeparted:turquoise"));
            Assert.That(session.TryMoveBus("R2", 7), Is.False);
            Drain(session);
            Assert.That(session.GetSnapshot().IsVictory, Is.True);
            CollectionAssert.AreEqual(new[]
            {
                "Board:b1", "Board:c1", "Board:a1", "Board:c2", "Board:b2", "Board:a2",
                "BusDeparted:red", "BusDeparted:turquoise", "BusDeparted:blue", "Victory"
            }, session.Trace);
        }

        [Test]
        public void CompletionOrderCannotLetArrivalBlockAnIndependentNextHead()
        {
            LevelDefinition level = CreateLevel(
                new[]
                {
                    new QueueDefinition("A", new[] { Human("a1", "R"), Human("a2", "R"),
                        Human("a3", "R"), Human("a4", "R") }),
                    new QueueDefinition("B", new[] { Human("b1", "T"), Human("b2", "T") })
                },
                new[] { Mini("red-first", "R", 1), Mini("red-next", "R", 3), Mini("turquoise", "T", 3) },
                new[] { new RowDefinition("R1", "red-first", new[] { "red-next" }), Row("R2", "turquoise") });
            BoardSession earlySecondLane = new BoardSession(level);
            BoardSession lateSecondLane = new BoardSession(level);
            AcknowledgePassenger(earlySecondLane, "a1");
            AcknowledgePassenger(lateSecondLane, "b1");
            AcknowledgePassenger(earlySecondLane, "b1");
            AcknowledgePassenger(earlySecondLane, "a2");
            AcknowledgePassenger(earlySecondLane, "b2");
            AcknowledgePassenger(lateSecondLane, "a1");
            AcknowledgePassenger(lateSecondLane, "a2");
            Assert.That(lateSecondLane.BoardingSteps.Count, Is.EqualTo(1));
            Assert.That(lateSecondLane.PendingStep.PassengerId, Is.EqualTo("b2"));
            Assert.That(lateSecondLane.GetSnapshot().Rows[0].Bus.Id, Is.EqualTo("red-first"));
            Assert.That(lateSecondLane.GetSnapshot().NextBusIds, Is.EquivalentTo(new[] { "red-next" }));
            AcknowledgePassenger(lateSecondLane, "b2");
            Drain(earlySecondLane);
            Drain(lateSecondLane);
            BoardSnapshot expected = earlySecondLane.GetSnapshot();
            BoardSnapshot actual = lateSecondLane.GetSnapshot();
            Assert.That(actual.Rows[0].Bus.Id, Is.EqualTo(expected.Rows[0].Bus.Id));
            Assert.That(actual.Rows[0].Bus.Id, Is.EqualTo("red-next"));
            Assert.That(actual.Rows[0].Bus.LeftX, Is.EqualTo(expected.Rows[0].Bus.LeftX));
            Assert.That(actual.Rows[0].Bus.Occupied, Is.EqualTo(expected.Rows[0].Bus.Occupied));
            Assert.That(actual.Rows[1].Bus, Is.Null);
            Assert.That(expected.Rows[1].Bus, Is.Null);
            Assert.That(actual.Queues[0].RemainingPassengers.Count,
                Is.EqualTo(expected.Queues[0].RemainingPassengers.Count));
            Assert.That(actual.Queues[0].RemainingPassengers[0].Id, Is.EqualTo("a3"));
            Assert.That(actual.Queues[1].RemainingPassengers, Is.Empty);
            Assert.That(expected.Queues[1].RemainingPassengers, Is.Empty);
            CollectionAssert.AreEqual(expected.NextBusIds, actual.NextBusIds);
            CollectionAssert.AreEqual(GetVehicleTrace(earlySecondLane), GetVehicleTrace(lateSecondLane));
            CollectionAssert.AreEqual(new[]
            {
                "BusDeparted:red-first", "BusDeparted:turquoise", "BusArrived:red-next"
            }, GetVehicleTrace(lateSecondLane));
            MoveAndDrain(earlySecondLane, "R1", 1);
            MoveAndDrain(lateSecondLane, "R1", 1);
            Assert.That(earlySecondLane.GetSnapshot().IsVictory, Is.True);
            Assert.That(lateSecondLane.GetSnapshot().IsVictory, Is.True);
        }

        [Test]
        public void SafeMovementOpensAnotherLaneForTheNextCommonBeat()
        {
            BoardSession session = new BoardSession(CreateLevel01());
            Assert.That(session.TryMoveBus("R1", 1), Is.True);
            Acknowledge(session);
            BoardStep firstLane = session.PendingStep;
            Assert.That(session.BoardingSteps.Count, Is.EqualTo(1));
            Assert.That(session.TryMoveBus("R2", 3), Is.True);
            Assert.That(session.BoardingSteps.Count, Is.EqualTo(1));
            Assert.That(session.PendingStep, Is.SameAs(firstLane));
            Assert.That(session.GetLaneAccess("B").CanBoard, Is.True);
            Assert.That(session.TryAcknowledge(firstLane.Generation, firstLane.Token), Is.True);
            Assert.That(session.GetSnapshot().Rows[0].Bus.Occupied, Is.EqualTo(1));
            Assert.That(session.BoardingSteps.Count, Is.EqualTo(2));
            Assert.That(session.BoardingSteps[0].PassengerId, Is.EqualTo("L01-A-02"));
            Assert.That(session.BoardingSteps[1].QueueId, Is.EqualTo("B"));
            Assert.That(session.TryMoveBus("R2", 5), Is.False);
        }

        [Test]
        public void EveryActiveBusAndEveryUpperSweptCorridorAreProtected()
        {
            LevelDefinition level = CreateLevel(
                new[]
                {
                    new QueueDefinition("A", new[] { Human("a1", "R"), Human("a2", "R") }),
                    new QueueDefinition("B", new[] { Human("b1", "T"), Human("b2", "T") }),
                    new QueueDefinition("C", new[] { Human("c1", "B"), Human("c2", "B") })
                },
                new[] { Mini("blue", "B", 8), Mini("red", "R", 1), Mini("turquoise", "T", 3) },
                new[] { Row("R1", "blue"), Row("R2", "red"), Row("R3", "turquoise") });
            BoardSession session = new BoardSession(level);
            Assert.That(session.BoardingSteps.Count, Is.EqualTo(2));
            Assert.That(session.TryMoveBus("R2", 0), Is.False);
            Assert.That(session.TryMoveBus("R3", 5), Is.False);
            Assert.That(session.CanMoveBus("R1", 5), Is.True);
            Assert.That(session.CanMoveBus("R1", 3), Is.False, "Protect the later active corridor B.");
            Assert.That(session.CanMoveBus("R1", 0), Is.False, "Sweeps cannot cross either active corridor.");
            Assert.That(session.CanMoveBus("R1", 4.1f), Is.True);
            Assert.That(session.CanMoveBus("R1", 4), Is.False);
        }

        [Test]
        public void RestartCancelsEveryConcurrentCommandAndRejectsAllOldTokens()
        {
            BoardSession session = new BoardSession(CreateTwoRouteLevel(3, 1));
            BoardStep oldFirst = session.BoardingSteps[0];
            BoardStep oldSecond = session.BoardingSteps[1];
            session.Restart();
            Assert.That(session.BoardingSteps.Count, Is.EqualTo(2));
            Assert.That(session.TryAcknowledge(oldFirst.Generation, oldFirst.Token), Is.False);
            Assert.That(session.TryAcknowledge(oldSecond.Generation, oldSecond.Token), Is.False);
            Assert.That(session.TryAcknowledge(session.PendingStep.Generation, oldSecond.Token), Is.False);
            Assert.That(session.GetSnapshot().Rows[0].Bus.Occupied, Is.Zero);
            Assert.That(session.GetSnapshot().Rows[1].Bus.Occupied, Is.Zero);
            Assert.That(session.Trace, Is.Empty);
        }

        [Test]
        public void EchoExposedByOneLaneWaitsForOthersThenStopsRefillForSecurity()
        {
            LevelDefinition level = CreateLevel(
                new[]
                {
                    new QueueDefinition("A", new[] { Human("a1", "R"), Human("a2", "R") }),
                    new QueueDefinition("B", new[] { Human("b1", "T"),
                        new PassengerDefinition("echo-b", "T", "echo"), Human("b2", "T") })
                },
                new[] { Mini("red", "R", 1), Mini("turquoise", "T", 3) },
                new[] { Row("R1", "red"), Row("R2", "turquoise") });
            BoardSession session = new BoardSession(level);
            BoardStep firstLane = session.BoardingSteps[0];
            BoardStep laterLane = session.BoardingSteps[1];
            Assert.That(session.TryAcknowledge(laterLane.Generation, laterLane.Token), Is.True);
            Assert.That(session.BoardingSteps.Count, Is.EqualTo(1));
            Assert.That(session.GetSnapshot().HasSecurityCheck, Is.False);
            Assert.That(session.TryAcknowledge(firstLane.Generation, firstLane.Token), Is.True);
            Assert.That(session.BoardingSteps, Is.Empty);
            Assert.That(session.PendingStep, Is.Null);
            Assert.That(session.GetSnapshot().HasSecurityCheck, Is.True);
            Assert.That(session.GetSnapshot().Queues[0].RemainingPassengers[0].Id, Is.EqualTo("a2"));
            Assert.That(session.TryQuarantine("B"), Is.True);
            Drain(session);
            Assert.That(session.GetSnapshot().IsVictory, Is.True);
            Assert.That(session.GetSnapshot().Stability, Is.EqualTo(3));
        }

        [Test]
        public void InitialEchoInLaterLaneAllowsEarlierWalkThenStopsItsRefill()
        {
            LevelDefinition level = CreateLevel(
                new[]
                {
                    new QueueDefinition("A", new[] { Human("a1", "R"), Human("a2", "R") }),
                    new QueueDefinition("B", new[] { new PassengerDefinition("echo-b", "T", "echo"),
                        Human("b1", "T"), Human("b2", "T") })
                },
                new[] { Mini("red", "R", 1), Mini("turquoise", "T", 3) },
                new[] { Row("R1", "red"), Row("R2", "turquoise") });
            BoardSession session = new BoardSession(level);
            Assert.That(session.BoardingSteps.Count, Is.EqualTo(1));
            Assert.That(session.PendingStep.PassengerId, Is.EqualTo("a1"));
            Assert.That(session.GetSnapshot().HasSecurityCheck, Is.False);
            Acknowledge(session);
            Assert.That(session.BoardingSteps, Is.Empty);
            Assert.That(session.GetSnapshot().HasSecurityCheck, Is.True);
            CollectionAssert.AreEqual(new[] { "Board:a1", "SecurityCheck:echo-b" }, session.Trace);
            Assert.That(session.GetSnapshot().Queues[0].RemainingPassengers[0].Id, Is.EqualTo("a2"));
        }

        [Test]
        public void BoardingAndFifoWaitForTheirOwnAnimationAcknowledgments()
        {
            BoardSession session = new BoardSession(CreateLevel01());
            Assert.That(session.TryMoveBus("R1", 1), Is.True);
            Assert.That(session.TryMoveBus("R2", 3), Is.False);
            Acknowledge(session);
            Assert.That(session.PendingStep.Kind, Is.EqualTo(BoardStepKind.Board));
            Assert.That(session.GetSnapshot().Rows[0].Bus.Occupied, Is.Zero);
            Assert.That(session.GetSnapshot().Queues[0].RemainingPassengers.Count, Is.EqualTo(4));
            Acknowledge(session);
            Assert.That(session.GetSnapshot().Rows[0].Bus.Occupied, Is.EqualTo(1));
            Acknowledge(session);
            Assert.That(session.PendingStep.Kind, Is.EqualTo(BoardStepKind.Depart));
            Assert.That(session.GetSnapshot().Rows[0].Bus.Id, Is.EqualTo("L01-R1"));
            Assert.That(session.GetSnapshot().NextBusIds.Count, Is.EqualTo(1));
            Assert.That(session.GetLaneAccess("A").Reason, Is.EqualTo(LaneAccessReason.Full));
            BoardStep departure = session.PendingStep;
            Acknowledge(session);
            Assert.That(session.PendingStep.Kind, Is.EqualTo(BoardStepKind.Arrive));
            Assert.That(session.GetSnapshot().Rows[0].Bus.Id, Is.EqualTo("L01-R2"));
            Assert.That(session.GetSnapshot().NextBusIds, Is.Empty);
            Assert.That(session.Trace, Does.Not.Contain("BusArrived:L01-R2"));
            Assert.That(session.TryMoveBus("R1", 1), Is.False);
            Assert.That(session.TryAcknowledge(departure.Generation, departure.Token), Is.False);
            Acknowledge(session);
            Assert.That(session.Trace, Does.Contain("BusArrived:L01-R2"));
            Assert.That(session.CanMove, Is.True);
        }

        [TestCase(BoardStepKind.Move)]
        [TestCase(BoardStepKind.Board)]
        [TestCase(BoardStepKind.Depart)]
        [TestCase(BoardStepKind.Arrive)]
        [TestCase(BoardStepKind.Victory)]
        public void RestartDuringEveryPhaseInvalidatesOldAcknowledgments(BoardStepKind interruptedKind)
        {
            BoardSession session = new BoardSession(CreateLevel01());

            if (interruptedKind == BoardStepKind.Victory)
            {
                MoveAndDrain(session, "R1", 1);
                MoveAndDrain(session, "R1", 1);
                Assert.That(session.TryMoveBus("R2", 3), Is.True);
            }
            else
            {
                Assert.That(session.TryMoveBus("R1", 1), Is.True);
            }

            AdvanceUntil(session, interruptedKind);
            BoardStep stale = session.PendingStep;
            session.Restart();
            Assert.That(session.GetSnapshot().Generation, Is.GreaterThan(stale.Generation));
            Assert.That(session.TryAcknowledge(stale.Generation, stale.Token), Is.False);
            AssertInitialLevel01(session.GetSnapshot());
            Assert.That(session.Trace, Is.Empty);
            Assert.That(session.CanMove, Is.True);
            Assert.That(session.TryMoveBus("R1", 1), Is.True);
            BoardStep fresh = session.PendingStep;
            Assert.That(session.TryAcknowledge(stale.Generation, fresh.Token), Is.False);
            Assert.That(session.TryAcknowledge(fresh.Generation, stale.Token), Is.False);
            Assert.That(session.PendingStep, Is.SameAs(fresh));
        }

        [Test]
        public void FirstMismatchedBodyBlocksMatchingBusBelowAndResolveRestartsFromA()
        {
            BoardSession session = new BoardSession(CreateTwoRouteLevel(1, 1));
            LaneAccess access = session.GetLaneAccess("A");
            Assert.That(access.FirstRowId, Is.EqualTo("R1"));
            Assert.That(access.BusId, Is.EqualTo("top-t"));
            Assert.That(access.Reason, Is.EqualTo(LaneAccessReason.RouteMismatch));
            Assert.That(session.PendingStep, Is.Null);
            MoveAndDrain(session, "R1", 3);
            Assert.That(session.GetSnapshot().IsVictory, Is.True);
            CollectionAssert.AreEqual(new[]
            {
                "Moved:top-t:3", "Board:a1", "Board:b1", "Board:a2", "Board:b2",
                "BusDeparted:top-t", "BusDeparted:bottom-r", "Victory"
            }, session.Trace);
        }

        [Test]
        public void MisalignedDoorBlocksLowerBusEvenWhenLowerDoorMatches()
        {
            BoardSession session = new BoardSession(CreateTwoRouteLevel(2, 1));
            Assert.That(session.GetLaneAccess("A").Reason, Is.EqualTo(LaneAccessReason.DoorMisaligned));
            Assert.That(session.GetSnapshot().Queues[0].RemainingPassengers.Count, Is.EqualTo(2));
            MoveAndDrain(session, "R1", 5);
            Assert.That(session.GetSnapshot().Rows[1].Bus, Is.Null);
            Assert.That(session.GetSnapshot().Queues[0].RemainingPassengers, Is.Empty);
        }

        [Test]
        public void PartialBusRemainsMovableAndCarriesItsOccupancyAcrossMoves()
        {
            LevelDefinition definition = CreateLevel(
                new[]
                {
                    new QueueDefinition("A", new[] { Human("a1", "R"), Human("a2", "T") }),
                    new QueueDefinition("B", new[] { Human("b1", "R"), Human("b2", "T") })
                },
                new[] { Mini("r", "R", 1), Mini("t", "T", 0) },
                new[] { Row("R1", "r"), Row("R2", "t") });
            BoardSession session = new BoardSession(definition);
            Drain(session);
            Assert.That(session.CanMove, Is.True);
            Assert.That(session.GetSnapshot().Rows[0].Bus.Occupied, Is.EqualTo(1));
            Assert.That(session.TryMoveBus("R1", 3), Is.True);
            Assert.That(session.GetSnapshot().Rows[0].Bus.Occupied, Is.EqualTo(1));
            Drain(session);
            Assert.That(session.GetSnapshot().Rows[0].Bus, Is.Null);
            MoveAndDrain(session, "R2", 1);
            Assert.That(session.GetSnapshot().Rows[1].Bus.Occupied, Is.EqualTo(1));
            MoveAndDrain(session, "R2", 3);
            Assert.That(session.GetSnapshot().IsVictory, Is.True);
        }

        [Test]
        public void ArrivalBlocksLowerBusBeforeResolveCanResume()
        {
            LevelDefinition definition = CreateLevel(
                new[]
                {
                    new QueueDefinition("A", new[] { Human("a1", "R"), Human("a2", "R"),
                        Human("a3", "R"), Human("a4", "R") }),
                    new QueueDefinition("B", new[] { Human("b1", "T"), Human("b2", "T") })
                },
                new[] { Mini("red-first", "R", 2), Mini("turquoise-next", "T", 1), Mini("red-low", "R", 1) },
                new[] { new RowDefinition("R1", "red-first", new[] { "turquoise-next" }), Row("R2", "red-low") });
            BoardSession session = new BoardSession(definition);
            Assert.That(session.TryMoveBus("R1", 1), Is.True);
            AdvanceUntil(session, BoardStepKind.Arrive);
            Assert.That(session.GetSnapshot().Queues[0].RemainingPassengers.Count, Is.EqualTo(2));
            Assert.That(session.GetLaneAccess("A").BusId, Is.EqualTo("turquoise-next"));
            Acknowledge(session);
            Assert.That(session.PendingStep, Is.Null);
            Assert.That(session.GetLaneAccess("A").Reason, Is.EqualTo(LaneAccessReason.RouteMismatch));
            Assert.That(session.GetSnapshot().Rows[1].Bus.Occupied, Is.Zero);
        }

        [Test]
        public void InvalidMovesAndRepeatedAcknowledgmentsLeaveStateUnchanged()
        {
            BoardSession session = new BoardSession(CreateLevel01());
            Assert.That(session.TryMoveBus("R0", 1), Is.False);
            Assert.That(session.TryMoveBus("R1", -1), Is.False);
            Assert.That(session.TryMoveBus("R1", 11), Is.False);
            AssertInitialLevel01(session.GetSnapshot());
            Assert.That(session.TryAcknowledge(0, 0), Is.False);
            Assert.That(session.TryMoveBus("R1", 10), Is.True);
            BoardStep step = session.PendingStep;
            Acknowledge(session);
            Assert.That(session.TryAcknowledge(step.Generation, step.Token), Is.False);
            Assert.That(session.Trace.Count, Is.EqualTo(1));
        }

        [TestCase(1, LaneAccessReason.Ready)]
        [TestCase(2, LaneAccessReason.DoorMisaligned)]
        public void PreviewPredictsDoorAlignmentWithoutMovingBus(int leftX, LaneAccessReason expected)
        {
            BoardSession session = new BoardSession(CreateLevel01());
            LaneAccess preview = session.PreviewLaneAccess("A", "R1", leftX);
            Assert.That(preview.QueueId, Is.EqualTo("A"));
            Assert.That(preview.FirstRowId, Is.EqualTo("R1"));
            Assert.That(preview.BusId, Is.EqualTo("L01-R1"));
            Assert.That(preview.Reason, Is.EqualTo(expected));
            Assert.That(preview.CanBoard, Is.EqualTo(expected == LaneAccessReason.Ready));
            AssertInitialLevel01(session.GetSnapshot());
            Assert.That(session.GetLaneAccess("A").Reason, Is.EqualTo(LaneAccessReason.NoBus));
        }

        [Test]
        public void PreviewKeepsUpperBodyAsBlockerForMatchingLowerBus()
        {
            BoardSession session = new BoardSession(CreateLevel01());
            Assert.That(session.PreviewLaneAccess("B", "R2", 3).CanBoard, Is.True);
            MoveAndDrain(session, "R1", 3);
            LaneAccess blocked = session.PreviewLaneAccess("B", "R2", 3);
            Assert.That(blocked.FirstRowId, Is.EqualTo("R1"));
            Assert.That(blocked.BusId, Is.EqualTo("L01-R1"));
            Assert.That(blocked.Reason, Is.EqualTo(LaneAccessReason.RouteMismatch));
            Assert.That(blocked.CanBoard, Is.False);
            Assert.That(session.GetSnapshot().Rows[1].Bus.LeftX, Is.Zero);
            Assert.That(session.GetSnapshot().Queues[1].RemainingPassengers.Count, Is.EqualTo(2));
        }

        [Test]
        public void PreviewDoesNotChangeTracePendingStepOrFutureTokens()
        {
            BoardSession session = new BoardSession(CreateLevel01());
            BoardSession control = new BoardSession(CreateLevel01());
            int generation = session.GetSnapshot().Generation;

            for (int leftX = 0; leftX <= 10; leftX++)
            {
                session.PreviewLaneAccess("A", "R1", leftX);
                session.PreviewLaneAccess("B", "R2", leftX);
                Assert.That(session.PreviewLaneAccess("C", "R1", leftX).Reason,
                    Is.EqualTo(LaneAccessReason.EmptyQueue));
            }

            AssertInitialLevel01(session.GetSnapshot());
            Assert.That(session.GetSnapshot().Generation, Is.EqualTo(generation));
            Assert.That(session.Trace, Is.Empty);
            Assert.That(session.PendingStep, Is.Null);
            Assert.That(session.CanMove, Is.True);
            Assert.That(session.TryMoveBus("R1", 1), Is.True);
            Assert.That(control.TryMoveBus("R1", 1), Is.True);
            Assert.That(session.PendingStep.Token, Is.EqualTo(control.PendingStep.Token));
            Acknowledge(session);
            Acknowledge(control);
            BoardStep pending = session.PendingStep;
            session.PreviewLaneAccess("A", "R1", 2);
            session.PreviewLaneAccess("B", "R2", 3);
            Assert.That(session.PendingStep, Is.SameAs(pending));
            Assert.That(session.GetSnapshot().Rows[0].Bus.Occupied, Is.Zero);
            Assert.That(session.GetSnapshot().Queues[0].RemainingPassengers.Count, Is.EqualTo(4));
            CollectionAssert.AreEqual(control.Trace, session.Trace);
            Acknowledge(session);
            Acknowledge(control);
            Assert.That(session.PendingStep.Token, Is.EqualTo(control.PendingStep.Token));
            CollectionAssert.AreEqual(control.Trace, session.Trace);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void PreviewMatchesAccessAfterTheSameMoveIsCommitted(int leftX)
        {
            BoardSession session = new BoardSession(CreateLevel01());
            LaneAccess expectedA = session.PreviewLaneAccess("A", "R1", leftX);
            LaneAccess expectedB = session.PreviewLaneAccess("B", "R1", leftX);
            Assert.That(session.TryMoveBus("R1", leftX), Is.True);
            Acknowledge(session);
            LaneAccess actualA = session.GetLaneAccess("A");
            LaneAccess actualB = session.GetLaneAccess("B");
            Assert.That(actualA.Reason, Is.EqualTo(expectedA.Reason));
            Assert.That(actualA.BusId, Is.EqualTo(expectedA.BusId));
            Assert.That(actualA.FirstRowId, Is.EqualTo(expectedA.FirstRowId));
            Assert.That(actualB.Reason, Is.EqualTo(expectedB.Reason));
            Assert.That(actualB.BusId, Is.EqualTo(expectedB.BusId));

            if (expectedA.CanBoard)
            {
                Assert.That(session.PendingStep.Kind, Is.EqualTo(BoardStepKind.Board));
                Assert.That(session.PendingStep.QueueId, Is.EqualTo("A"));
            }
            else
            {
                Assert.That(session.PendingStep, Is.Null);
            }
        }

        [Test]
        public void PreviewPreservesFullAndArrivalAvailabilityRules()
        {
            BoardSession session = new BoardSession(CreateLevel01());
            Assert.That(session.TryMoveBus("R1", 1), Is.True);
            AdvanceUntil(session, BoardStepKind.Depart);
            Assert.That(session.PreviewLaneAccess("A", "R1", 1).Reason, Is.EqualTo(LaneAccessReason.Full));
            Acknowledge(session);
            Assert.That(session.PendingStep.Kind, Is.EqualTo(BoardStepKind.Arrive));
            Assert.That(session.PreviewLaneAccess("A", "R1", 1).Reason,
                Is.EqualTo(LaneAccessReason.Unavailable));
            Assert.That(session.GetSnapshot().Rows[0].Bus.LeftX, Is.Zero);
        }

        [Test]
        public void PreviewRejectsUnknownRowsOutOfBoundsAndDepartedBuses()
        {
            BoardSession session = new BoardSession(CreateLevel01());
            Assert.Throws<ArgumentException>(() => session.PreviewLaneAccess("A", "R0", 1));
            Assert.Throws<ArgumentException>(() => session.PreviewLaneAccess("A", "R1", -1));
            Assert.Throws<ArgumentException>(() => session.PreviewLaneAccess("A", "R1", 11));
            Assert.Throws<ArgumentException>(() => session.PreviewLaneAccess("E", "R1", 1));
            AssertInitialLevel01(session.GetSnapshot());
            Assert.That(session.Trace, Is.Empty);
            MoveAndDrain(session, "R1", 1);
            MoveAndDrain(session, "R1", 1);
            int traceCount = session.Trace.Count;
            Assert.Throws<ArgumentException>(() => session.PreviewLaneAccess("A", "R1", 1));
            Assert.That(session.Trace.Count, Is.EqualTo(traceCount));
            Assert.That(session.GetSnapshot().Rows[0].Bus, Is.Null);
        }

        [Test]
        public void SnapshotsStayIndependentAcrossMovementBoardingAndRestart()
        {
            BoardSession session = new BoardSession(CreateLevel01());
            BoardSnapshot initial = session.GetSnapshot();
            MoveAndDrain(session, "R1", 1);
            BoardSnapshot afterArrival = session.GetSnapshot();
            session.Restart();
            AssertInitialLevel01(initial);
            Assert.That(afterArrival.Rows[0].Bus.Id, Is.EqualTo("L01-R2"));
            Assert.That(afterArrival.Queues[0].RemainingPassengers.Count, Is.EqualTo(2));
            AssertInitialLevel01(session.GetSnapshot());
            Assert.Throws<NotSupportedException>(() =>
                ((IList<RowSnapshot>)initial.Rows).Clear());
        }

        [Test]
        public void ConfigOwnsItsCollectionsAndRejectsUnsupportedContent()
        {
            List<PassengerDefinition> passengers = new List<PassengerDefinition> { Human("h", "R") };
            QueueDefinition queue = new QueueDefinition("A", passengers);
            passengers.Clear();
            Assert.That(queue.Passengers.Count, Is.EqualTo(1));
            Assert.Throws<ArgumentException>(() => new PassengerDefinition("e", "R", "unknown"));
            Assert.Throws<ArgumentException>(() => new BusDefinition("bad", "R", "MINI", 2, 1, 0));
        }

        [Test]
        public void SecurityCheckStopsBoardingUntilExplicitQuarantineAndScanPreservesChoice()
        {
            BoardSession session = new BoardSession(CreateEchoLevel());
            BoardSnapshot initial = session.GetSnapshot();
            Assert.That(initial.Queues[0].RemainingPassengers[0].Kind, Is.EqualTo("human"));
            Assert.That(session.PreviewLaneAccess("A", "R1", 1).Reason, Is.EqualTo(LaneAccessReason.Ready));
            MoveAndDrain(session, "R1", 1);
            Assert.That(session.GetSnapshot().HasSecurityCheck, Is.True);
            Assert.That(session.PendingStep, Is.Null);
            Assert.That(session.CanMove, Is.False);
            Assert.That(session.TryMoveBus("R2", 3), Is.False);
            Assert.That(session.TryQuarantine("E"), Is.False);
            Assert.That(session.GetSnapshot().Rows[0].Bus.Occupied, Is.Zero);
            Assert.That(session.TryScan(), Is.True);
            Assert.That(session.TryScan(), Is.False);
            Assert.That(session.GetSnapshot().CctvCharges, Is.EqualTo(1));
            Acknowledge(session);
            BoardSnapshot scanned = session.GetSnapshot();
            Assert.That(scanned.HasSecurityCheck, Is.True);
            Assert.That(scanned.CctvCharges, Is.Zero);
            Assert.That(scanned.CctvUses, Is.EqualTo(1));
            Assert.That(session.IsCctvAnomaly("A"), Is.True);
            Assert.That(scanned.Queues[0].RemainingPassengers[0].Kind, Is.EqualTo("human"));
            Assert.That(session.GetLaneAccess("A").Reason, Is.EqualTo(LaneAccessReason.Ready));
            Assert.That(session.TryScan(), Is.False);
            Assert.That(session.TryQuarantine("E"), Is.False);
            Assert.That(session.TryQuarantine("A"), Is.True);
            Assert.That(session.GetSnapshot().Queues[0].RemainingPassengers.Count, Is.EqualTo(3));
            Acknowledge(session);
            Assert.That(session.GetSnapshot().Rows[0].Bus.Occupied, Is.Zero);
            Assert.That(session.GetSnapshot().Queues[0].RemainingPassengers.Count, Is.EqualTo(2));
            Drain(session);
            MoveAndDrain(session, "R2", 3);
            Assert.That(session.GetSnapshot().IsVictory, Is.True);
            Assert.That(session.GetSnapshot().Stability, Is.EqualTo(3));
            Assert.That(session.GetSnapshot().EmergencyResets, Is.Zero);
            Assert.That(initial.Queues[0].RemainingPassengers[0].Kind, Is.EqualTo("human"),
                "Old snapshots must stay neutral and independent.");
            CollectionAssert.AreEqual(new[]
            {
                "Moved:red:1", "SecurityCheck:echo-a", "CctvViewed", "Quarantined:echo-a",
                "Board:a1", "Board:a2", "BusDeparted:red", "Moved:turquoise:3",
                "Board:b1", "Board:b2", "BusDeparted:turquoise", "Victory"
            }, session.Trace);
        }

        [Test]
        public void ScanKeepsSnapshotsNeutralWhileCctvReadsOnlyCurrentHeads()
        {
            BoardSession session = new BoardSession(CreateEchoLevel(extraEchoes: true));
            Assert.That(session.TryQuarantine("E"), Is.False);
            Assert.That(session.TryScan(), Is.True);
            Drain(session);
            BoardSnapshot scanned = session.GetSnapshot();
            Assert.That(scanned.Queues[0].RemainingPassengers[0].Kind, Is.EqualTo("human"));
            Assert.That(session.IsCctvAnomaly("A"), Is.True);
            Assert.That(scanned.Queues[1].RemainingPassengers[0].Kind, Is.EqualTo("human"));
            Assert.That(session.IsCctvAnomaly("B"), Is.True);
            Assert.That(scanned.Queues[0].RemainingPassengers[1].Kind, Is.EqualTo("human"));
            Assert.That(session.Trace, Does.Not.Contain("Revealed:echo-hidden"));
            Assert.That(session.TryQuarantine("A"), Is.True);
            Drain(session);
            Assert.That(session.GetSnapshot().Queues[0].RemainingPassengers[0].Kind, Is.EqualTo("human"));
            Assert.That(session.TryQuarantine("E"), Is.False);
            Assert.That(session.TryQuarantine("B"), Is.True);
            Drain(session);
            MoveAndDrain(session, "R1", 1);
            Assert.That(session.GetSnapshot().HasSecurityCheck, Is.True);
            Assert.That(session.GetSnapshot().Rows[0].Bus.Occupied, Is.Zero);
        }

        [TestCase(3, false)]
        [TestCase(1, true)]
        public void RiskIsolatesWithoutTakingSeatAndDefeatsOnlyAtZero(int stability, bool isDefeat)
        {
            BoardSession session = new BoardSession(CreateEchoLevel(stability));
            Assert.That(session.TryAcceptRisk(), Is.False);
            MoveAndDrain(session, "R1", 1);
            Assert.That(session.TryAcceptRisk(), Is.True);
            Assert.That(session.TryAcceptRisk(), Is.False);
            Assert.That(session.GetSnapshot().Stability, Is.EqualTo(stability));
            Acknowledge(session);
            BoardSnapshot reset = session.GetSnapshot();
            Assert.That(reset.Stability, Is.EqualTo(stability - 1));
            Assert.That(reset.EmergencyResets, Is.EqualTo(1));
            Assert.That(reset.IsDefeat, Is.EqualTo(isDefeat));
            Assert.That(reset.HasSecurityCheck, Is.False);
            Assert.That(reset.Rows[0].Bus.Id, Is.EqualTo("red"));
            Assert.That(reset.Rows[0].Bus.Route, Is.EqualTo("R"));
            Assert.That(reset.Rows[0].Bus.Occupied, Is.Zero);
            Assert.That(reset.Queues[0].RemainingPassengers.Count, Is.EqualTo(2));
            Assert.That(reset.CctvCharges, Is.EqualTo(1));
            Drain(session);

            if (isDefeat)
            {
                Assert.That(session.TryMoveBus("R2", 3), Is.False);
                Assert.That(session.TryScan(), Is.False);
                Assert.That(session.TryQuarantine("E"), Is.False);
                Assert.That(session.Trace, Does.Contain("Defeat"));
            }
            else
            {
                MoveAndDrain(session, "R2", 3);
                Assert.That(session.GetSnapshot().IsVictory, Is.True);
            }
        }

        [TestCase(BoardStepKind.Scan)]
        [TestCase(BoardStepKind.Quarantine)]
        [TestCase(BoardStepKind.Corruption)]
        [TestCase(BoardStepKind.Defeat)]
        public void RestartRestoresSecurityResourcesAndRejectsStaleSteps(BoardStepKind kind)
        {
            BoardSession session = new BoardSession(CreateEchoLevel(1));
            MoveAndDrain(session, "R1", 1);

            if (kind == BoardStepKind.Scan || kind == BoardStepKind.Quarantine)
            {
                Assert.That(session.TryScan(), Is.True);

                if (kind == BoardStepKind.Quarantine)
                {
                    Drain(session);
                    Assert.That(session.TryQuarantine("A"), Is.True);
                }
            }
            else
            {
                Assert.That(session.TryAcceptRisk(), Is.True);

                if (kind == BoardStepKind.Defeat)
                {
                    Acknowledge(session);
                }
            }

            BoardStep stale = session.PendingStep;
            Assert.That(stale.Kind, Is.EqualTo(kind));
            session.Restart();
            Assert.That(session.TryAcknowledge(stale.Generation, stale.Token), Is.False);
            AssertEchoInitial(session.GetSnapshot());
            Assert.That(session.Trace, Is.Empty);
            Assert.That(session.CanMove, Is.True);
        }

        [Test]
        public void RestartFromSecurityCheckRestoresInitialAttempt()
        {
            BoardSession session = new BoardSession(CreateEchoLevel(1));
            MoveAndDrain(session, "R1", 1);
            Assert.That(session.GetSnapshot().HasSecurityCheck, Is.True);
            session.Restart();
            AssertEchoInitial(session.GetSnapshot());
            Assert.That(session.TryAcceptRisk(), Is.False);
        }

        [Test]
        public void LevelFifoArrivesInFirstFreeRowRegardlessOfLegacyRowOwnership()
        {
            BoardSession session = new BoardSession(CreateLevel01());
            MoveAndDrain(session, "R2", 3);
            BoardSnapshot afterLowerExit = session.GetSnapshot();
            Assert.That(afterLowerExit.Rows[0].Bus.Id, Is.EqualTo("L01-R1"));
            Assert.That(afterLowerExit.Rows[1].Bus.Id, Is.EqualTo("L01-R2"));
            Assert.That(afterLowerExit.NextBusIds, Is.Empty);
            MoveAndDrain(session, "R1", 1);
            Assert.That(session.GetSnapshot().Rows[0].Bus, Is.Null);
            MoveAndDrain(session, "R2", 1);
            Assert.That(session.GetSnapshot().IsVictory, Is.True);
        }

        [Test]
        public void HumanQuarantineCommitsPenaltyAndLostSeatOnlyAfterEffectAcknowledgment()
        {
            BoardSession session = new BoardSession(CreateLevel01());
            Assert.That(session.TryQuarantine("A"), Is.True);
            Assert.That(session.GetSnapshot().Stability, Is.EqualTo(3));
            Assert.That(session.GetSnapshot().LostSeats, Is.Zero);
            Assert.That(session.GetSnapshot().LastIsolationResult, Is.Null);
            Acknowledge(session);
            BoardSnapshot snapshot = session.GetSnapshot();
            Assert.That(snapshot.Stability, Is.EqualTo(2));
            Assert.That(snapshot.LostSeats, Is.EqualTo(1));
            Assert.That(snapshot.LastIsolationResult, Is.EqualTo("HumanLost"));
            Assert.That(snapshot.Rows[0].Bus.LostSeats, Is.EqualTo(1));
            Assert.That(snapshot.Rows[0].Bus.Occupied, Is.Zero);
            MoveAndDrain(session, "R1", 1);
            MoveAndDrain(session, "R1", 1);
            MoveAndDrain(session, "R2", 3);
            Assert.That(session.GetSnapshot().IsVictory, Is.True);
            Assert.That(session.GetSnapshot().LostSeats, Is.EqualTo(1));
            session.Restart();
            Assert.That(session.GetSnapshot().LostSeats, Is.Zero);
            Assert.That(session.GetSnapshot().LastIsolationResult, Is.Null);
            Assert.That(session.GetSnapshot().Stability, Is.EqualTo(3));
            Assert.That(session.GetSnapshot().NextBusIds, Is.EquivalentTo(new[] { "L01-R2" }));
        }

        [Test]
        public void MemoryChoiceRemainsAvailableAfterChargeExhaustionAndKeepsRiskOption()
        {
            BoardSession session = new BoardSession(CreateEchoLevel());
            MoveAndDrain(session, "R1", 1);
            Assert.That(session.TryScan(), Is.True);
            Drain(session);
            Assert.That(session.GetSnapshot().HasSecurityCheck, Is.True);
            Assert.That(session.GetSnapshot().CctvCharges, Is.Zero);
            Assert.That(session.TryQuarantine("A"), Is.True);
            Drain(session);
            Assert.That(session.GetSnapshot().Stability, Is.EqualTo(3));
            Assert.That(session.TryQuarantine("B"), Is.True);
            Drain(session);
            Assert.That(session.GetSnapshot().Stability, Is.EqualTo(2));
            Assert.That(session.GetSnapshot().LostSeats, Is.EqualTo(1));
        }

        [Test]
        public void HumanQuarantineDefeatsAtZeroAndRetryRestoresResources()
        {
            BoardSession session = new BoardSession(CreateEchoLevel(1));
            Assert.That(session.TryQuarantine("B"), Is.True);
            Drain(session);
            Assert.That(session.GetSnapshot().IsDefeat, Is.True);
            Assert.That(session.GetSnapshot().Stability, Is.Zero);
            Assert.That(session.GetSnapshot().LostSeats, Is.EqualTo(1));
            session.Restart();
            AssertEchoInitial(session.GetSnapshot());
            Assert.That(session.GetSnapshot().LostSeats, Is.Zero);
        }

        private static LevelDefinition CreateEchoLevel(int stability = 3, bool extraEchoes = false)
        {
            List<PassengerDefinition> firstQueue = new List<PassengerDefinition>
            {
                new PassengerDefinition("echo-a", "R", "echo")
            };
            List<PassengerDefinition> secondQueue = new List<PassengerDefinition>();

            if (extraEchoes)
            {
                firstQueue.Add(new PassengerDefinition("echo-hidden", "R", "echo"));
                secondQueue.Add(new PassengerDefinition("echo-b", "T", "echo"));
            }

            firstQueue.Add(Human("a1", "R"));
            firstQueue.Add(Human("a2", "R"));
            secondQueue.Add(Human("b1", "T"));
            secondQueue.Add(Human("b2", "T"));
            return new LevelDefinition("LEVEL-04", 12,
                new Dictionary<string, int> { { "A", 2 }, { "B", 4 }, { "C", 6 }, { "D", 8 } },
                new[] { new QueueDefinition("A", firstQueue), new QueueDefinition("B", secondQueue) },
                new[] { Mini("red", "R", 0), Mini("turquoise", "T", 0) },
                new[] { Row("R1", "red"), Row("R2", "turquoise") }, 1, stability, 1);
        }

        private static void AssertEchoInitial(BoardSnapshot snapshot)
        {
            Assert.That(snapshot.IsVictory, Is.False);
            Assert.That(snapshot.IsDefeat, Is.False);
            Assert.That(snapshot.HasSecurityCheck, Is.False);
            Assert.That(snapshot.Stability, Is.EqualTo(1));
            Assert.That(snapshot.CctvCharges, Is.EqualTo(1));
            Assert.That(snapshot.CctvUses, Is.Zero);
            Assert.That(snapshot.EmergencyResets, Is.Zero);
            Assert.That(snapshot.Queues[0].RemainingPassengers[0].Kind, Is.EqualTo("human"));
            Assert.That(snapshot.Queues[0].RemainingPassengers.Count, Is.EqualTo(3));
            Assert.That(snapshot.Queues[0].RemainingPassengers[0].Kind, Is.EqualTo("human"));
            Assert.That(snapshot.Rows[0].Bus.LeftX, Is.Zero);
            Assert.That(snapshot.Rows[0].Bus.Occupied, Is.Zero);
        }

        private static LevelDefinition CreateLevel01()
        {
            return CreateLevel(
                new[]
                {
                    new QueueDefinition("A", new[] { Human("L01-A-01", "R"), Human("L01-A-02", "R"),
                        Human("L01-A-03", "R"), Human("L01-A-04", "R") }),
                    new QueueDefinition("B", new[] { Human("L01-B-01", "T"), Human("L01-B-02", "T") })
                },
                new[] { Mini("L01-R1", "R", 0), Mini("L01-R2", "R", 0), Mini("L01-T", "T", 0) },
                new[] { new RowDefinition("R1", "L01-R1", new[] { "L01-R2" }), Row("R2", "L01-T") });
        }

        private static LevelDefinition CreateTwoRouteLevel(int upperX, int lowerX)
        {
            return CreateLevel(
                new[]
                {
                    new QueueDefinition("A", new[] { Human("a1", "R"), Human("a2", "R") }),
                    new QueueDefinition("B", new[] { Human("b1", "T"), Human("b2", "T") })
                },
                new[] { Mini("top-t", "T", upperX), Mini("bottom-r", "R", lowerX) },
                new[] { Row("R1", "top-t"), Row("R2", "bottom-r") });
        }

        private static LevelDefinition CreateLevel(IEnumerable<QueueDefinition> queues,
            IEnumerable<BusDefinition> buses, IEnumerable<RowDefinition> rows)
        {
            return new LevelDefinition("LEVEL-01", 12,
                new Dictionary<string, int> { { "A", 2 }, { "B", 4 }, { "C", 6 }, { "D", 8 } }, queues, buses, rows);
        }

        private static PassengerDefinition Human(string id, string route)
        {
            return new PassengerDefinition(id, route);
        }

        private static BusDefinition Mini(string id, string route, int x)
        {
            return new BusDefinition(id, route, "MINI", 2, 2, x);
        }

        private static RowDefinition Row(string id, string bus)
        {
            return new RowDefinition(id, bus, Array.Empty<string>());
        }

        private static List<string> GetVehicleTrace(BoardSession session)
        {
            List<string> trace = new List<string>();

            foreach (string entry in session.Trace)
            {
                if (entry.StartsWith("BusDeparted:", StringComparison.Ordinal)
                    || entry.StartsWith("BusArrived:", StringComparison.Ordinal))
                {
                    trace.Add(entry);
                }
            }

            return trace;
        }

        private static void AcknowledgePassenger(BoardSession session, string passengerId)
        {
            foreach (BoardStep step in session.BoardingSteps)
            {
                if (step.PassengerId == passengerId)
                {
                    Assert.That(session.TryAcknowledge(step.Generation, step.Token), Is.True);
                    return;
                }
            }

            Assert.Fail("Passenger is not currently walking: " + passengerId);
        }

        private static void MoveAndDrain(BoardSession session, string row, int x)
        {
            Assert.That(session.TryMoveBus(row, x), Is.True);
            Drain(session);
        }

        private static void Drain(BoardSession session)
        {
            int remainingSteps = 100;

            while (session.PendingStep != null && remainingSteps-- > 0)
            {
                Acknowledge(session);
            }

            Assert.That(remainingSteps, Is.GreaterThan(0), "Resolve must reach a stable state.");
        }

        private static void AdvanceUntil(BoardSession session, BoardStepKind kind)
        {
            int remainingSteps = 100;

            while (session.PendingStep != null && session.PendingStep.Kind != kind && remainingSteps-- > 0)
            {
                Acknowledge(session);
            }

            Assert.That(session.PendingStep, Is.Not.Null);
            Assert.That(session.PendingStep.Kind, Is.EqualTo(kind));
        }

        private static void Acknowledge(BoardSession session)
        {
            BoardStep step = session.PendingStep;
            Assert.That(step, Is.Not.Null);
            Assert.That(session.TryAcknowledge(step.Generation, step.Token), Is.True);
        }

        private static void AssertInitialLevel01(BoardSnapshot snapshot)
        {
            Assert.That(snapshot.IsVictory, Is.False);
            Assert.That(snapshot.Rows[0].Bus.Id, Is.EqualTo("L01-R1"));
            Assert.That(snapshot.Rows[0].Bus.LeftX, Is.Zero);
            Assert.That(snapshot.Rows[0].Bus.Occupied, Is.Zero);
            Assert.That(snapshot.NextBusIds, Is.EquivalentTo(new[] { "L01-R2" }));
            Assert.That(snapshot.Rows[1].Bus.Id, Is.EqualTo("L01-T"));
            Assert.That(snapshot.Rows[1].Bus.LeftX, Is.Zero);
            Assert.That(snapshot.Queues[0].RemainingPassengers.Count, Is.EqualTo(4));
            Assert.That(snapshot.Queues[1].RemainingPassengers.Count, Is.EqualTo(2));
        }
    }
}
