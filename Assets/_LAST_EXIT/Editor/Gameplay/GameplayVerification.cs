using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastExit.Editor.Gameplay
{
    public class GameplayVerification : IErrorCallbacks
    {
        private const string ResultsFolder = "TestResults";
        private const string RunCommand = "LAST EXIT/Gameplay/Run EditMode Tests";
        private const string RunPlayModeCommand = "LAST EXIT/Gameplay/Run PlayMode Tests";
        private const string RunPaceCommand = "LAST EXIT/Gameplay/Run Passenger Walking Regression";
        private const string RunPortraitCommand = "LAST EXIT/Gameplay/Run Portrait Campaign Regression";
        private const string RunStopPreviewCommand = "LAST EXIT/Gameplay/Run Stop Preview Regression";
        private const string ContextKey = "LastExit.Gameplay.Verification.Context";
        private const string RestoreContextKey = "LastExit.Gameplay.Verification.RestoreContext";
        private const string DefaultReportPrefix = ResultsFolder + "/FullFinal-20261108-";
        private const string ErrorRecoveryPrefix = ResultsFolder + "/FullAudit-RunnerError-Failure";
        private const string ErrorRecoveryReport = ResultsFolder + "/FullAudit-RunnerError.txt";
        private const string SyntheticFailure = "Synthetic runner error recovery diagnostic; no test was executed.";
        private static GameplayVerification s_active;

        private readonly TestRunnerApi _api;
        private readonly SceneSetup[] _setup;
        private readonly TestMode _mode;
        private readonly string _reportPrefix;

        private GameplayVerification(TestRunnerApi api, SceneSetup[] setup, TestMode mode, string reportPrefix = null)
        {
            _api = api;
            _setup = setup;
            _mode = mode;
            _reportPrefix = string.IsNullOrEmpty(reportPrefix) ? DefaultReportPrefix + mode : reportPrefix;
            s_active = this;
        }

        [MenuItem(RunCommand)]
        public static void Run()
        {
            Execute(TestMode.EditMode);
        }

        [MenuItem(RunPlayModeCommand)]
        public static void RunPlayMode()
        {
            Execute(TestMode.PlayMode);
        }

        public static void RunAudio()
        {
            Execute(TestMode.PlayMode, "LastExit.Tests.Gameplay.PlayMode.AudioFeedbackPlayModeTests");
        }

        public static void RunSecurityPanel()
        {
            Execute(TestMode.PlayMode, "LastExit.Tests.Gameplay.PlayMode.CctvPanelPlayModeTests");
        }

        public static void RunBoarding()
        {
            const string Fixture = "LastExit.Tests.Gameplay.PlayMode.LevelScenePlayModeTests.";
            Execute(TestMode.PlayMode,
                "LastExit.Tests.Gameplay.PlayMode.BoardingFeedbackPlayModeTests",
                Fixture + "BoardingWalkAnimatesRigAndAdvancesQueueWithoutTeleporting",
                Fixture + "BoardingBatchWaitsForEveryWalkAndHopBeforeDispatchingNextHeads",
                Fixture + "PauseResumesBothWalkersAndRetryCancelsEveryOldCallback",
                Fixture + "MouseAndTouchCanMoveAnotherBusWhilePassengerKeepsWalking");
        }

        public static void RunReadiness()
        {
            const string Fixture = "LastExit.Tests.Gameplay.PlayMode.LevelScenePlayModeTests.";
            Execute(TestMode.PlayMode,
                "LastExit.Tests.Platform.PlayMode.PlatformReadinessPlayModeTests",
                "LastExit.Tests.Gameplay.PlayMode.CompositionLifecyclePlayModeTests",
                "LastExit.Tests.Gameplay.PlayMode.CctvPanelPlayModeTests.OverlappingPhoneTargetsSelectNearestHeadThroughActualGraphicRaycast",
                Fixture + "LaterCampaignRowsAndLongBodiesFitBothOrientationsAndKeepDoorAlignment",
                Fixture + "BlockedBusShowsImmediateHintAndRetryClearsIt",
                Fixture + "BoardingBatchWaitsForEveryWalkAndHopBeforeDispatchingNextHeads",
                Fixture + "BoardingWalkAnimatesRigAndAdvancesQueueWithoutTeleporting",
                Fixture + "MouseLandscapeRunsActualAnimationsToVictory",
                Fixture + "EmulatedTouchPortraitRunsSameStateAndTraceToVictory",
                Fixture + "RestartDuringBoardDepartureAndArrivalCancelsOldAnimations");
        }

        public static void RecoverInterruptedRun()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Exit interrupted Play mode before recovering test ownership.");
            }

            string context = SessionState.GetString(ContextKey, string.Empty);

            if (string.IsNullOrEmpty(context))
            {
                return;
            }

            VerificationContext saved = JsonUtility.FromJson<VerificationContext>(context);
            string reportPrefix = string.IsNullOrEmpty(saved.ReportPrefix)
                ? DefaultReportPrefix + saved.Mode : saved.ReportPrefix;
            File.WriteAllText(reportPrefix + ".txt",
                "INTERRUPTED: run exited before completion; no passing result claimed.");
            SessionState.EraseString(ContextKey);
            SessionState.SetString(RestoreContextKey, context);

            if (s_active != null)
            {
                s_active._api.UnregisterCallbacks(s_active);
                UnityEngine.Object.DestroyImmediate(s_active._api);
                s_active = null;
            }

            RestoreScenesWhenReady();
        }

        [MenuItem(RunStopPreviewCommand)]
        public static void RunStopPreview()
        {
            Execute(TestMode.PlayMode,
                "LastExit.Tests.Gameplay.PlayMode.LevelScenePlayModeTests." +
                "DoorStopPreviewAndMagnetMatchMouseAndTouchRelease");
        }

        [MenuItem(RunPaceCommand)]
        public static void RunPace()
        {
            const string Fixture = "LastExit.Tests.Gameplay.PlayMode.LevelScenePlayModeTests.";
            Execute(TestMode.PlayMode,
                Fixture + "BlockedBusShowsImmediateHintAndRetryClearsIt",
                Fixture + "BoardingBatchWaitsForEveryWalkAndHopBeforeDispatchingNextHeads",
                Fixture + "BoardingWalkAnimatesRigAndAdvancesQueueWithoutTeleporting",
                Fixture + "MouseLandscapeRunsActualAnimationsToVictory",
                Fixture + "EmulatedTouchPortraitRunsSameStateAndTraceToVictory",
                Fixture + "RestartDuringBoardDepartureAndArrivalCancelsOldAnimations");
        }

        [MenuItem(RunPortraitCommand)]
        public static void RunPortraitCampaign()
        {
            Execute(TestMode.PlayMode,
                "LastExit.Tests.Gameplay.PlayMode.LevelScenePlayModeTests." +
                "CampaignPortraitCompletesFourLevelsAndResetsInterruptedAttempts");
        }

        public static void RunLandscapeCampaign()
        {
            Execute(TestMode.PlayMode,
                "LastExit.Tests.Gameplay.PlayMode.LevelScenePlayModeTests." +
                    "CampaignLandscapeCompletesFourLevelsAndResetsInterruptedAttempts");
        }

        public static void RunQuarantine()
        {
            Execute(TestMode.PlayMode, "LastExit.Tests.Gameplay.PlayMode.QuarantineChamberPlayModeTests",
                "LastExit.Tests.Gameplay.PlayMode.LevelScenePlayModeTests." +
                    "RestartDuringBoardDepartureAndArrivalCancelsOldAnimations",
                "LastExit.Tests.Gameplay.PlayMode.LevelScenePlayModeTests." +
                    "DisableAndReenableDuringBoardingRestartsWithoutOldCallbacks");
        }

        public static void RunCompactLayout()
        {
            const string Fixture = "LastExit.Tests.Gameplay.PlayMode.LevelScenePlayModeTests.";
            Execute(TestMode.PlayMode,
                Fixture + "BoardingRefreshProjectsInspectionTargetOntoNextQueueHead",
                Fixture + "LaterCampaignRowsAndLongBodiesFitBothOrientationsAndKeepDoorAlignment",
                Fixture + "SmallBusKeepsFortyFourPixelTouchTargetAtPhoneAndShortLandscape",
                Fixture + "FullLandscapeBlockerCaptionFitsBetweenQueueCountAndBusBadge",
                Fixture + "CapacityBadgesClearBusSilhouettesAtBothOrientations",
                Fixture + "DoorStopPreviewAndMagnetMatchMouseAndTouchRelease");
        }

        public static void VerifyErrorRecovery()
        {
            EnsureCanExecute();
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            string expectedSetup = SerializeContext(setup, TestMode.EditMode, ErrorRecoveryPrefix);
            TestRunnerApi api = ScriptableObject.CreateInstance<TestRunnerApi>();
            GameplayVerification callbacks = new GameplayVerification(api, setup, TestMode.EditMode,
                ErrorRecoveryPrefix);

            try
            {
                api.RegisterCallbacks(callbacks);
                Directory.CreateDirectory(ResultsFolder);
                File.WriteAllText(ErrorRecoveryReport, "IN PROGRESS: " + SyntheticFailure);
                File.WriteAllText(ErrorRecoveryPrefix + ".txt", "STARTING: " + SyntheticFailure);
                SessionState.SetString(ContextKey, expectedSetup);
                callbacks.OnError(SyntheticFailure);

                if (string.IsNullOrEmpty(SessionState.GetString(ContextKey, string.Empty)) == false ||
                    s_active != null || api != null)
                {
                    throw new InvalidOperationException("Runner error retained pending ownership or its API.");
                }

                RestoreScenesWhenReady();
                string restoredSetup = SerializeContext(EditorSceneManager.GetSceneManagerSetup(),
                    TestMode.EditMode, ErrorRecoveryPrefix);

                if (string.IsNullOrEmpty(SessionState.GetString(RestoreContextKey, string.Empty)) == false ||
                    restoredSetup != expectedSetup)
                {
                    throw new InvalidOperationException("Runner error did not restore the original scene setup.");
                }

                string failure = File.ReadAllText(ErrorRecoveryPrefix + ".txt");

                if (failure.StartsWith("FAILED: ", StringComparison.Ordinal) == false ||
                    failure.Contains(SyntheticFailure) == false)
                {
                    throw new InvalidOperationException("Runner error report did not record the synthetic failure.");
                }

                File.WriteAllText(ErrorRecoveryReport,
                    "PASS: synthetic OnError released pending ownership, destroyed its API, " +
                    "restored the scene setup and recorded FAILED instead of RUNNING. No test was executed.");
            }
            finally
            {
                callbacks.ReleaseRun();
                RestoreScenesWhenReady();
            }
        }

        // Keep the TestRunner callback contract together, including its failure terminal path.
        public void RunStarted(ITestAdaptor testsToRun)
        {
            File.WriteAllText(_reportPrefix + ".txt", "RUNNING: " + testsToRun.FullName);
        }

        public void TestStarted(ITestAdaptor test)
        {
            File.WriteAllText(_reportPrefix + "-Current.txt", test.FullName);
        }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.Test.IsSuite == false)
            {
                File.AppendAllText(_reportPrefix + "-Progress.txt",
                    result.ResultState + ": " + result.FullName + Environment.NewLine +
                    (result.FailCount > 0 ? result.Message + Environment.NewLine : string.Empty));
            }
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            if (ReferenceEquals(s_active, this) == false)
            {
                return;
            }

            string summary = "Passed=" + result.PassCount + "; Failed=" + result.FailCount +
                "; State=" + result.ResultState;

            try
            {
                Directory.CreateDirectory(ResultsFolder);
                File.WriteAllText(_reportPrefix + ".xml", result.ToXml().OuterXml);
                File.WriteAllText(_reportPrefix + ".txt", summary);
            }
            finally
            {
                ReleaseRun();
            }

            Debug.Log("Gameplay " + _mode + " tests: " + summary);
        }

        public void OnError(string message)
        {
            if (ReferenceEquals(s_active, this) == false)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(ResultsFolder);
                File.WriteAllText(_reportPrefix + ".txt", "FAILED: " + message);
            }
            finally
            {
                ReleaseRun(true);
            }
        }

        [InitializeOnLoadMethod]
        private static void RestorePendingCallbacks()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.delayCall += RestoreScenesWhenReady;
            string json = SessionState.GetString(ContextKey, string.Empty);

            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            VerificationContext context = JsonUtility.FromJson<VerificationContext>(json);
            SceneSetup[] setup = ReadSceneSetup(context);
            TestRunnerApi api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new GameplayVerification(api, setup, context.Mode, context.ReportPrefix));
        }

        private static void Execute(TestMode mode, params string[] testNames)
        {
            EnsureCanExecute();
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            TestRunnerApi api = ScriptableObject.CreateInstance<TestRunnerApi>();
            GameplayVerification callbacks = new GameplayVerification(api, setup, mode);

            try
            {
                api.RegisterCallbacks(callbacks);
                string[] assemblies = mode == TestMode.PlayMode
                    ? new[] { "LastExit.Gameplay.Tests.PlayMode", "LastExit.Platform.Tests.PlayMode" }
                    : CompilationPipeline.GetAssemblies(AssembliesType.Editor)
                    .Where(assembly => assembly.name.StartsWith("LastExit.", StringComparison.Ordinal) &&
                        assembly.name.Contains(".Tests") &&
                        assembly.name.EndsWith(".PlayMode", StringComparison.Ordinal) == false)
                    .Select(assembly => assembly.name).ToArray();
                Filter filter = new Filter
                {
                    testMode = mode,
                    assemblyNames = assemblies,
                    testNames = testNames.Length == 0 ? null : testNames
                };
                Directory.CreateDirectory(ResultsFolder);
                File.WriteAllText(callbacks._reportPrefix + ".txt", "STARTING: " + string.Join(", ", assemblies));
                File.WriteAllText(callbacks._reportPrefix + "-Progress.txt", string.Empty);
                SessionState.SetString(ContextKey, SerializeContext(setup, mode, callbacks._reportPrefix));
                api.Execute(new ExecutionSettings(filter));
            }
            catch
            {
                try
                {
                    callbacks.ReleaseRun(true);
                }
                catch (Exception cleanupException)
                {
                    // Cleanup diagnostics must not replace the original startup exception.
                    Debug.LogException(cleanupException);
                }

                throw;
            }
        }

        private static void EnsureCanExecute()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                throw new InvalidOperationException(
                    "Wait for script compilation and asset import before running tests.");
            }

            if (EditorUtility.scriptCompilationFailed)
            {
                throw new InvalidOperationException(
                    "Resolve script compilation errors before running automated tests.");
            }

            // A completed PlayMode run may return before its deferred scene-restore callback is pumped.
            RestoreScenesWhenReady();

            if (LastExit.Editor.ArtLab.ArtLabVerification.IsPending)
            {
                throw new InvalidOperationException("An Art Lab test run is already pending or restoring scenes.");
            }

            if (s_active != null || string.IsNullOrEmpty(SessionState.GetString(ContextKey, string.Empty)) == false)
            {
                throw new InvalidOperationException("A gameplay test run is already pending.");
            }

            if (string.IsNullOrEmpty(SessionState.GetString(RestoreContextKey, string.Empty)) == false)
            {
                throw new InvalidOperationException("The previous gameplay test run is restoring its scene setup.");
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Exit Play mode before running automated tests.");
            }

            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                if (SceneManager.GetSceneAt(index).isDirty)
                {
                    throw new InvalidOperationException("Save modified scenes before running gameplay tests.");
                }
            }
        }

        private static string SerializeContext(SceneSetup[] setup, TestMode mode, string reportPrefix = null)
        {
            VerificationContext context = new VerificationContext
            {
                Mode = mode,
                ReportPrefix = reportPrefix,
                Scenes = setup.Select(scene => new SceneRecord
                {
                    Path = scene.path,
                    IsLoaded = scene.isLoaded,
                    IsActive = scene.isActive
                }).ToArray()
            };
            return JsonUtility.ToJson(context);
        }

        private static SceneSetup[] ReadSceneSetup(VerificationContext context)
        {
            return context.Scenes.Select(scene => new SceneSetup
            {
                path = scene.Path,
                isLoaded = scene.IsLoaded,
                isActive = scene.IsActive
            }).ToArray();
        }

        private static void RestoreScenesWhenReady()
        {
            string json = SessionState.GetString(RestoreContextKey, string.Empty);

            if (string.IsNullOrEmpty(json) || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RestoreScenesWhenReady;
                return;
            }

            VerificationContext context = JsonUtility.FromJson<VerificationContext>(json);
            EditorSceneManager.RestoreSceneManagerSetup(ReadSceneSetup(context));
            SessionState.EraseString(RestoreContextKey);
            Debug.Log("Gameplay test scene setup restored in Edit mode.");
        }

        private void ReleaseRun(bool stopPlayMode = false)
        {
            if (ReferenceEquals(s_active, this) == false)
            {
                return;
            }

            try
            {
                SessionState.SetString(RestoreContextKey, SerializeContext(_setup, _mode, _reportPrefix));

                if (stopPlayMode && EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EditorApplication.isPlaying = false;
                }

                _api.UnregisterCallbacks(this);
            }
            finally
            {
                SessionState.EraseString(ContextKey);
                s_active = null;
                UnityEngine.Object.DestroyImmediate(_api);
                EditorApplication.delayCall += RestoreScenesWhenReady;
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.delayCall += RestoreScenesWhenReady;
            }
        }

        [Serializable]
        private class VerificationContext
        {
            public TestMode Mode;
            public string ReportPrefix;
            public SceneRecord[] Scenes;
        }

        [Serializable]
        private class SceneRecord
        {
            public string Path;
            public bool IsLoaded;
            public bool IsActive;
        }
    }
}
