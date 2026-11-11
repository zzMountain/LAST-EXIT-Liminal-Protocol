namespace LastExit.Domain
{
    public class RowSnapshot
    {
        internal RowSnapshot(string id, BusSnapshot bus)
        {
            Id = id;
            Bus = bus;
        }

        public string Id { get; }
        public BusSnapshot Bus { get; }
    }
}
