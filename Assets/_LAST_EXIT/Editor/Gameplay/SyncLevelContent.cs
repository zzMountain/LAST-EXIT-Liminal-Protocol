using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LastExit.Editor.Gameplay
{
    public static class SyncLevelContent
    {
        public const string AssetPath = "Assets/_LAST_EXIT/Resources/Levels/level_01.json";
        private const string SyncCommand = "LAST EXIT/Gameplay/Sync LEVEL-01 to LEVEL-15";
        private const string LevelAssetFolder = "Assets/_LAST_EXIT/Resources/Levels/";
        private const string VariantAssetFolder = "Assets/_LAST_EXIT/Resources/ArrivalVariants/";
        private const int FirstVariantLevel = 2;
        private const int LastLevel = 15;
        private const int LastVariantLevel = 4;
        private const int VariantsPerLevel = 16;

        [MenuItem(SyncCommand)]
        public static void Sync()
        {
            string projectDirectory = Directory.GetParent(Application.dataPath).FullName;
            string sourceFolder = Path.Combine(projectDirectory, "SourceData", "Levels");
            string variantSourceFolder = Path.Combine(sourceFolder, "arrival_variants");
            ValidateVariantSources(variantSourceFolder);

            for (int index = 1; index <= LastLevel; index++)
            {
                string filename = "level_" + index.ToString("00") + ".json";
                CopyAsset(projectDirectory, Path.Combine(sourceFolder, filename), LevelAssetFolder + filename);
            }

            for (int level = FirstVariantLevel; level <= LastVariantLevel; level++)
            {
                string levelId = "LEVEL-" + level.ToString("00");

                for (int variant = 0; variant < VariantsPerLevel; variant++)
                {
                    string filename = "v" + variant.ToString("00") + ".json";
                    string sourcePath = Path.Combine(variantSourceFolder, levelId, filename);
                    string assetPath = VariantAssetFolder + levelId + "/" + filename;
                    CopyAsset(projectDirectory, sourcePath, assetPath);
                }
            }
        }

        private static void ValidateVariantSources(string sourceFolder)
        {
            for (int level = FirstVariantLevel; level <= LastVariantLevel; level++)
            {
                string directory = Path.Combine(sourceFolder, "LEVEL-" + level.ToString("00"));

                if (Directory.Exists(directory) == false)
                {
                    throw new InvalidOperationException("Arrival variant source directory is missing: " + directory);
                }

                string[] paths = Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly);

                if (paths.Length != VariantsPerLevel)
                {
                    throw new InvalidOperationException("Expected sixteen arrival variant JSON files in " + directory);
                }

                Array.Sort(paths, StringComparer.Ordinal);

                for (int variant = 0; variant < VariantsPerLevel; variant++)
                {
                    string expected = "v" + variant.ToString("00") + ".json";

                    if (string.Equals(Path.GetFileName(paths[variant]), expected, StringComparison.Ordinal) == false)
                    {
                        throw new InvalidOperationException("Arrival variants must be named v00.json through v15.json: " +
                            directory);
                    }
                }
            }
        }

        private static void CopyAsset(string projectDirectory, string sourcePath, string assetPath)
        {
            string destinationPath = Path.Combine(projectDirectory, assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
            File.Copy(sourcePath, destinationPath, true);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }
    }
}
