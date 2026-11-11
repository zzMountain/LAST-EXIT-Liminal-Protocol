using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastExit.Editor.Gameplay
{
    public static class GameplayWebGlBuilder
    {
        public const string OutputPath = "Builds/WebGL_FullFinal_20261108";
        public const string ReleaseOutputPath = "Builds/WebGL_Yandex_FullFinal_20261108";
        private const string BuildCommand = "LAST EXIT/Gameplay/Build LEVEL-01 to LEVEL-15 WebGL";
        private const string ReleaseBuildCommand = "LAST EXIT/Gameplay/Build Yandex Release WebGL";

        [MenuItem(BuildCommand)]
        public static void Build()
        {
            BuildCampaign(OutputPath, BuildOptions.Development, "TestResults/FullFinal-20261108-Dev-WebGL.txt");
        }

        [MenuItem(ReleaseBuildCommand)]
        public static void BuildRelease()
        {
            BuildCampaign(ReleaseOutputPath, BuildOptions.None, "TestResults/FullFinal-20261108-Release-WebGL.txt");
        }

        private static void BuildCampaign(string outputPath, BuildOptions buildOptions, string reportPath)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Exit Play mode before building the LEVEL-01–15 campaign.");
            }

            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                if (SceneManager.GetSceneAt(index).isDirty)
                {
                    throw new InvalidOperationException("Save modified scenes before building the LEVEL-01–15 campaign.");
                }
            }

            string processorCount = Environment.GetEnvironmentVariable("DOTNET_PROCESSOR_COUNT");
            string binaryenCores = Environment.GetEnvironmentVariable("BINARYEN_CORES");
            string emccCores = Environment.GetEnvironmentVariable("EMCC_CORES");
            // Keep the responsive template selected for ordinary Editor builds as well.
            PlayerSettings.WebGL.template = "PROJECT:LastExit";

            try
            {
                // Same single-worker workaround as the project's Build-WebGL.ps1.
                Environment.SetEnvironmentVariable("DOTNET_PROCESSOR_COUNT", "1");
                Environment.SetEnvironmentVariable("BINARYEN_CORES", "1");
                Environment.SetEnvironmentVariable("EMCC_CORES", "1");
                BuildPlayerOptions options = new BuildPlayerOptions
                {
                    scenes = new[] { LevelSceneAuthoring.ScenePath },
                    locationPathName = outputPath,
                    target = BuildTarget.WebGL,
                    options = buildOptions
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);

                if (report.summary.result == BuildResult.Succeeded)
                {
                    File.Copy("Assets/_LAST_EXIT/Art/Fonts/Inter-LICENSE.txt",
                        Path.Combine(outputPath, "Inter-LICENSE.txt"), true);
                }

                Directory.CreateDirectory("TestResults");
                string summary = "Result=" + report.summary.result + "; Bytes=" + report.summary.totalSize +
                    "; Errors=" + report.summary.totalErrors + "; Output=" + outputPath +
                    "; Development=" + ((buildOptions & BuildOptions.Development) != 0);
                File.WriteAllText(reportPath, summary);

                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new InvalidOperationException("LEVEL-01–15 WebGL build failed: " + summary);
                }

                Debug.Log(summary);
            }
            finally
            {
                Environment.SetEnvironmentVariable("DOTNET_PROCESSOR_COUNT", processorCount);
                Environment.SetEnvironmentVariable("BINARYEN_CORES", binaryenCores);
                Environment.SetEnvironmentVariable("EMCC_CORES", emccCores);
            }
        }
    }
}
