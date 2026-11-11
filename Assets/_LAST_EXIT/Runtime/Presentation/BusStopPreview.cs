using LastExit.Domain;

namespace LastExit.Presentation
{
    public class BusStopPreview
    {
        public BusStopPreview(string busId, int leftX, bool isAllowed, LaneAccess doorAccess)
        {
            BusId = busId;
            LeftX = leftX;
            IsAllowed = isAllowed;
            DoorAccess = doorAccess;
        }

        public string BusId { get; }
        public int LeftX { get; }
        public bool IsAllowed { get; }
        public LaneAccess DoorAccess { get; }
        public bool CanBoard => IsAllowed && DoorAccess != null && DoorAccess.BusId == BusId && DoorAccess.CanBoard;
    }
}
