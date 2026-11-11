namespace LastExit.Domain
{
    public class BusSnapshot
    {
        internal BusSnapshot(BusDefinition definition, int leftX, int occupied, int lostSeats = 0)
        {
            Id = definition.Id;
            Route = definition.Route;
            Class = definition.Class;
            FootprintCells = definition.FootprintCells;
            Capacity = definition.Capacity;
            LeftX = leftX;
            Occupied = occupied;
            LostSeats = lostSeats;
        }

        public string Id { get; }
        public string Route { get; }
        public string Class { get; }
        public int FootprintCells { get; }
        public int Capacity { get; }
        public int LeftX { get; }
        public int Occupied { get; }
        public int LostSeats { get; }
    }
}
