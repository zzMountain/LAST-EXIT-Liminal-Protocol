using System;
using System.Collections.Generic;

namespace LastExit.Domain
{
    public class QueueDefinition
    {
        public QueueDefinition(string id, IEnumerable<PassengerDefinition> passengers)
        {
            if (passengers == null)
            {
                throw new ArgumentNullException(nameof(passengers));
            }

            Id = id;
            Passengers = new List<PassengerDefinition>(passengers).AsReadOnly();
        }

        public string Id { get; }
        public IReadOnlyList<PassengerDefinition> Passengers { get; }
    }
}
