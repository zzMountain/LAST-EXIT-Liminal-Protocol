using System;

namespace LastExit.Editor.ArtLab
{
    [Serializable]
    public class ArtLabPlayReport
    {
        public bool passed;
        public int animators;
        public int playingAnimators;
        public int movingHierarchies;
        public bool placementStable;
        public string[] clips;
        public string scene;
    }
}
