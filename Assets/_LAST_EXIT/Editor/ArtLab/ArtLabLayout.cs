using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastExit.Editor.ArtLab
{
    // Authoring coordinates for the readability study, not approved LevelConfig data.
    public static class ArtLabLayout
    {
        public const float QueueHeadZ = 6.6f;
        public const float QueueSpacing = 1.35f;
        public const float FloorFrontZ = -9.6f;
        public const float FloorBackZ = 11.7f;
        public const float LandscapeSize = 10.5f;
        public const float PortraitSize = 17.2f;

        private static readonly float[] s_rowZ = { 2, -1.7f, -5.4f };
        private static readonly float[] s_laneX = { -3, -1, 1, 3 };

        public static IReadOnlyList<float> RowZ { get; } = Array.AsReadOnly(s_rowZ);
        public static IReadOnlyList<float> LaneX { get; } = Array.AsReadOnly(s_laneX);
        public static Vector3 Q3Position { get; } = new Vector3(-6.6f, 0, 6.7f);
        public static Vector3 CctvPosition { get; } = new Vector3(6.5f, 0, -8);
    }
}
