namespace LastExit.Domain
{
    public class BoardStep
    {
        internal BoardStep(BoardStepKind kind, int generation, long token, string rowId, string busId,
            string queueId, string passengerId, int fromLeftX, int toLeftX)
        {
            Kind = kind;
            Generation = generation;
            Token = token;
            RowId = rowId;
            BusId = busId;
            QueueId = queueId;
            PassengerId = passengerId;
            FromLeftX = fromLeftX;
            ToLeftX = toLeftX;
        }

        public BoardStepKind Kind { get; }
        public int Generation { get; }
        public long Token { get; }
        public string RowId { get; }
        public string BusId { get; }
        public string QueueId { get; }
        public string PassengerId { get; }
        public int FromLeftX { get; }
        public int ToLeftX { get; }
    }
}
