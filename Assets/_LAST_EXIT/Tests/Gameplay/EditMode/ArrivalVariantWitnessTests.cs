using System;
using System.Collections.Generic;
using System.Globalization;
using LastExit.Content;
using LastExit.Domain;
using NUnit.Framework;
using UnityEngine;

namespace LastExit.Tests.Gameplay
{
    public class ArrivalVariantWitnessTests
    {
        private const int VariantsPerLevel = 16;
        private const int MaximumStepsPerCommand = 256;

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void EveryShippedVariantWinsCleanlyAndRepeatsIdentically(int levelIndex)
        {
            string levelId = "LEVEL-" + levelIndex.ToString("00", CultureInfo.InvariantCulture);

            for (int index = 0; index < VariantsPerLevel; index++)
            {
                string variantId = "v" + index.ToString("00", CultureInfo.InvariantCulture);
                string resourcePath = "ArrivalVariants/" + levelId + "/" + variantId;
                TextAsset resource = Resources.Load<TextAsset>(resourcePath);
                Assert.IsNotNull(resource, resourcePath);
                LevelDefinition definition = LevelJsonImporter.Parse(resource.text);
                Assert.AreEqual(levelId, definition.Id);
                VariantWitness witness = JsonUtility.FromJson<VariantWitness>(resource.text);
                Assert.IsNotNull(witness);
                Assert.IsNotNull(witness.reference_solution);
                Assert.That(witness.reference_solution.Length, Is.GreaterThan(0), resourcePath);
                BoardSession board = new BoardSession(definition);
                Assert.IsNull(board.PendingStep, resourcePath + " must start idle.");
                Assert.AreEqual(0, board.Trace.Count, resourcePath + " must not auto-board at startup.");
                Assert.IsFalse(board.GetSnapshot().HasSecurityCheck, resourcePath);
                Replay(board, witness.reference_solution, resourcePath);
                AssertCleanVictory(board, definition, resourcePath);
                List<string> firstTrace = new List<string>(board.Trace);
                int previousGeneration = board.GetSnapshot().Generation;
                board.Restart();
                Assert.That(board.GetSnapshot().Generation, Is.GreaterThan(previousGeneration), resourcePath);
                Assert.IsNull(board.PendingStep, resourcePath);
                Assert.AreEqual(0, board.Trace.Count, resourcePath);
                Assert.IsFalse(board.GetSnapshot().IsVictory, resourcePath);
                Assert.AreEqual(definition.CctvCharges, board.GetSnapshot().CctvCharges, resourcePath);
                Assert.AreEqual(definition.InitialStability, board.GetSnapshot().Stability, resourcePath);
                Replay(board, witness.reference_solution, resourcePath + " repeat");
                AssertCleanVictory(board, definition, resourcePath + " repeat");
                CollectionAssert.AreEqual(firstTrace, board.Trace, resourcePath + " replay trace");
            }
        }

        [TestCase(1)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        [TestCase(11)]
        [TestCase(12)]
        [TestCase(13)]
        [TestCase(14)]
        [TestCase(15)]
        public void FixedCampaignResourcesWinCleanlyAndRepeat(int levelIndex)
        {
            string path = "Levels/level_" + levelIndex.ToString("00", CultureInfo.InvariantCulture);
            TextAsset resource = Resources.Load<TextAsset>(path);
            Assert.IsNotNull(resource, path);
            LevelDefinition definition = LevelJsonImporter.Parse(resource.text);
            VariantWitness witness = JsonUtility.FromJson<VariantWitness>(resource.text);
            BoardSession board = new BoardSession(definition);
            Replay(board, witness.reference_solution, path);
            AssertCleanVictory(board, definition, path);
            List<string> trace = new List<string>(board.Trace);
            board.Restart();
            Replay(board, witness.reference_solution, path);
            AssertCleanVictory(board, definition, path);
            CollectionAssert.AreEqual(trace, board.Trace);
        }

        [Test]
        public void ExtraScanWinsWithTwoStarsAndRestartClearsTheResult()
        {
            TextAsset resource = Resources.Load<TextAsset>("Levels/level_05");
            BoardSession board = new BoardSession(LevelJsonImporter.Parse(resource.text));
            Assert.AreEqual(0, board.ResultStars);
            Replay(board, new[] { new WitnessCommand { action = "scan" } }, "extra scan");
            VariantWitness witness = JsonUtility.FromJson<VariantWitness>(resource.text);
            Replay(board, witness.reference_solution, "LEVEL-05 extra scan");
            Assert.IsTrue(board.GetSnapshot().IsVictory);
            Assert.AreEqual(2, board.ResultStars);
            board.Restart();
            Assert.AreEqual(0, board.ResultStars);
        }

        [Test]
        public void EmergencyResetWinAwardsOneStar()
        {
            TextAsset resource = Resources.Load<TextAsset>("Levels/level_04");
            BoardSession board = new BoardSession(LevelJsonImporter.Parse(resource.text));
            VariantWitness witness = JsonUtility.FromJson<VariantWitness>(resource.text);
            List<WitnessCommand> commands = new List<WitnessCommand>();

            foreach (WitnessCommand command in witness.reference_solution)
            {
                if (command.action == "scan")
                {
                    continue;
                }

                if (command.action == "quarantine")
                {
                    command.action = "risk";
                }

                commands.Add(command);
            }

            Replay(board, commands.ToArray(), "LEVEL-04 risk");
            Assert.IsTrue(board.GetSnapshot().IsVictory);
            Assert.AreEqual(1, board.GetSnapshot().EmergencyResets);
            Assert.AreEqual(1, board.ResultStars);
        }

        [Test]
        public void HumanSeatLossDoesNotInventAnotherStarPenalty()
        {
            TextAsset resource = Resources.Load<TextAsset>("Levels/level_01");
            BoardSession board = new BoardSession(LevelJsonImporter.Parse(resource.text));
            Replay(board, new[] { new WitnessCommand { action = "quarantine", queue = "A" } }, "Human loss");
            VariantWitness witness = JsonUtility.FromJson<VariantWitness>(resource.text);
            Replay(board, witness.reference_solution, "LEVEL-01 Human loss");
            Assert.IsTrue(board.GetSnapshot().IsVictory);
            Assert.AreEqual(1, board.GetSnapshot().LostSeats);
            Assert.AreEqual(0, board.GetSnapshot().EmergencyResets);
            Assert.AreEqual(3, board.ResultStars);
        }

        private static void Replay(BoardSession board, WitnessCommand[] commands, string context)
        {
            foreach (WitnessCommand command in commands)
            {
                Assert.IsNotNull(command, context);
                Assert.IsNull(board.PendingStep, context);
                bool accepted;

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
                    case "risk":
                        accepted = board.TryAcceptRisk();
                        break;
                    default:
                        Assert.Fail(context + " has an unsupported witness action: " + command.action);
                        return;
                }

                Assert.IsTrue(accepted, context + " rejected " + command.action + " " + command.row);
                int completedSteps = 0;

                while (board.PendingStep != null && completedSteps < MaximumStepsPerCommand)
                {
                    BoardStep pending = board.PendingStep;
                    Assert.IsTrue(board.TryAcknowledge(pending.Generation, pending.Token), context);
                    completedSteps++;
                }

                Assert.IsNull(board.PendingStep, context + " failed to reach a stable state.");
                Assert.IsFalse(board.GetSnapshot().IsDefeat, context);
            }
        }

