using System.Collections.Generic;
using LastExit.Domain;
using NUnit.Framework;

namespace LastExit.Tests.Gameplay
{
    public class BoardSessionSecurityOrderTests
    {
        private const int MaximumCompletions = 100;

        [Test]
        public void CommonBeatKeepsIdenticalSecurityHeadsAcrossAcknowledgementOrders()
        {
            BoardSession earlierLaneFirst = CreateSession();
            BoardSession laterLaneFirst = CreateSession();
            CompleteSteps(earlierLaneFirst, false);
            CompleteSteps(laterLaneFirst, true);
            BoardSnapshot earlier = earlierLaneFirst.GetSnapshot();
            BoardSnapshot later = laterLaneFirst.GetSnapshot();

            Assert.That(earlierLaneFirst.PendingStep, Is.Null);
            Assert.That(laterLaneFirst.PendingStep, Is.Null);
            Assert.That(earlier.HasSecurityCheck, Is.True);
            Assert.That(later.HasSecurityCheck, Is.True);
            Assert.That(earlier.Rows[0].Bus.Occupied, Is.EqualTo(1));
            Assert.That(later.Rows[0].Bus.Occupied, Is.EqualTo(1));
            Assert.That(earlier.Rows[1].Bus.Occupied, Is.EqualTo(1));
            Assert.That(later.Rows[1].Bus.Occupied, Is.EqualTo(earlier.Rows[1].Bus.Occupied));
            Assert.That(earlier.Queues[1].RemainingPassengers.Count, Is.EqualTo(3));
            Assert.That(later.Queues[1].RemainingPassengers.Count, Is.EqualTo(3));
            Assert.That(later.Queues[1].RemainingPassengers[0].Id,
                Is.EqualTo(earlier.Queues[1].RemainingPassengers[0].Id));
            AssertNeutralPassengers(earlier);
            AssertNeutralPassengers(later);

            Assert.That(earlierLaneFirst.TryAcceptRisk(), Is.True);
            Assert.That(laterLaneFirst.TryAcceptRisk(), Is.True);
            CompleteSteps(earlierLaneFirst, false);
            CompleteSteps(laterLaneFirst, true);

            Assert.That(earlierLaneFirst.GetSnapshot().IsVictory, Is.True);
            Assert.That(laterLaneFirst.GetSnapshot().IsVictory, Is.True);
            Assert.That(earlierLaneFirst.GetSnapshot().Stability, Is.EqualTo(2));
            Assert.That(laterLaneFirst.GetSnapshot().Stability, Is.EqualTo(2));
        }

        private static BoardSession CreateSession()
        {
            LevelDefinition definition = new LevelDefinition("LEVEL-04", 12,
                new Dictionary<string, int> { { "A", 2 }, { "B", 4 }, { "C", 6 }, { "D", 8 } },
                new[]
                {
                    new QueueDefinition("A", new[]
                    {
                        new PassengerDefinition("a0", "R"),
                        new PassengerDefinition("echo", "R", "echo"),
                        new PassengerDefinition("a1", "R"),
                        new PassengerDefinition("a2", "R"),
                        new PassengerDefinition("a3", "R")
                    }),
                    new QueueDefinition("B", new[]
                    {
                        new PassengerDefinition("b0", "T"),
                        new PassengerDefinition("b1", "T"),
                        new PassengerDefinition("b2", "T"),
                        new PassengerDefinition("b3", "T")
                    })
                },
                new[]
                {
                    new BusDefinition("r", "R", "STANDARD", 3, 4, 1),
                    new BusDefinition("t", "T", "STANDARD", 3, 4, 3)
                },
                new[] { new RowDefinition("R1", "r"), new RowDefinition("R2", "t") });

            return new BoardSession(definition);
        }

        private static void CompleteSteps(BoardSession session, bool laterLaneFirst)
        {
            int completions = 0;

            while (session.PendingStep != null && completions < MaximumCompletions)
            {
                BoardStep step = session.PendingStep;

                if (laterLaneFirst && session.BoardingSteps.Count > 0)
                {
                    step = session.BoardingSteps[session.BoardingSteps.Count - 1];
                }

                Assert.That(session.TryAcknowledge(step.Generation, step.Token), Is.True);
                completions++;
            }

            Assert.That(session.PendingStep, Is.Null, "The finite attempt must reach a stable boundary.");
        }

        private static void AssertNeutralPassengers(BoardSnapshot snapshot)
        {
            foreach (QueueSnapshot queue in snapshot.Queues)
            {
                foreach (PassengerDefinition passenger in queue.RemainingPassengers)
                {
                    Assert.That(passenger.Kind, Is.EqualTo("human"));
                }
            }
        }
    }
}
