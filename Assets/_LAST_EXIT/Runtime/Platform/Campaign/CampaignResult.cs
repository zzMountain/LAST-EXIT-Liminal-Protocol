using System;

namespace LastExit.Platform
{
    public class CampaignResult
    {
        public CampaignResult(int levelIndex, int stars, int cctvUses, int emergencyResets, int lostSeats)
        {
            if (levelIndex < 0 || stars < 1 || stars > 3 || cctvUses < 0 || emergencyResets < 0 || lostSeats < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(levelIndex));
            }

            LevelIndex = levelIndex;
            Stars = stars;
            CctvUses = cctvUses;
            EmergencyResets = emergencyResets;
            LostSeats = lostSeats;
        }

        public int LevelIndex { get; }
        public int Stars { get; }
        public int CctvUses { get; }
        public int EmergencyResets { get; }
        public int LostSeats { get; }

        internal bool IsBetterThan(CampaignResult previous)
        {
            if (Stars != previous.Stars)
            {
                return Stars > previous.Stars;
            }

            if (EmergencyResets != previous.EmergencyResets)
            {
                return EmergencyResets < previous.EmergencyResets;
            }

            if (CctvUses != previous.CctvUses)
            {
                return CctvUses < previous.CctvUses;
            }

            return LostSeats < previous.LostSeats;
        }
    }
}