        private static void AssertCleanVictory(BoardSession board, LevelDefinition definition, string context)
        {
            BoardSnapshot snapshot = board.GetSnapshot();
            Assert.IsTrue(snapshot.IsVictory, context);
            Assert.AreEqual(3, board.ResultStars, context);
            Assert.IsFalse(snapshot.IsDefeat, context);
            Assert.IsFalse(snapshot.HasSecurityCheck, context);
            Assert.AreEqual(definition.InitialStability, snapshot.Stability, context);
            Assert.AreEqual(0, snapshot.EmergencyResets, context);
            Assert.AreEqual(definition.TargetCctvUses, snapshot.CctvUses, context);
            Assert.AreEqual(definition.CctvCharges - definition.TargetCctvUses, snapshot.CctvCharges, context);
            Assert.IsNull(board.PendingStep, context);
            Assert.AreEqual("Victory", board.Trace[board.Trace.Count - 1], context);
            List<string> expectedHumans = new List<string>();
            List<string> expectedEchoes = new List<string>();
            List<string> expectedDepartures = new List<string>();
            List<string> expectedArrivals = new List<string>();

            foreach (QueueDefinition queue in definition.Queues)
            {
                foreach (PassengerDefinition passenger in queue.Passengers)
                {
                    if (passenger.Kind == "human")
                    {
                        expectedHumans.Add(passenger.Id);
                    }
                    else
                    {
                        expectedEchoes.Add(passenger.Id);
                    }
                }
            }

            foreach (BusDefinition bus in definition.Buses)
            {
                expectedDepartures.Add(bus.Id);
            }

            expectedArrivals.AddRange(definition.NextBusIds);

            foreach (QueueSnapshot queue in snapshot.Queues)
            {
                Assert.AreEqual(0, queue.RemainingPassengers.Count, context + " queue " + queue.Id);
            }

            foreach (RowSnapshot row in snapshot.Rows)
            {
                Assert.IsNull(row.Bus, context + " row " + row.Id);
            }

            Assert.IsEmpty(snapshot.NextBusIds, context + " global FIFO");

            CollectionAssert.AreEquivalent(expectedHumans, GetTraceSubjects(board.Trace, "Board:"), context);
            CollectionAssert.AreEquivalent(expectedEchoes, GetTraceSubjects(board.Trace, "Quarantined:"), context);
            CollectionAssert.AreEquivalent(expectedDepartures, GetTraceSubjects(board.Trace, "BusDeparted:"), context);
            CollectionAssert.AreEquivalent(expectedArrivals, GetTraceSubjects(board.Trace, "BusArrived:"), context);
            Assert.AreEqual(0, GetTraceSubjects(board.Trace, "CorruptionReset:").Count, context);
        }

        private static List<string> GetTraceSubjects(IReadOnlyList<string> trace, string prefix)
        {
            List<string> subjects = new List<string>();

            foreach (string entry in trace)
            {
                if (entry.StartsWith(prefix, StringComparison.Ordinal))
                {
                    subjects.Add(entry.Substring(prefix.Length));
                }
            }

            return subjects;
        }

        // QA-only DTO fields mirror the external snake_case witness, outside gameplay assembly contracts.
#pragma warning disable CS0649
        [Serializable]
        private class VariantWitness
        {
            public WitnessCommand[] reference_solution;
        }

        [Serializable]
        private class WitnessCommand
        {
            public string action;
            public string row;
            public int left_x;
            public string queue;
        }
#pragma warning restore CS0649
    }
}
