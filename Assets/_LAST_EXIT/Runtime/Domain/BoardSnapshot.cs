using System.Collections.Generic;

namespace LastExit.Domain
{
    public class BoardSnapshot
    {
        internal BoardSnapshot(int generation, bool isVictory, IEnumerable<RowSnapshot> rows,
            IEnumerable<QueueSnapshot> queues, int cctvCharges, int cctvUses, int stability,
            int emergencyResets, bool isDefeat, bool hasSecurityCheck, IEnumerable<string> nextBusIds,
            int lostSeats, string lastIsolationResult, int isolationCount)
        {
            Generation = generation;
            IsVictory = isVictory;
            CctvCharges = cctvCharges;
            CctvUses = cctvUses;
            Stability = stability;
            EmergencyResets = emergencyResets;
            IsDefeat = isDefeat;
            HasSecurityCheck = hasSecurityCheck;
            NextBusIds = new List<string>(nextBusIds).AsReadOnly();
            LostSeats = lostSeats;
            LastIsolationResult = lastIsolationResult;
            IsolationCount = isolationCount;
            Rows = new List<RowSnapshot>(rows).AsReadOnly();
            Queues = new List<QueueSnapshot>(queues).AsReadOnly();
        }

        public int Generation { get; }
        public bool IsVictory { get; }
        public int CctvCharges { get; }
        public int CctvUses { get; }
        public int Stability { get; }
        public int EmergencyResets { get; }
        public bool IsDefeat { get; }
        public bool HasSecurityCheck { get; }
        public IReadOnlyList<string> NextBusIds { get; }
        public int LostSeats { get; }
        public string LastIsolationResult { get; }
        public int IsolationCount { get; }
        public IReadOnlyList<RowSnapshot> Rows { get; }
        public IReadOnlyList<QueueSnapshot> Queues { get; }
    }
}
