using System;

namespace LastExit.Domain
{
    public class BusDefinition
    {
        public BusDefinition(string id, string route, string busClass, int footprintCells, int capacity,
            int initialLeftX)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Bus ID is required.", nameof(id));
            }

            if (route != "R" && route != "T" && route != "B" && route != "G")
            {
                throw new ArgumentException("Unknown bus route.", nameof(route));
            }

            bool validClass = (busClass == "MINI" && footprintCells == 2 && capacity == 2)
                || (busClass == "STANDARD" && footprintCells == 3 && capacity == 4)
                || (busClass == "LONG" && footprintCells == 4 && capacity == 6);

            if (validClass == false)
            {
                throw new ArgumentException("Bus size and capacity must follow DEC-001.", nameof(busClass));
            }

            Id = id;
            Route = route;
            Class = busClass;
            FootprintCells = footprintCells;
            Capacity = capacity;
            InitialLeftX = initialLeftX;
        }

        public string Id { get; }
        public string Route { get; }
        public string Class { get; }
        public int FootprintCells { get; }
        public int Capacity { get; }
        public int InitialLeftX { get; }
    }
}
