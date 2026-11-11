namespace LastExit.Domain
{
    public class LaneAccess
    {
        internal LaneAccess(string queueId, string firstRowId, string busId, LaneAccessReason reason)
        {
            QueueId = queueId;
            FirstRowId = firstRowId;
            BusId = busId;
            Reason = reason;
        }

        public string QueueId { get; }
        public string FirstRowId { get; }
        public string BusId { get; }
        public LaneAccessReason Reason { get; }
        public bool CanBoard => Reason == LaneAccessReason.Ready;
    }
}
