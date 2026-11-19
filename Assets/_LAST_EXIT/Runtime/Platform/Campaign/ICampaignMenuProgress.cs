namespace LastExit.Platform
{
    public interface ICampaignMenuProgress : ILevelSelectionReadout
    {
        public int LevelCount { get; }
        public int ResumeLevelIndex { get; }

    }
}
