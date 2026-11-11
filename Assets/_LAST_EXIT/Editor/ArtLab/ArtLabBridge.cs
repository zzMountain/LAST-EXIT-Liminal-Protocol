using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastExit.Editor.ArtLab
{
    [InitializeOnLoad]
    public static class ArtLabBridge
    {
        private const string InspectCommand = "inspect";
        private const string BuildSceneCommand = "Build Scene";
        private const string BuildUXPreviewCommand = "Build UX Preview";
        private const string CaptureUXPreviewCommand = "Capture UX Preview";
        private const string RunUXPlayProbeCommand = "Run UX Play Probe";
        private const string RunAuthoredBaselineProbeCommand = "Run Authored Baseline Probe";
        private const string RecoverAuthoredBaselineProbeCommand = "Recover Authored Baseline Probe";
        private const string SaveSnapshotCommand = "Save Snapshot";
        private const string CaptureAllCommand = "Capture All";
        private const string BakeReflectionsCommand = "Bake Reflections";
        private const string ImportModelsCommand = "Import Models";
        private const string RefreshScriptsCommand = "Refresh Scripts";
        private const string MigratePreviewContractsCommand = "Migrate Preview Contracts";
        private const string OpenPanelCommand = "Open Panel";
        private const string RunTestsCommand = "Run Tests";
        private const string RecoverTestsCommand = "Recover Tests";
        private const string RunnerErrorProbeCommand = "Verify Runner Error Recovery";
        private const string RunPlayProbeCommand = "Run Play Probe";
        private const string RequestPath = "TestResults/ArtLab.request.txt";
        private const string ResponsePath = "TestResults/ArtLab.response.txt";

        private static double s_nextPoll;

        static ArtLabBridge()
        {
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < s_nextPoll || EditorApplication.isCompiling ||
                EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            s_nextPoll = EditorApplication.timeSinceStartup + 1;

            if (File.Exists(RequestPath) == false)
            {
                return;
            }

            string command;

            try
            {
                command = File.ReadAllText(RequestPath).Trim();
                File.Delete(RequestPath);
            }
            catch (IOException)
            {
                // The command producer may still own the file. Leave it pending for the next poll.
                return;
            }

            try
            {
                if (command == InspectCommand)
                {
                    string state = "Unity=" + Application.unityVersion;
                    state += "\nAuthoredBaselineProbe=" + SessionState.GetString(
                        "LastExit.ArtLab.AuthoredBaselineProbe", string.Empty);

                    for (int index = 0; index < SceneManager.sceneCount; index++)
                    {
                        Scene scene = SceneManager.GetSceneAt(index);
                        state += "\nScene=" + scene.path + ";dirty=" + scene.isDirty;
                    }

                    File.WriteAllText(ResponsePath, state);
                }
                else
                {
                    switch (command)
                    {
                        case BuildSceneCommand:
                            ArtLabSceneBuilder.Build();
                            break;
                        case BuildUXPreviewCommand:
                            ArtLabUxScene.Build();
                            break;
                        case CaptureUXPreviewCommand:
                            ArtLabUxCapture.Capture();
                            break;
                        case RunUXPlayProbeCommand:
                            ArtLabUxPlayProbe.Run();
                            break;
                        case RunAuthoredBaselineProbeCommand:
                            ArtLabAuthoredBaselineProbe.Run();
                            break;
                        case RecoverAuthoredBaselineProbeCommand:
                            ArtLabAuthoredBaselineProbe.Recover();
                            break;
                        case SaveSnapshotCommand:
                            ArtLabSceneBuilder.SaveSnapshot();
                            break;
                        case CaptureAllCommand:
                            ArtLabPreview.CaptureAll();
                            break;
                        case BakeReflectionsCommand:
                            ArtLabReflections.Bake();
                            break;
                        case ImportModelsCommand:
                            ArtLabImporter.Import();
                            break;
                        case RefreshScriptsCommand:
                            AssetDatabase.Refresh();
                            break;
                        case MigratePreviewContractsCommand:
                            ArtLabPreviewMigration.Run();
                            break;
                        case OpenPanelCommand:
                            ArtLabWindow.Open();
                            break;
                        case RunTestsCommand:
                            ArtLabVerification.Run();
                            break;
                        case RecoverTestsCommand:
                            ArtLabVerification.RecoverInterruptedRun();
                            break;
                        case RunnerErrorProbeCommand:
                            ArtLabVerification.VerifyErrorRecovery();
                            break;
                        case RunPlayProbeCommand:
                            ArtLabPlayProbe.Run();
                            break;
                        default:
                            throw new InvalidOperationException("Unknown Art Lab command: " + command);
                    }

                    File.WriteAllText(ResponsePath, "Completed: " + command);
                }
            }
            catch (Exception exception)
            {
                // Preserve a failed automation request without terminating the user's Editor.
                File.WriteAllText(ResponsePath, "FAILED: " + exception);
                Debug.LogException(exception);
            }
        }
    }
}
