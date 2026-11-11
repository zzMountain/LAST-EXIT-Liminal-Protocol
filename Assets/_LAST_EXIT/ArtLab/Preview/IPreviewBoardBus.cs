namespace LastExit.ArtLab.Preview
{
    public interface IPreviewBoardBus : IPreviewBusShape
    {
        public string Route { get; }
        public int Capacity { get; }
    }
}
