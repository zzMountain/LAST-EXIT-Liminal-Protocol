using System;
using UnityEngine;

namespace LastExit.Platform
{
    // Serialization-only carrier; gameplay never mutates a decoded instance directly.
    [Serializable]
    public class CampaignProgress
    {
        public int version;
        public int currentLevel;
        public int[] completedLevels;
        public CampaignResultData[] bestResults;
        public bool anomalyHintShown;
        public bool quarantineHintShown;

        public static bool TryDecode(string json, int levelCount, out CampaignProgress progress)
        {
            progress = null;

            if (string.IsNullOrWhiteSpace(json) || levelCount < 1)
            {
                return false;
            }

            CampaignProgress decoded = new CampaignProgress { currentLevel = -1 };

            try
            {
                JsonUtility.FromJsonOverwrite(json, decoded);
            }
            catch (ArgumentException)
            {
                // Unreadable persistence is recoverable; never replace it during a read.
                return false;
            }

            if (decoded == null || decoded.version != 1 || decoded.currentLevel < 0 ||
                decoded.currentLevel >= levelCount || decoded.completedLevels == null)
            {
                return false;
            }

            bool[] seen = new bool[levelCount];

            foreach (int index in decoded.completedLevels)
            {
                if (index < 0 || index >= levelCount || seen[index])
                {
                    return false;
                }

                seen[index] = true;
            }

            decoded.bestResults = decoded.bestResults ?? Array.Empty<CampaignResultData>();
            bool[] resultsSeen = new bool[levelCount];

            foreach (CampaignResultData result in decoded.bestResults)
            {
                if (result == null || result.levelIndex < 0 || result.levelIndex >= levelCount ||
                    seen[result.levelIndex] == false || resultsSeen[result.levelIndex] || result.stars < 1 ||
                    result.stars > 3 || result.cctvUses < 0 || result.emergencyResets < 0 || result.lostSeats < 0)
                {
                    return false;
                }

                resultsSeen[result.levelIndex] = true;
            }

            progress = decoded;
            return true;
        }
    }
}
