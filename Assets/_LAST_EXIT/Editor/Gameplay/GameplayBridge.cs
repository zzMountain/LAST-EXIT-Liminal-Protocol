using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using LastExit.Domain;
using LastExit.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastExit.Editor.Gameplay
{
    // Local file-command automation; error responses survive without terminating the user's Editor.
    [InitializeOnLoad]
    public static class GameplayBridge
    {
        private const string InspectCommand = "inspect";
        private const string AudioCommand = "apply-audio";
        private const string AudioTestsCommand = "audio-tests";
        private const string SecurityTestsCommand = "security-tests";
        private const string BoardingTestsCommand = "boarding-tests";
        private const string ApplyBoardingCommand = "apply-boarding";
        private const string CaptureCommand = "capture";
        private const string EnterPlayCommand = "enter-play";
        private const string ExitPlayCommand = "exit-play";
        private const string ClearConsoleCommand = "clear-console";
        private const string CreateCommand = "create";
        private const string CampaignCommand = "campaign";
        private const string SyncCommand = "sync";
        private const string ApplyFontCommand = "apply-font";
        private const string ApplyPaceCommand = "apply-pace";
        private const string PaceTestsCommand = "pace-tests";
        private const string PortraitTestsCommand = "portrait-tests";
        private const string ApplyWalkCommand = "apply-walk";
        private const string InspectWalkCommand = "inspect-walk";
        private const string TestsCommand = "tests";
        private const string PlayTestsCommand = "play-tests";
        private const string StopTestsCommand = "stop-tests";
        private const string BuildCommand = "build";
        private const string ArtBalancedCommand = "art-balanced";
        private const string ApplyArtCommand = "apply-art";
        private const string InspectArtCommand = "inspect-art";
        private const string MobileEchoCommand = "mobile-echo";
        private const string RecoverTestsCommand = "recover-tests";
        private const string LandscapeTestsCommand = "landscape-tests";
        private const string QuarantineTestsCommand = "quarantine-tests";
        private const string CompactTestsCommand = "compact-tests";
        private const string NumericBadgesCommand = "numeric-badges";
        private const string InspectProjectCommand = "inspect-project";
        private const string RunnerErrorProbeCommand = "runner-error-probe";
        private const string ReadinessTestsCommand = "ready-tests";
        private const string ExpandCampaignCommand = "campaign-all";
        private const string ReleaseBuildCommand = "release";
        private const string SaveForShutdownCommand = "save-for-shutdown";
        private const string RequestPath = "TestResults/Gameplay.request.txt";
        private const string ResponsePath = "TestResults/Gameplay.response.txt";

        private static double s_nextPoll;

        static GameplayBridge()
        {
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < s_nextPoll || EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                return;
            }

            s_nextPoll = EditorApplication.timeSinceStartup + .5;

            if (File.Exists(RequestPath) == false)
            {
                return;
            }

            string command;

            try
            {
                command = File.ReadAllText(RequestPath).Trim().ToLowerInvariant();
                File.Delete(RequestPath);
            }
            catch (IOException)
            {
                // A local writer may still own the command file. Retry on the next poll.
                return;
            }

            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode && command != InspectCommand && command != CaptureCommand &&
                    command != EnterPlayCommand && command != ExitPlayCommand)
                {
                    throw new InvalidOperationException("Exit Play mode before this automation command: " + command);
                }

                switch (command)
                {
                    case ReadinessTestsCommand:
                        GameplayVerification.RunReadiness();
                        WriteResponse("Started focused loading readiness PlayMode tests");
                        return;
                    case ExpandCampaignCommand:
                        CampaignSceneAuthoring.Expand();
                        break;
                    case ReleaseBuildCommand:
                        GameplayWebGlBuilder.BuildRelease();
                        break;
                    case SaveForShutdownCommand:
                        if (EditorSceneManager.SaveOpenScenes() == false)
                        {
                            throw new IOException("Could not save all open scenes; shutdown preparation stopped.");
                        }

                        AssetDatabase.SaveAssets();
                        WriteResponse("Saved open scenes and project assets for shutdown.\n" + Inspect());
                        return;
                    case AudioCommand:
                        AudioAuthoring.Apply();
                        break;
                    case ApplyBoardingCommand:
                        PassengerAnimationAuthoring.ApplyBoarding();
                        break;
                    case BoardingTestsCommand:
                        GameplayVerification.RunBoarding();
                        WriteResponse("Started focused boarding feedback PlayMode tests");
                        return;
                    case SecurityTestsCommand:
                        GameplayVerification.RunSecurityPanel();
                        WriteResponse("Started focused security panel PlayMode tests");
                        return;
                    case AudioTestsCommand:
                        GameplayVerification.RunAudio();
                        WriteResponse("Started focused audio PlayMode tests");
                        return;
                    case RunnerErrorProbeCommand:
                        GameplayVerification.VerifyErrorRecovery();
                        break;
                    case InspectProjectCommand:
                        ProjectAuditInspection.Inspect();
                        break;
                    case NumericBadgesCommand:
                        StationArtDirection.ApplyNumericBadges();
                        break;
                    case RecoverTestsCommand:
                        GameplayVerification.RecoverInterruptedRun();
                        break;
                    case MobileEchoCommand:
                        MobileEchoAuthoring.Apply();
                        break;
                    case LandscapeTestsCommand:
                        GameplayVerification.RunLandscapeCampaign();
                        WriteResponse("Started focused landscape campaign regression");
                        return;
                    case QuarantineTestsCommand:
                        GameplayVerification.RunQuarantine();
                        WriteResponse("Started focused quarantine lifecycle regression");
                        return;
                    case CompactTestsCommand:
                        GameplayVerification.RunCompactLayout();
                        WriteResponse("Started focused compact layout regression");
                        return;
                    case ArtBalancedCommand:
                        StationArtDirection.UseBalanced();
                        break;
                    case ApplyArtCommand:
                        StationArtDirection.Apply();
                        break;
                    case InspectArtCommand:
                        WriteResponse(StationArtDirection.Inspect());
                        return;
                    case InspectCommand:
                        WriteResponse(Inspect());
                        return;
                    case ClearConsoleCommand:
                        WriteResponse(ClearConsole());
                        return;
                    case CreateCommand:
                        LevelSceneAuthoring.Create();
                        break;
                    case CampaignCommand:
                        SyncLevelContent.Sync();
                        CampaignSceneAuthoring.Apply();
                        break;
                    case SyncCommand:
                        SyncLevelContent.Sync();
                        break;
                    case ApplyFontCommand:
                        GameplayFontAuthoring.Apply();
                        break;
                    case ApplyPaceCommand:
                        PassengerAnimationAuthoring.ApplyPace();
                        break;
                    case PaceTestsCommand:
                        GameplayVerification.RunPace();
                        WriteResponse("Started focused passenger walking PlayMode regression");
                        return;
                    case PortraitTestsCommand:
                        GameplayVerification.RunPortraitCampaign();
                        WriteResponse("Started focused portrait campaign regression");
                        return;
                    case ApplyWalkCommand:
                        PassengerAnimationAuthoring.Apply();
                        break;
                    case InspectWalkCommand:
                        WriteResponse(PassengerAnimationAuthoring.Inspect());
                        return;
                    case TestsCommand:
                        GameplayVerification.Run();
                        WriteResponse("Started EditMode tests; results: TestResults/FullFinal-20261108-EditMode.xml and .txt");
                        return;
                    case PlayTestsCommand:
                        GameplayVerification.RunPlayMode();
                        WriteResponse("Started PlayMode tests; results: TestResults/FullFinal-20261108-PlayMode.xml and .txt");
                        return;
                    case StopTestsCommand:
                        GameplayVerification.RunStopPreview();
                        WriteResponse("Started focused stop preview PlayMode regression");
                        return;
                    case BuildCommand:
                        GameplayWebGlBuilder.Build();
                        break;
                    case CaptureCommand:
                        GameplayCapture.Capture();
                        break;
                    case EnterPlayCommand:
                        EditorApplication.isPlaying = true;
                        break;
                    case ExitPlayCommand:
                        EditorApplication.isPlaying = false;
                        break;
                    default:
                        throw new InvalidOperationException("Unknown gameplay command: " + command);
                }

                WriteResponse("Completed: " + command);
            }
            catch (Exception exception)
            {
                WriteResponse("FAILED: " + exception);
                Debug.LogException(exception);
            }
        }

        private static string Inspect()
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine("Unity=" + Application.unityVersion);
            report.AppendLine("Playing=" + EditorApplication.isPlaying);
            report.AppendLine("Compiling=" + EditorApplication.isCompiling);
            report.AppendLine("Updating=" + EditorApplication.isUpdating);
            report.AppendLine(GetConsoleCounts());

            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                report.AppendLine("Scene=" + scene.path + ";dirty=" + scene.isDirty + ";loaded=" + scene.isLoaded);

                foreach (LevelComposition composition in scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<LevelComposition>(true)))
                {
                    AppendRuntimeState(report, composition);

                    if (composition.Game != null)
                    {
                        LevelBoardView board = scene.GetRootGameObjects()
                            .SelectMany(root => root.GetComponentsInChildren<LevelBoardView>()).Single();

                        foreach (RowSnapshot row in composition.Game.Snapshot.Rows)
                        {
                            if (row.Bus == null)
                            {
                                continue;
                            }

                            report.AppendLine("Door=" + row.Bus.Id + ":" + board.GetDoor(row.Bus.Id));

                            foreach (Renderer renderer in board.GetBus(row.Bus.Id).GetComponentsInChildren<Renderer>())
                            {
                                if (renderer.name.StartsWith("LE_BUS_", StringComparison.Ordinal))
                                {
                                    report.AppendLine("Body=" + renderer.name + ";min=" + renderer.bounds.min +
                                        ";max=" + renderer.bounds.max);
                                }
                            }
                        }
                    }
                }
            }

            return report.ToString();
        }

        private static string GetConsoleCounts()
        {
            Type entries = typeof(EditorApplication).Assembly.GetType("UnityEditor.LogEntries") ??
                typeof(EditorApplication).Assembly.GetType("UnityEditorInternal.LogEntries");
            MethodInfo counts = entries?.GetMethod("GetCountsByType", BindingFlags.Static | BindingFlags.Public |
                BindingFlags.NonPublic);

            if (counts == null)
            {
                return "Console=Unavailable";
            }

            try
            {
                object[] arguments = { 0, 0, 0 };
                counts.Invoke(null, arguments);
                return "ConsoleErrors=" + arguments[0] + ";Warnings=" + arguments[1] + ";Logs=" + arguments[2];
            }
            catch (TargetInvocationException)
            {
                // Editor console internals are optional diagnostics, never a command precondition.
                return "Console=Unavailable";
            }
        }

        private static string ClearConsole()
        {
            Type entries = typeof(EditorApplication).Assembly.GetType("UnityEditor.LogEntries") ??
                typeof(EditorApplication).Assembly.GetType("UnityEditorInternal.LogEntries");
            MethodInfo clear = entries?.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public |
                BindingFlags.NonPublic);

            if (clear == null)
            {
                return "Console=Unavailable; no action performed";
            }

            clear.Invoke(null, null);
            return "Console cleared";
        }

        private static void AppendRuntimeState(StringBuilder report, LevelComposition composition)
        {
            if (composition.Game == null)
            {
                report.AppendLine("Gameplay=Authored; runtime not initialized");
                return;
            }

            LevelGameSession game = composition.Game;
            BoardSnapshot snapshot = game.Snapshot;
            report.AppendLine("Gameplay=Running;Generation=" + snapshot.Generation + ";Victory=" + snapshot.IsVictory +
                ";Paused=" + game.IsPaused + ";Tutorial=" + game.Tutorial.Step +
                ";SecurityViewing=" + game.IsSecurityViewing + ";Stability=" + snapshot.Stability +
                ";LostSeats=" + snapshot.LostSeats);
            report.AppendLine("LevelFIFO=" + string.Join(",", snapshot.NextBusIds));

            foreach (RowSnapshot row in snapshot.Rows)
            {
                BusSnapshot bus = row.Bus;
                string busState = bus == null ? "empty" : bus.Id + ",route=" + bus.Route +
                    ",leftX=" + bus.LeftX + ",occupied=" + bus.Occupied + "/" + bus.Capacity;
                report.AppendLine("Row=" + row.Id + ";Bus=" + busState);
            }

            foreach (QueueSnapshot queue in snapshot.Queues)
            {
                report.AppendLine("Queue=" + queue.Id + ";Remaining=" + queue.RemainingPassengers.Count);
            }

            report.AppendLine("Trace:");

            foreach (string step in game.Trace)
            {
                report.AppendLine(step);
            }
        }

        private static void WriteResponse(string text)
        {
            Directory.CreateDirectory("TestResults");
            File.WriteAllText(ResponsePath, text);
        }
    }
}
