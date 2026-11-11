using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LastExit.Editor
{
    public static class WebGlBuilder
    {
        private const string BuildCommand = "LAST EXIT/Build WebGL Smoke";

        [MenuItem(BuildCommand)]
        public static void Build()
        {
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath },
                locationPathName = "Builds/WebGL_Smoke",
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException("WebGL smoke build failed: " + report.summary.result);
            }

            Debug.Log("WebGL smoke build succeeded. Bytes: " + report.summary.totalSize);
        }
    }
}
