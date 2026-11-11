using System;
using System.Collections.Generic;
using LastExit.Domain;
using UnityEngine;

namespace LastExit.Content
{
    public static class LevelContentLoader
    {
        private const string TutorialLevelId = "LEVEL-01";
        private const string VariantResourceRoot = "ArrivalVariants/";

        public static LevelContentSelection Load(TextAsset source, bool randomizeArrivals)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            LevelDefinition definition = LevelJsonImporter.Parse(source.text);

            // DEC-008 authorizes randomized arrival scenarios only for LEVEL-02–04.
            bool hasApprovedVariants = definition.Id == "LEVEL-02" || definition.Id == "LEVEL-03" ||
                definition.Id == "LEVEL-04";

            if (randomizeArrivals == false || definition.Id == TutorialLevelId || hasApprovedVariants == false)
            {
                return new LevelContentSelection(definition, LevelContentSelection.FixedVariantId);
            }

            TextAsset[] variants = Resources.LoadAll<TextAsset>(VariantResourceRoot + definition.Id);

            if (variants.Length == 0)
            {
                throw new InvalidOperationException("No verified arrival variants are installed for " + definition.Id);
            }

            Array.Sort(variants, CompareNames);
            TextAsset candidate = variants[UnityEngine.Random.Range(0, variants.Length)];
            LevelDefinition selected = LevelJsonImporter.Parse(candidate.text);
            ValidateVariant(definition, selected);
            return new LevelContentSelection(selected, candidate.name);
        }

        private static int CompareNames(TextAsset left, TextAsset right)
        {
            return string.Compare(left.name, right.name, StringComparison.Ordinal);
        }

        private static void ValidateVariant(LevelDefinition source, LevelDefinition variant)
        {
            if (source.Id != variant.Id || source.Width != variant.Width ||
                source.CctvCharges != variant.CctvCharges || source.InitialStability != variant.InitialStability ||
                source.TargetCctvUses != variant.TargetCctvUses || source.Corridors.Count != variant.Corridors.Count ||
                source.Queues.Count != variant.Queues.Count || source.Buses.Count != variant.Buses.Count ||
                source.Rows.Count != variant.Rows.Count || source.NextBusIds.Count != variant.NextBusIds.Count)
            {
                throw new InvalidOperationException(
                    "Arrival variants must preserve level identity, grid and resources.");
            }

            foreach (KeyValuePair<string, int> corridor in source.Corridors)
            {
                if (variant.Corridors.TryGetValue(corridor.Key, out int x) == false || x != corridor.Value)
                {
                    throw new InvalidOperationException("Arrival variants must preserve every corridor.");
                }
            }

            ValidateQueues(source.Queues, variant.Queues);
            ValidateBuses(source.Buses, variant.Buses);

            for (int index = 0; index < source.Rows.Count; index++)
            {
                RowDefinition expected = source.Rows[index];
                RowDefinition actual = variant.Rows[index];

                if (expected.Id != actual.Id)
                {
                    throw new InvalidOperationException(
                        "Arrival variants must preserve row IDs and global FIFO length.");
                }
            }
        }

        private static void ValidateQueues(IReadOnlyList<QueueDefinition> source,
            IReadOnlyList<QueueDefinition> variant)
        {
            for (int queueIndex = 0; queueIndex < source.Count; queueIndex++)
            {
                QueueDefinition expected = source[queueIndex];
                QueueDefinition actual = variant[queueIndex];

                if (expected.Id != actual.Id || expected.Passengers.Count != actual.Passengers.Count)
                {
                    throw new InvalidOperationException(
                        "Arrival variants must preserve queue identities and lengths.");
                }

                for (int passengerIndex = 0; passengerIndex < expected.Passengers.Count; passengerIndex++)
                {
                    PassengerDefinition passenger = expected.Passengers[passengerIndex];
                    PassengerDefinition candidate = actual.Passengers[passengerIndex];

                    if (passenger.Id != candidate.Id || passenger.Route != candidate.Route ||
                        passenger.Kind != candidate.Kind)
                    {
                        throw new InvalidOperationException(
                            "Arrival variants must preserve passenger order and identity.");
                    }
                }
            }
        }

        private static void ValidateBuses(IReadOnlyList<BusDefinition> source, IReadOnlyList<BusDefinition> variant)
        {
            Dictionary<string, BusDefinition> roster = new Dictionary<string, BusDefinition>(StringComparer.Ordinal);

            foreach (BusDefinition bus in variant)
            {
                roster.Add(bus.Id, bus);
            }

            foreach (BusDefinition bus in source)
            {
                if (roster.TryGetValue(bus.Id, out BusDefinition candidate) == false ||
                    bus.Route != candidate.Route || bus.Class != candidate.Class ||
                    bus.FootprintCells != candidate.FootprintCells || bus.Capacity != candidate.Capacity)
                {
                    throw new InvalidOperationException("Arrival variants must preserve the complete bus roster.");
                }
            }
        }
    }
}
