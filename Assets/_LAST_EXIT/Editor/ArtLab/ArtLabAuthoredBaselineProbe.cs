using System;
using System.IO;
using System.Linq;
using LastExit.ArtLab.Preview;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace LastExit.Editor.ArtLab
{
    [InitializeOnLoad]
    public static class ArtLabAuthoredBaselineProbe
    {
        private const string RunCommand = "LAST EXIT/Art Lab/Verify Authored Reset Baseline";
        private const string StateKey = "LastExit.ArtLab.AuthoredBaselineProbe";
        private const float AuthoredOffset = .5f;
        private const float RuntimeOffset = 1;
        private const float PositionTolerance = .001f;
        private const double TimeoutSeconds = 20;

        private static double s_deadline;
        private static int s_startFrame;

        static ArtLabAuthoredBaselineProbe()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;

            if (string.IsNullOrEmpty(SessionState.GetString(StateKey, string.Empty)) == false)
            {
                EditorApplication.update += RestoreWhenReady;
            }
        }

        [MenuItem(RunCommand)]
        public static void Run()
        {
            Scene scene = SceneManager.GetActiveScene();

            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty ||
                ArtLabUxScene.IsPreviewScene(scene.path) == false || SceneManager.sceneCount != 1)
            {
                throw new InvalidOperationException("Open one saved UX preview scene in Edit Mode before this probe.");
            }

            if (string.IsNullOrEmpty(SessionState.GetString(StateKey, string.Empty)) == false)
            {
                throw new InvalidOperationException("The authored-baseline probe is already active.");
            }

            PreviewBus bus = GetBuses(scene)[0];
            ProbeState state = new ProbeState();
            state.Scene = scene.path;
            state.OriginalPosition = bus.transform.position;
            state.AuthoredPosition = bus.transform.position + Vector3.right * AuthoredOffset;
            state.PreviousPaused = EditorApplication.isPaused;
            state.PreviousRunInBackground = Application.runInBackground;
            SaveState(state);
            EditorApplication.update -= RestoreWhenReady;
            EditorApplication.update += RestoreWhenReady;
            bus.transform.position = state.AuthoredPosition;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorApplication.isPaused = false;
            EditorApplication.isPlaying = true;
        }

        public static void Recover()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Exit Play Mode before recovering the authored-baseline probe.");
            }

            string json = SessionState.GetString(StateKey, string.Empty);

            if (string.IsNullOrEmpty(json))
            {
                throw new InvalidOperationException("No authored-baseline probe state is available to recover.");
            }

            ProbeState state = ArtLabReportJson.Deserialize<ProbeState>(json);

            if (SceneManager.GetActiveScene().path != state.Scene || SceneManager.sceneCount != 1)
            {
                throw new InvalidOperationException("Recovery requires the same single scene that the probe opened.");
            }

            RestoreSavedScene();
        }

        private static PreviewBus[] GetBuses(Scene scene)
        {
            return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PreviewBus>())
                .OrderByDescending(bus => bus.transform.position.z).ToArray();
        }

        private static void SaveState(ProbeState state)
        {
            SessionState.SetString(StateKey, ArtLabReportJson.Serialize(state));
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup > s_deadline)
            {
                Finish("Timed out waiting for Play Mode frames.");
                return;
            }

            if (Time.frameCount < s_startFrame + 2)
            {
                EditorApplication.QueuePlayerLoopUpdate();
                return;
            }

            try
            {
                ProbeState state = ArtLabReportJson.Deserialize<ProbeState>(SessionState.GetString(StateKey, string.Empty));
                Scene scene = SceneManager.GetActiveScene();
                PreviewBus bus = GetBuses(scene)[0];
                PreviewBusDrag drag = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<PreviewBusDrag>()).Single();
                state.AwakeCapturedAuthoredPosition =
                    Vector3.Distance(bus.InitialPosition, state.AuthoredPosition) < PositionTolerance;
                bus.Position += Vector3.right * RuntimeOffset;
                drag.ResetLayout();
                state.ResetReturnedToAuthoredPosition =
                    Vector3.Distance(bus.Position, state.AuthoredPosition) < PositionTolerance;
                SaveState(state);
                Finish(string.Empty);
            }
            catch (Exception exception)
            {
                // This diagnostic owns the temporary scene change and must exit Play Mode on failure too.
                Finish(exception.ToString());
            }
        }

        private static void Finish(string error)
        {
            EditorApplication.update -= Poll;

            try
            {
                ProbeState state = ArtLabReportJson.Deserialize<ProbeState>(SessionState.GetString(StateKey, string.Empty));
                state.Error = error;
                state.AwaitingRestore = true;
                SaveState(state);
                WriteReport(state);
            }
            finally
            {
                // The probe owns Play Mode even when its report destination is full or read-only.
                EditorApplication.isPlaying = false;
            }
        }

        private static void RestoreWhenReady()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                return;
            }

            string json = SessionState.GetString(StateKey, string.Empty);

            if (string.IsNullOrEmpty(json))
            {
                EditorApplication.update -= RestoreWhenReady;
                return;
            }

            ProbeState state = ArtLabReportJson.Deserialize<ProbeState>(json);

            if (state.AwaitingRestore)
            {
                if (SceneManager.GetActiveScene().path != state.Scene || SceneManager.sceneCount != 1)
                {
                    EditorApplication.update -= RestoreWhenReady;
                    state.Error = "Automatic restoration stopped because the active scene setup changed.";
                    SaveState(state);
                    WriteReport(state);
                    return;
                }

                Recover();
            }
        }

        private static void WriteReport(ProbeState state)
        {
            string reports = ArtLabPreview.OutputRoot + "/Reports";
            Directory.CreateDirectory(reports);
            File.WriteAllText(reports + "/authored_baseline_play_probe.json", ArtLabReportJson.Serialize(state, true));
        }

        private static void RestoreSavedScene()
        {
            ProbeState state = ArtLabReportJson.Deserialize<ProbeState>(SessionState.GetString(StateKey, string.Empty));

            try
            {
                Scene restored = EditorSceneManager.OpenScene(state.Scene, OpenSceneMode.Single);
                PreviewBus bus = GetBuses(restored)[0];
                state.SavedSceneRestored = restored.isDirty == false &&
                    Vector3.Distance(bus.Position, state.OriginalPosition) < PositionTolerance;
                state.Passed = state.AwakeCapturedAuthoredPosition && state.ResetReturnedToAuthoredPosition &&
                    state.SavedSceneRestored && string.IsNullOrEmpty(state.Error);
            }
            catch (Exception exception)
            {
                state.Error = exception.ToString();
                state.Passed = false;
            }
            finally
            {
                EditorApplication.update -= RestoreWhenReady;
                EditorApplication.isPaused = state.PreviousPaused;
                Application.runInBackground = state.PreviousRunInBackground;
                state.AwaitingRestore = false;
                WriteReport(state);

                if (state.SavedSceneRestored)
                {
                    SessionState.EraseString(StateKey);
                }
                else
                {
                    SaveState(state);
                }
            }
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (string.IsNullOrEmpty(SessionState.GetString(StateKey, string.Empty)))
            {
                return;
            }

            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                ProbeState state = ArtLabReportJson.Deserialize<ProbeState>(SessionState.GetString(StateKey, string.Empty));
                state.PreviousRunInBackground = Application.runInBackground;
                SaveState(state);
                Application.runInBackground = true;
                Type gameViewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
                EditorWindow.GetWindow(gameViewType).Focus();
                s_startFrame = Time.frameCount;
                s_deadline = EditorApplication.timeSinceStartup + TimeoutSeconds;
                EditorApplication.update -= Poll;
                EditorApplication.update += Poll;
            }
            else if (change == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= Poll;
                ProbeState state = ArtLabReportJson.Deserialize<ProbeState>(SessionState.GetString(StateKey, string.Empty));
                Application.runInBackground = state.PreviousRunInBackground;
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                ProbeState state = ArtLabReportJson.Deserialize<ProbeState>(SessionState.GetString(StateKey, string.Empty));
                state.AwaitingRestore = true;
                SaveState(state);
            }
        }

        [Serializable]
        private class ProbeState
        {
            [FormerlySerializedAs("scene")]
            public string Scene;
            [FormerlySerializedAs("originalPosition")]
            public Vector3 OriginalPosition;
            [FormerlySerializedAs("authoredPosition")]
            public Vector3 AuthoredPosition;
            [FormerlySerializedAs("previousPaused")]
            public bool PreviousPaused;
            [FormerlySerializedAs("previousRunInBackground")]
            public bool PreviousRunInBackground;
            [FormerlySerializedAs("awakeCapturedAuthoredPosition")]
            public bool AwakeCapturedAuthoredPosition;
            [FormerlySerializedAs("resetReturnedToAuthoredPosition")]
            public bool ResetReturnedToAuthoredPosition;
            [FormerlySerializedAs("savedSceneRestored")]
            public bool SavedSceneRestored;
            [FormerlySerializedAs("awaitingRestore")]
            public bool AwaitingRestore;
            [FormerlySerializedAs("passed")]
            public bool Passed;
            [FormerlySerializedAs("error")]
            public string Error;
        }
    }
}
