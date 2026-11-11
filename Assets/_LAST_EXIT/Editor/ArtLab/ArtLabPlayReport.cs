using System;
using UnityEngine.Serialization;

namespace LastExit.Editor.ArtLab
{
    [Serializable]
    public class ArtLabPlayReport
    {
        [FormerlySerializedAs("passed")]
        public bool Passed;
        [FormerlySerializedAs("animators")]
        public int Animators;
        [FormerlySerializedAs("playingAnimators")]
        public int PlayingAnimators;
        [FormerlySerializedAs("movingHierarchies")]
        public int MovingHierarchies;
        [FormerlySerializedAs("placementStable")]
        public bool PlacementStable;
        [FormerlySerializedAs("clips")]
        public string[] Clips;
        [FormerlySerializedAs("scene")]
        public string Scene;
    }
}
