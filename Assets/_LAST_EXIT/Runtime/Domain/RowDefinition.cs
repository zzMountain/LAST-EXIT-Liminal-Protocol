using System;
using System.Collections.Generic;

namespace LastExit.Domain
{
    public class RowDefinition
    {
        public RowDefinition(string id, string initialBusId, IEnumerable<string> nextBusIds = null)
        {
            Id = id;
            InitialBusId = initialBusId;
            NextBusIds = new List<string>(nextBusIds ?? Array.Empty<string>()).AsReadOnly();
        }

        public string Id { get; }
        public string InitialBusId { get; }
        public IReadOnlyList<string> NextBusIds { get; }
    }
}
