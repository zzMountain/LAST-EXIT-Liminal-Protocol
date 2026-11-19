namespace LastExit.Platform
{
    public interface ILevelSelectionReadout
    {
        public bool IsLevelUnlocked(int levelIndex);
        public int GetBestStars(int levelIndex);
    }
}
