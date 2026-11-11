using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastExit.Editor.ArtLab
{
    public class ArtLabVerification : IErrorCallbacks
    {
        private const string RunCommand = "LAST EXIT/Art Lab/Run Tests";
        private const string ProbeCommand = "LAST EXIT/Art Lab/Verify Runner Error Recovery";
        private const string ContextKey = "LastExit.ArtLab.Verification.Context";
        private const string RestoreContextKey = "LastExit.ArtLab.Verification.RestoreContext";
        private const string GameplayContextKey = "LastExit.Gameplay.Verification.Context";
        private const string GameplayRestoreContextKey = "LastExit.Gameplay.Verification.RestoreContext";
        private const string DefaultReportPrefix = "TestResults/ArtLab-EditMode";
        private const string ErrorReportPrefix = "TestResults/FullAudit-ArtLabRunnerError-Failure";
        private const string ErrorReportPath = "TestResults/FullAudit-ArtLabRunnerError.txt";
        private const string SyntheticFailure = "Synthetic Art Lab runner error; no test was executed.";
        private static ArtLabVerification s_active;

        private readonly TestRunnerApi _api;
        private readonly SceneSetup[] _setup;
        private readonly string _reportPrefix;

        private ArtLabVerification(TestRunnerApi api, SceneSetup[] setup, string reportPrefix)
        {
            _api = api;
            _setup = setup;
            _reportPrefix = reportPrefix;
            s_active = this;
        }

        public static bool IsPending => s_active != null || HasContext(ContextKey) || HasContext(RestoreContextKey);

        [MenuItem(RunCommand)]
        public static void Run()
        {
            EnsureCanExecute();
            TestRunnerApi api = ScriptableObject.CreateInstance<TestRunnerApi>();
            ArtLabVerification callbacks = new ArtLabVerification(api, EditorSceneManager.GetSceneManagerSetup(),
                DefaultReportPrefix);

            try
            {
                api.RegisterCallbacks(callbacks);
                Directory.CreateDirectory("TestResults");
                File.WriteAllText(callbacks._reportPrefix + ".txt", "STARTING: Art Lab EditMode tests");
                File.WriteAllText("TestResults/ArtLab-tests.txt", "STARTING: Art Lab EditMode tests");
                SessionState.SetString(ContextKey, SerializeContext(callbacks._setup, callbacks._reportPrefix));
                Filter filter = new Filter
                {
                    testMode = TestMode.EditMode,
                    assemblyNames = new[] { "LastExit.Tests.EditMode", "LastExit.ArtLab.Tests" }
                };
                api.Execute(new ExecutionSettings(filter));
            }
            catch (Exception exception)
            {
                try
                {
                    callbacks.OnError(exception.ToString());
                }
                catch (Exception cleanupException)
                {
                    // A restoration failure must not replace the original runner startup exception.
                    Debug.LogException(cleanupException);
                }

                throw;
            }
        }

        public static void RecoverInterruptedRun()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Exit Play mode before recovering an interrupted Art Lab run.");
            }

            string json = SessionState.GetString(ContextKey, string.Empty);

            if (string.IsNullOrEmpty(json))
            {
                RestoreScenesWhenReady();
                return;
            }

            VerificationContext context = JsonUtility.FromJson<VerificationContext>(json);
            Directory.CreateDirectory("TestResults");
            const string Interrupted = "INTERRUPTED: run exited before completion; no passing result claimed.";
            File.WriteAllText(context.ReportPrefix + ".txt", Interrupted);
            File.WriteAllText("TestResults/ArtLab-tests.txt", Interrupted);
            SessionState.SetString(RestoreContextKey, json);
            SessionState.EraseString(ContextKey);

            if (s_active != null)
            {
                s_active._api.UnregisterCallbacks(s_active);
                UnityEngine.Object.DestroyImmediate(s_active._api);
                s_active = null;
            }

            RestoreScenesWhenReady();
        }

        [MenuItem(ProbeCommand)]
        public static void VerifyErrorRecovery()
        {
            EnsureCanExecute();
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            string expected = SerializeContext(setup, ErrorReportPrefix);
            TestRunnerApi api = ScriptableObject.CreateInstance<TestRunnerApi>();
            ArtLabVerification callbacks = new ArtLabVerification(api, setup, ErrorReportPrefix);

            try
            {
                api.RegisterCallbacks(callbacks);
                Directory.CreateDirectory("TestResults");
                File.WriteAllText(ErrorReportPath, "IN PROGRESS: " + SyntheticFailure);
                SessionState.SetString(ContextKey, expected);
                callbacks.OnError(SyntheticFailure);
                RestoreScenesWhenReady();

                if (IsPending || api != null ||
                    SerializeContext(EditorSceneManager.GetSceneManagerSetup(), ErrorReportPrefix) != expected ||
                    File.ReadAllText(ErrorReportPrefix + ".txt") != "FAILED: " + SyntheticFailure)
                {
                    throw new InvalidOperationException(
                        "Art Lab runner error recovery retained ownership or scene state.");
                }

                File.WriteAllText(ErrorReportPath, "PASS: OnError recorded FAILED, released its API and ownership, " +
                    "and restored the saved scene setup. No test was executed.");
            }
            finally
            {
                callbacks.ReleaseRun();
                RestoreScenesWhenReady();
            }
        }

        // Keep the TestRunner callback contract together, including the terminal failure path.
        public void RunStarted(ITestAdaptor testsToRun)
        {
            File.WriteAllText(_reportPrefix + ".txt", "RUNNING: " + testsToRun.FullName);
        }

        public void TestStarted(ITestAdaptor test)
        {
        }

        public void TestFinished(ITestResultAdaptor result)
        {
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            if (ReferenceEquals(s_active, this) == false)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory("TestResults");
                File.WriteAllText(_reportPrefix + ".xml", result.ToXml().OuterXml);
                Directory.CreateDirectory(ArtLabPreview.OutputRoot + "/Reports");
                File.WriteAllText(ArtLabPreview.OutputRoot + "/Reports/Unity-EditMode.xml", result.ToXml().OuterXml);
                string summary = "Passed=" + result.PassCount + "; Failed=" + result.FailCount +
                    "; State=" + result.ResultState;
                File.WriteAllText(_reportPrefix + ".txt", summary);
                File.WriteAllText("TestResults/ArtLab-tests.txt", summary);
            }
            finally
            {
                ReleaseRun();
            }
        }

        public void OnError(string message)
        {
            if (ReferenceEquals(s_active, this) == false)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory("TestResults");
                File.WriteAllText(_reportPrefix + ".txt", "FAILED: " + message);

                if (_reportPrefix == DefaultReportPrefix)
                {
                    File.WriteAllText("TestResults/ArtLab-tests.txt", "FAILED: " + message);
                }
            }
            finally
            {
                ReleaseRun();
            }
        }

        [InitializeOnLoadMethod]
        private static void RestorePendingCallbacks()
        {
            EditorApplication.delayCall += RestoreScenesWhenReady;
            string json = SessionState.GetString(ContextKey, string.Empty);

            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            VerificationContext context = JsonUtility.FromJson<VerificationContext>(json);
            TestRunnerApi api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new ArtLabVerification(api, ReadSceneSetup(context), context.ReportPrefix));
        }

        private static void EnsureCanExecute()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorUtility.scriptCompilationFailed)
            {
                throw new InvalidOperationException(
                    "Resolve compilation and finish asset import before Art Lab tests.");
            }

            RestoreScenesWhenReady();

            if (IsPending || HasContext(GameplayContextKey) || HasContext(GameplayRestoreContextKey))
            {
                throw new InvalidOperationException("A project test run or scene restoration is already pending.");
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Exit Play mode before running Art Lab tests.");
            }

            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                if (SceneManager.GetSceneAt(index).isDirty)
                {
                    throw new InvalidOperationException("Save modified scenes before running tests.");
                }
            }
        }

        private static bool HasContext(string key)
        {
            return string.IsNullOrEmpty(SessionState.GetString(key, string.Empty)) == false;
        }

        private static string SerializeContext(SceneSetup[] setup, string reportPrefix)
        {
            VerificationContext context = new VerificationContext
            {
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
        }

        private void ReleaseRun()
        {
            if (ReferenceEquals(s_active, this) == false)
            {
                return;
            }

            try
            {
                SessionState.SetString(RestoreContextKey, SerializeContext(_setup, _reportPrefix));
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

        [Serializable]
        private class VerificationContext
        {
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
