using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace LastExit.Domain
{
    public class LevelDefinition
    {
        private const string LaneIds = "ABCD";
        private const int MaximumCctvCharges = 5;
        private const string CampaignLevelPrefix = "LEVEL-";
        private const int FirstSecurityLevel = 4;

        public LevelDefinition(string id, int width, IDictionary<string, int> corridors,
            IEnumerable<QueueDefinition> queues, IEnumerable<BusDefinition> buses, IEnumerable<RowDefinition> rows,
            int cctvCharges = 0, int initialStability = 3, int targetCctvUses = 0,
            IEnumerable<string> nextBusIds = null)
        {
            if (string.IsNullOrWhiteSpace(id) || width <= 0)
            {
                throw new ArgumentException("A level requires an ID and positive grid width.");
            }

            Id = id;
            Width = width;
            CctvCharges = cctvCharges;
            UsesBoardingBeat = id.StartsWith(CampaignLevelPrefix, StringComparison.Ordinal) &&
                int.TryParse(id.Substring(CampaignLevelPrefix.Length), out int levelNumber) &&
                levelNumber >= FirstSecurityLevel;
            InitialStability = initialStability;
            TargetCctvUses = targetCctvUses;
            Corridors = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(corridors));
            Queues = new List<QueueDefinition>(queues).AsReadOnly();
            Buses = new List<BusDefinition>(buses).AsReadOnly();
            Rows = new List<RowDefinition>(rows).AsReadOnly();
            List<string> reserve = nextBusIds == null ? CreateLegacyReserve(Rows) : new List<string>(nextBusIds);
            NextBusIds = reserve.AsReadOnly();
            Validate();
        }

        public string Id { get; }
        public int Width { get; }
        public int CctvCharges { get; }
        public bool UsesBoardingBeat { get; }
        public bool SupportsInspection => UsesBoardingBeat || CctvCharges > 0;
        public int InitialStability { get; }
        public int TargetCctvUses { get; }
        public IReadOnlyDictionary<string, int> Corridors { get; }
        public IReadOnlyList<QueueDefinition> Queues { get; }
        public IReadOnlyList<BusDefinition> Buses { get; }
        public IReadOnlyList<RowDefinition> Rows { get; }

        public IReadOnlyList<string> NextBusIds { get; }

        private static List<string> CreateLegacyReserve(IReadOnlyList<RowDefinition> rows)
        {
            List<string> reserve = new List<string>();
            int wave = 0;
            bool hasNext;

            do
            {
                hasNext = false;

                foreach (RowDefinition row in rows)
                {
                    if (row == null)
                    {
                        throw new ArgumentException("A row definition cannot be null.", nameof(rows));
                    }

                    if (row.NextBusIds.Count > wave)
                    {
                        reserve.Add(row.NextBusIds[wave]);
                        hasNext = true;
                    }
                }

                wave++;
            }
            while (hasNext);

            return reserve;
        }

        private static Dictionary<string, int> CreateRouteCounts()
        {
            return new Dictionary<string, int> { { "R", 0 }, { "T", 0 }, { "B", 0 }, { "G", 0 } };
        }

        private static void ValidateScheduledBus(string id, Dictionary<string, BusDefinition> buses,
            HashSet<string> scheduledIds)
        {
            if (id == null || buses.ContainsKey(id) == false || scheduledIds.Add(id) == false)
            {
                throw new ArgumentException("A row references an unknown or already scheduled bus.");
            }
        }

        private void Validate()
        {
            if (CctvCharges < 0 || CctvCharges > MaximumCctvCharges || InitialStability < 1 || InitialStability > 3
                || TargetCctvUses < 0 || TargetCctvUses > CctvCharges)
            {
                throw new ArgumentException("Invalid CCTV or stability configuration.");
            }

            if (Corridors.Count != LaneIds.Length || Queues.Count < 2 || Queues.Count > 4
                || Rows.Count < 2 || Rows.Count > 5)
            {
                throw new ArgumentException("Expected four corridors, two to four queues and two to five rows.");
            }

            int previousX = -1;

            foreach (char lane in LaneIds)
            {
                if (Corridors.TryGetValue(lane.ToString(), out int x) == false || x <= previousX || x >= Width)
                {
                    throw new ArgumentException("Corridors must appear in A to D order inside the grid.");
                }

                previousX = x;
            }

            HashSet<string> passengerIds = new HashSet<string>();
            Dictionary<string, int> passengerCounts = CreateRouteCounts();

            for (int index = 0; index < Queues.Count; index++)
            {
                QueueDefinition queue = Queues[index];

                if (queue == null || queue.Id != LaneIds[index].ToString())
                {
                    throw new ArgumentException("Queues must be ordered A to D without gaps.");
                }

                foreach (PassengerDefinition passenger in queue.Passengers)
                {
                    if (passenger == null || passengerIds.Add(passenger.Id) == false)
                    {
                        throw new ArgumentException("Passenger IDs must be unique.");
                    }

                    if (passenger.Kind == PassengerDefinition.HumanKind)
                    {
                        passengerCounts[passenger.Route]++;
                    }
                }
            }

            Dictionary<string, BusDefinition> buses = new Dictionary<string, BusDefinition>();
            Dictionary<string, int> seats = CreateRouteCounts();

            foreach (BusDefinition bus in Buses)
            {
                if (bus == null || buses.ContainsKey(bus.Id) || bus.InitialLeftX < 0
                    || bus.InitialLeftX > Width - bus.FootprintCells)
                {
                    throw new ArgumentException("Bus IDs and initial positions must be valid.");
                }

                buses.Add(bus.Id, bus);
                seats[bus.Route] += bus.Capacity;
            }

            HashSet<string> scheduledIds = new HashSet<string>();

            for (int index = 0; index < Rows.Count; index++)
            {
                RowDefinition row = Rows[index];

                if (row == null || row.Id != "R" + (index + 1))
                {
                    throw new ArgumentException("Rows must be ordered R1 to R5 without gaps.");
                }

                ValidateScheduledBus(row.InitialBusId, buses, scheduledIds);
            }

            foreach (string busId in NextBusIds)
            {
                ValidateScheduledBus(busId, buses, scheduledIds);
            }

            if (scheduledIds.Count != buses.Count)
            {
                throw new ArgumentException("Every bus must appear exactly once in a row schedule.");
            }

            foreach (string route in passengerCounts.Keys)
            {
                if (passengerCounts[route] != seats[route])
                {
                    throw new ArgumentException("Human counts must equal scheduled capacity for each route.");
                }
            }
        }
    }
}
