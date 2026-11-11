using System.Collections.Generic;
using LastExit.Domain;
using NUnit.Framework;

namespace LastExit.Tests.Gameplay
{
    public class BoardSessionIsolationBoundaryTests
    {
        private const int MaximumCompletions = 100;

        [TestCase(false)]
        [TestCase(true)]
        public void ChoosingAnotherHeadRetainsReadyEchoBeforeFullDeparture(bool otherHeadIsEcho)
        {
            BoardSession session = CreateSession(otherHeadIsEcho);
            CompleteSteps(session);
            Assert.That(session.GetSnapshot().HasSecurityCheck, Is.True);
            Assert.That(session.GetSnapshot().Rows[1].Bus.Occupied, Is.EqualTo(2));
            Assert.That(session.TryQuarantine("C"), Is.True);
            BoardStep isolation = session.PendingStep;
            Assert.That(session.TryAcknowledge(isolation.Generation, isolation.Token), Is.True);

            BoardSnapshot snapshot = session.GetSnapshot();
            Assert.That(snapshot.HasSecurityCheck, Is.True);
            Assert.That(session.PendingStep, Is.Null);
            Assert.That(session.GetLaneAccess("A").CanBoard, Is.True);
            Assert.That(snapshot.Queues[0].RemainingPassengers[0].Id, Is.EqualTo("echo-a"));
            Assert.That(snapshot.Rows[1].Bus.Id, Is.EqualTo("turquoise"));
            Assert.That(snapshot.Rows[1].Bus.Occupied, Is.EqualTo(2));
            Assert.That(snapshot.Stability, Is.EqualTo(otherHeadIsEcho ? 3 : 2));
            Assert.That(snapshot.LostSeats, Is.EqualTo(otherHeadIsEcho ? 0 : 1));
            CollectionAssert.DoesNotContain(session.Trace, "BusDeparted:turquoise");
            Assert.That(session.TryQuarantine("A"), Is.True);
            CompleteSteps(session);
            Assert.That(session.GetSnapshot().IsVictory, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ResolvingEchoResumesBoardingBeforeReleasingOtherFullBodies(bool acceptRisk)
        {
            BoardSession session = CreateSession(false);
            CompleteSteps(session);
            bool accepted = acceptRisk ? session.TryAcceptRisk() : session.TryQuarantine("A");
            Assert.That(accepted, Is.True);
            BoardStep isolation = session.PendingStep;
            Assert.That(session.TryAcknowledge(isolation.Generation, isolation.Token), Is.True);

            Assert.That(session.GetSnapshot().HasSecurityCheck, Is.False);
            Assert.That(session.PendingStep.Kind, Is.EqualTo(BoardStepKind.Board));
            Assert.That(session.PendingStep.PassengerId, Is.EqualTo("a3"));
            Assert.That(session.GetSnapshot().Rows[1].Bus.Id, Is.EqualTo("turquoise"));
            CollectionAssert.DoesNotContain(session.Trace, "BusDeparted:turquoise");
            CompleteSteps(session);
            Assert.That(session.GetSnapshot().IsVictory, Is.True);
            Assert.That(IndexOf(session.Trace, "Board:a4"),
                Is.LessThan(IndexOf(session.Trace, "BusDeparted:turquoise")));
        }

        private static BoardSession CreateSession(bool otherHeadIsEcho)
        {
            List<PassengerDefinition> thirdQueue = new List<PassengerDefinition>();

            if (otherHeadIsEcho)
            {
                thirdQueue.Add(new PassengerDefinition("echo-c", "B", "echo"));
            }

            thirdQueue.Add(new PassengerDefinition("c1", "B"));
            thirdQueue.Add(new PassengerDefinition("c2", "B"));
            LevelDefinition definition = new LevelDefinition("LEVEL-15", 12,
                new Dictionary<string, int> { { "A", 2 }, { "B", 4 }, { "C", 6 }, { "D", 8 } },
                new[]
                {
                    new QueueDefinition("A", new[]
                    {
                        new PassengerDefinition("a1", "R"), new PassengerDefinition("a2", "R"),
                        new PassengerDefinition("echo-a", "R", "echo"),
                        new PassengerDefinition("a3", "R"), new PassengerDefinition("a4", "R")
                    }),
                    new QueueDefinition("B", new[]
                    {
                        new PassengerDefinition("b1", "T"), new PassengerDefinition("b2", "T")
                    }),
                    new QueueDefinition("C", thirdQueue)
                },
                new[]
                {
                    new BusDefinition("red", "R", "STANDARD", 3, 4, 1),
                    new BusDefinition("turquoise", "T", "MINI", 2, 2, 3),
                    new BusDefinition("blue", "B", "MINI", 2, 2, 5)
                },
                new[] { new RowDefinition("R1", "red"), new RowDefinition("R2", "turquoise") },
                nextBusIds: new[] { "blue" });
            return new BoardSession(definition);
        }

        private static void CompleteSteps(BoardSession session)
        {
            int completions = 0;

            while (session.PendingStep != null && completions < MaximumCompletions)
            {
                BoardStep step = session.PendingStep;
                Assert.That(session.TryAcknowledge(step.Generation, step.Token), Is.True);
                completions++;
            }

            Assert.That(session.PendingStep, Is.Null, "The fixture must reach a settled boundary.");
        }

        private static int IndexOf(IReadOnlyList<string> events, string expected)
        {
            for (int index = 0; index < events.Count; index++)
            {
                if (events[index] == expected)
                {
                    return index;
                }
            }

            Assert.Fail("Missing event: " + expected);
            return -1;
        }
    }
}
