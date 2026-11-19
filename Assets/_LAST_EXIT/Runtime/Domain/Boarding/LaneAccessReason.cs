namespace LastExit.Domain
{
    public enum LaneAccessReason
    {
        EmptyQueue,
        NoBus,
        DoorMisaligned,
        RouteMismatch,
        Full,
        Unavailable,
        Ready
    }
}
