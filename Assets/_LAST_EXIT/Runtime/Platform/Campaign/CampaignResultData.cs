using System;

namespace LastExit.Platform
{
    // Raw version-1 JSON representation, not a validated runtime result. Field names preserve the wire contract.
    [Serializable]
    public class CampaignResultData
    {
        public int levelIndex;
        public int stars;
        public int cctvUses;
        public int emergencyResets;
        public int lostSeats;
    }
}
