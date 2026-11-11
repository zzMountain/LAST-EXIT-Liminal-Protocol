using System;
using LastExit.ArtLab.Preview;
using UnityEditor;

namespace LastExit.Editor.ArtLab
{
    public static class ArtLabHudAssets
    {
        public const string Folder = "Assets/_LAST_EXIT/ArtLab/UI";
        public const string PortraitPath = Folder + "/HUD_Portrait.asset";
        public const string LandscapePath = Folder + "/HUD_Landscape.asset";

        public static void Configure(PreviewHudLayout layout)
        {
            PreviewHudLayoutPreset portrait = AssetDatabase.LoadAssetAtPath<PreviewHudLayoutPreset>(PortraitPath);
            PreviewHudLayoutPreset landscape = AssetDatabase.LoadAssetAtPath<PreviewHudLayoutPreset>(LandscapePath);

            if (portrait == null || landscape == null)
            {
                throw new InvalidOperationException("HUD layout assets are missing. Restore them before generating a preview copy.");
            }

            layout.SetPresets(portrait, landscape);
        }
    }
}
