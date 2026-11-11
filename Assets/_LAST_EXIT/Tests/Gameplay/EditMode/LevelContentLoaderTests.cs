using System;
using System.Collections.Generic;
using System.Globalization;
using LastExit.Content;
using LastExit.Domain;
using NUnit.Framework;
using UnityEngine;

namespace LastExit.Tests.Gameplay
{
    public class LevelContentLoaderTests
    {
        private const int VariantsPerLevel = 16;
        private const int MaximumCoverageSeeds = 512;

        [Test]
        public void FirstLevelKeepsFixedDemonstrationWithoutConsumingRandomness()
        {
            UnityEngine.Random.State state = UnityEngine.Random.state;
            LevelContentSelection selected = LevelContentLoader.Load(LoadSource(1), true);
            Assert.AreEqual("fixed", selected.VariantId);
            Assert.AreEqual(3, selected.Definition.Buses[0].InitialLeftX);
            Assert.AreEqual("L01-R1", selected.Definition.Rows[0].InitialBusId);
            Assert.AreEqual(state, UnityEngine.Random.state);
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void ExplicitlyDisabledRandomizationKeepsBaseDefinition(int levelIndex)
        {
            TextAsset source = LoadSource(levelIndex);
            LevelDefinition expected = LevelJsonImporter.Parse(source.text);
            LevelContentSelection selected = LevelContentLoader.Load(source, false);
            Assert.AreEqual("fixed", selected.VariantId);
            AssertInvariantContent(expected, selected.Definition);
            Assert.AreEqual(GetSchedule(expected), GetSchedule(selected.Definition));
            Assert.AreEqual(GetPositions(expected), GetPositions(selected.Definition));
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void EveryInstalledVariantPreservesContentAndChangesOnlyArrivalScenario(int levelIndex)
        {
            TextAsset source = LoadSource(levelIndex);
            LevelDefinition expected = LevelJsonImporter.Parse(source.text);
            TextAsset[] pool = Resources.LoadAll<TextAsset>("ArrivalVariants/" + expected.Id);
            Assert.AreEqual(VariantsPerLevel, pool.Length);
            HashSet<string> installedIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> schedules = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> positions = new HashSet<string>(StringComparer.Ordinal);

            foreach (TextAsset candidate in pool)
            {
                Assert.IsTrue(installedIds.Add(candidate.name), "Duplicate arrival variant resource name.");
                LevelDefinition actual = LevelJsonImporter.Parse(candidate.text);
                AssertInvariantContent(expected, actual);
                schedules.Add(GetSchedule(actual));
                positions.Add(GetPositions(actual));
            }

            Assert.Greater(schedules.Count, 1, "Arrival bus assignment/order must vary.");
            Assert.Greater(positions.Count, 1, "Arrival stopping positions must vary.");
            UnityEngine.Random.State savedState = UnityEngine.Random.state;
            HashSet<string> selectedIds = new HashSet<string>(StringComparer.Ordinal);

            try
            {
                for (int seed = 0; seed < MaximumCoverageSeeds && selectedIds.Count < pool.Length; seed++)
                {
                    UnityEngine.Random.InitState(seed);
                    LevelContentSelection selection = LevelContentLoader.Load(source, true);
                    Assert.IsTrue(installedIds.Contains(selection.VariantId));
                    AssertInvariantContent(expected, selection.Definition);
                    selectedIds.Add(selection.VariantId);
                }

                CollectionAssert.AreEquivalent(installedIds, selectedIds,
                    "Every installed candidate must pass the public loader contract.");
            }
            finally
            {
                UnityEngine.Random.state = savedState;
            }
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void SelectionRepeatsForSameSeedAndVariesAcrossSeeds(int levelIndex)
        {
            TextAsset source = LoadSource(levelIndex);
            UnityEngine.Random.State savedState = UnityEngine.Random.state;

            try
            {
                UnityEngine.Random.InitState(42);
                LevelContentSelection first = LevelContentLoader.Load(source, true);
                UnityEngine.Random.InitState(42);
                LevelContentSelection repeat = LevelContentLoader.Load(source, true);
                Assert.AreEqual(first.VariantId, repeat.VariantId);
                Assert.AreEqual(GetSchedule(first.Definition), GetSchedule(repeat.Definition));
                Assert.AreEqual(GetPositions(first.Definition), GetPositions(repeat.Definition));
                HashSet<string> selectedIds = new HashSet<string>(StringComparer.Ordinal);

                for (int seed = 0; seed < VariantsPerLevel; seed++)
                {
                    UnityEngine.Random.InitState(seed);
                    selectedIds.Add(LevelContentLoader.Load(source, true).VariantId);
                }

                Assert.Greater(selectedIds.Count, 1);
            }
            finally
            {
                UnityEngine.Random.state = savedState;
            }
        }

        [Test]
        public void RejectsVariantPoolWithDifferentPassengerIdentity()
        {
            TextAsset source = LoadSource(2);
            LevelDefinition definition = LevelJsonImporter.Parse(source.text);
            string passengerId = definition.Queues[0].Passengers[0].Id;
            TextAsset changed = new TextAsset(source.text.Replace(passengerId, passengerId + "-changed"));
            UnityEngine.Random.State savedState = UnityEngine.Random.state;

            try
            {
                UnityEngine.Random.InitState(42);
                Assert.Throws<InvalidOperationException>(() => LevelContentLoader.Load(changed, true));
            }
            finally
            {
                UnityEngine.Random.state = savedState;
                UnityEngine.Object.DestroyImmediate(changed);
            }
        }

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
        public void LaterLevelsKeepTheirFixedLayoutWithoutConsumingRandomness(int levelIndex)
        {
            TextAsset source = LoadSource(levelIndex);
            UnityEngine.Random.State state = UnityEngine.Random.state;
            LevelContentSelection selected = LevelContentLoader.Load(source, true);
            Assert.AreEqual("fixed", selected.VariantId);
            Assert.AreEqual(state, UnityEngine.Random.state);
            Assert.AreEqual(GetSchedule(LevelJsonImporter.Parse(source.text)), GetSchedule(selected.Definition));
            Assert.AreEqual(GetPositions(LevelJsonImporter.Parse(source.text)), GetPositions(selected.Definition));
        }

        private static TextAsset LoadSource(int levelIndex)
        {
            string suffix = levelIndex.ToString("00", CultureInfo.InvariantCulture);
            TextAsset source = Resources.Load<TextAsset>("Levels/level_" + suffix);
            Assert.IsNotNull(source);
            return source;
        }

        private static void AssertInvariantContent(LevelDefinition expected, LevelDefinition actual)
        {
            Assert.AreEqual(expected.Id, actual.Id);
            Assert.AreEqual(expected.Width, actual.Width);
            Assert.AreEqual(expected.CctvCharges, actual.CctvCharges);
            Assert.AreEqual(expected.InitialStability, actual.InitialStability);
            Assert.AreEqual(expected.TargetCctvUses, actual.TargetCctvUses);
            CollectionAssert.AreEquivalent(expected.Corridors, actual.Corridors);
            Assert.AreEqual(expected.Queues.Count, actual.Queues.Count);
            Assert.AreEqual(expected.Rows.Count, actual.Rows.Count);
            Assert.AreEqual(expected.Buses.Count, actual.Buses.Count);

            for (int index = 0; index < expected.Queues.Count; index++)
            {
                QueueDefinition expectedQueue = expected.Queues[index];
                QueueDefinition actualQueue = actual.Queues[index];
                Assert.AreEqual(expectedQueue.Id, actualQueue.Id);
                Assert.AreEqual(expectedQueue.Passengers.Count, actualQueue.Passengers.Count);

                for (int passengerIndex = 0; passengerIndex < expectedQueue.Passengers.Count; passengerIndex++)
                {
                    PassengerDefinition passenger = expectedQueue.Passengers[passengerIndex];
                    PassengerDefinition candidate = actualQueue.Passengers[passengerIndex];
                    Assert.AreEqual(passenger.Id, candidate.Id);
                    Assert.AreEqual(passenger.Route, candidate.Route);
                    Assert.AreEqual(passenger.Kind, candidate.Kind);
                }
            }

            Dictionary<string, BusDefinition> actualBuses = new Dictionary<string, BusDefinition>();

            foreach (BusDefinition bus in actual.Buses)
            {
                actualBuses.Add(bus.Id, bus);
            }

            foreach (BusDefinition bus in expected.Buses)
            {
                Assert.IsTrue(actualBuses.ContainsKey(bus.Id));
                BusDefinition candidate = actualBuses[bus.Id];
                Assert.AreEqual(bus.Route, candidate.Route);
                Assert.AreEqual(bus.Class, candidate.Class);
                Assert.AreEqual(bus.FootprintCells, candidate.FootprintCells);
                Assert.AreEqual(bus.Capacity, candidate.Capacity);
            }

            for (int index = 0; index < expected.Rows.Count; index++)
            {
                Assert.AreEqual(expected.Rows[index].Id, actual.Rows[index].Id);
                Assert.AreEqual(expected.Rows[index].NextBusIds.Count, actual.Rows[index].NextBusIds.Count);
            }
        }

        private static string GetSchedule(LevelDefinition definition)
        {
            List<string> schedule = new List<string>();

            foreach (RowDefinition row in definition.Rows)
            {
                schedule.Add(row.Id + ":" + row.InitialBusId + ":" + string.Join(",", row.NextBusIds));
            }

            schedule.Add("FIFO:" + string.Join(",", definition.NextBusIds));
            return string.Join("|", schedule);
        }

        private static string GetPositions(LevelDefinition definition)
        {
            List<string> positions = new List<string>();

            foreach (BusDefinition bus in definition.Buses)
            {
                positions.Add(bus.Id + ":" + bus.InitialLeftX);
            }

            positions.Sort(StringComparer.Ordinal);
            return string.Join("|", positions);
        }
    }
}
