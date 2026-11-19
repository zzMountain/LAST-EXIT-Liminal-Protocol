using System.Collections.Generic;

namespace LastExit.Domain
{
    public class QueueSnapshot
    {
        internal QueueSnapshot(string id, int x, IEnumerable<PassengerDefinition> remainingPassengers)
        {
            Id = id;
            X = x;
            RemainingPassengers = new List<PassengerDefinition>(remainingPassengers).AsReadOnly();
        }

        public string Id { get; }
        public int X { get; }
        public IReadOnlyList<PassengerDefinition> RemainingPassengers { get; }
    }
}
