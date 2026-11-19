namespace LastExit.Presentation
{
    public interface ILevelTutorialReadout
    {
        public TutorialStep Step { get; }
        public bool IsComplete { get; }
        public string Text { get; }
        public string FirstBusId { get; }
    }
}
