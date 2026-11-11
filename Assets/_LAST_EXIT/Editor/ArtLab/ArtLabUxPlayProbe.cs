using System;
using System.IO;
using System.Linq;
using LastExit.ArtLab.Preview;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LastExit.Editor.ArtLab
{
    [InitializeOnLoad]
    public static class ArtLabUxPlayProbe
    {
        private const string RunCommand = "LAST EXIT/Art Lab/Run UX Play Probe";
        private const string SessionKey = "LastExit.Ux.PlayProbe";
        private const float BeforeSnapOffset = .8f;
        private const float AfterSnapOffset = 1.2f;
        private const float ExpectedSnapOffset = 2;

        private static Mouse s_mouse;
        private static Mouse s_previousMouse;
        private static Touchscreen s_touch;
        private static Touchscreen s_previousTouch;
        private static PreviewBus[] s_buses;
        private static PreviewBusDrag s_drag;
        private static Camera s_camera;
        private static Vector2 s_pick;
        private static Vector2 s_destination;
        private static Vector2 s_reset;
        private static Vector2 s_beforeSnap;
        private static Vector2 s_afterSnap;
        private static int s_phase;
        private static int s_frame;
        private static double s_deadline;
        private static bool s_moved;
        private static bool s_resetWorked;
        private static bool s_rowsStable;
        private static bool s_touchMoved;
        private static bool s_mouseFinalPosition;
        private static bool s_touchFinalPosition;
        private static bool s_disableSnapped;
        private static bool s_previousRunInBackground;
        private static bool s_previousPaused;

        static ArtLabUxPlayProbe()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem(RunCommand)]
        public static void Run()
        {
            Scene scene = SceneManager.GetActiveScene();

            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                ArtLabUxScene.IsPreviewScene(scene.path) == false || scene.isDirty)
            {
                throw new InvalidOperationException("Open and save the UX scene in Edit mode before its play probe.");
            }

            SessionState.SetBool(SessionKey, true);
            s_previousPaused = EditorApplication.isPaused;
            SessionState.SetBool(SessionKey + ".Paused", s_previousPaused);
            SessionState.SetBool(SessionKey + ".RestorePause", true);
            EditorApplication.isPaused = false;
            EditorApplication.isPlaying = true;
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup > s_deadline)
            {
                Finish("Timed out waiting for distinct player frames.");
                return;
            }

            if (Time.frameCount < s_frame + 3)
            {
                EditorApplication.QueuePlayerLoopUpdate();
                return;
            }

            s_frame = Time.frameCount;

            try
            {
                switch (s_phase++)
                {
                    case 0:
                        QueuePointer(s_pick, true);
                        break;
                    case 1:
                        ArtLabUxCapture.CaptureFrames("_Selected");
                        QueuePointer(s_destination, true);
                        break;
                    case 2:
                        QueuePointer(s_destination, false);
                        break;
                    case 3:
                        s_moved = ReferenceEquals(s_drag.Selected, s_buses[0]) &&
                            Mathf.Abs(s_buses[0].transform.position.x - s_buses[0].InitialPosition.x - 4) < .02f;
                        s_rowsStable = s_buses.All(bus =>
                            Mathf.Abs(bus.transform.position.z - bus.InitialPosition.z) < .001f &&
                            Mathf.Abs(bus.transform.position.y - bus.InitialPosition.y) < .001f);
                        QueuePointer(s_reset, true);
                        break;
                    case 4:
                        QueuePointer(s_reset, false);
                        break;
                    case 5:
                        s_resetWorked = s_drag.Selected == null && s_buses.All(bus =>
                            Vector3.Distance(bus.transform.position, bus.InitialPosition) < .001f);
                        QueueTouch(s_pick, UnityEngine.InputSystem.TouchPhase.Began);
                        break;
                    case 6:
                        QueueTouch(s_destination, UnityEngine.InputSystem.TouchPhase.Moved);
                        break;
                    case 7:
                        QueueTouch(s_destination, UnityEngine.InputSystem.TouchPhase.Ended);
                        break;
                    case 8:
                        s_touchMoved = ReferenceEquals(s_drag.Selected, s_buses[0]) &&
                            Mathf.Abs(s_buses[0].transform.position.x - s_buses[0].InitialPosition.x - 4) < .02f;
                        s_drag.ResetLayout();
                        QueuePointer(s_pick, true);
                        break;
                    case 9:
                        QueuePointer(s_beforeSnap, true);
                        break;
                    case 10:
                        QueuePointer(s_afterSnap, false);
                        break;
                    case 11:
                        s_mouseFinalPosition = HasSnapOffset();
                        s_drag.ResetLayout();
                        QueueTouch(s_pick, UnityEngine.InputSystem.TouchPhase.Began);
                        break;
                    case 12:
                        QueueTouch(s_beforeSnap, UnityEngine.InputSystem.TouchPhase.Moved);
                        break;
                    case 13:
                        QueueTouch(s_afterSnap, UnityEngine.InputSystem.TouchPhase.Ended);
                        break;
                    case 14:
                        s_touchFinalPosition = HasSnapOffset();
                        s_drag.ResetLayout();
                        QueuePointer(s_pick, true);
                        break;
                    case 15:
                        QueuePointer(s_afterSnap, true);
                        break;
                    case 16:
                        s_drag.enabled = false;
                        s_disableSnapped = HasSnapOffset();
                        QueuePointer(s_destination, true);
                        break;
                    case 17:
                        s_disableSnapped = s_disableSnapped && HasSnapOffset();
                        s_drag.enabled = true;
                        QueuePointer(s_destination, false);
                        break;
                    case 18:
                        s_disableSnapped = s_disableSnapped && HasSnapOffset();
                        Finish(string.Empty);
                        break;
                }
            }
            catch (Exception exception)
            {
                // A finite diagnostic owns this Play session and must restore input after a failed probe.
                Finish(exception.ToString());
            }
        }

        private static void QueuePointer(Vector2 position, bool pressed)
        {
            MouseState state = new MouseState();
            state.position = position;
            state = state.WithButton(MouseButton.Left, pressed);
            InputSystem.QueueStateEvent(s_mouse, state);
        }

        private static bool HasSnapOffset()
        {
            return Mathf.Abs(s_buses[0].transform.position.x - s_buses[0].InitialPosition.x -
                ExpectedSnapOffset) < .02f;
        }

        private static void QueueTouch(Vector2 position, UnityEngine.InputSystem.TouchPhase phase)
        {
            TouchState state = new TouchState();
            state.touchId = 1;
            state.phase = phase;
            state.position = position;
            InputSystem.QueueStateEvent(s_touch, state);
        }

        private static void Finish(string error)
        {
            EditorApplication.update -= Poll;
            bool passed = s_moved && s_rowsStable && s_resetWorked && s_touchMoved && s_mouseFinalPosition &&
                s_touchFinalPosition && s_disableSnapped && string.IsNullOrEmpty(error);
            string report = "{\n  \"passed\": " + passed.ToString().ToLowerInvariant() +
                ",\n  \"mouseDragSnappedFourUnits\": " + s_moved.ToString().ToLowerInvariant() +
                ",\n  \"rowsAndHeightUnchanged\": " + s_rowsStable.ToString().ToLowerInvariant() +
                ",\n  \"touchDragSnappedFourUnits\": " + s_touchMoved.ToString().ToLowerInvariant() +
                ",\n  \"mouseReleaseUsesFinalPosition\": " + s_mouseFinalPosition.ToString().ToLowerInvariant() +
                ",\n  \"touchEndUsesFinalPosition\": " + s_touchFinalPosition.ToString().ToLowerInvariant() +
                ",\n  \"disableMidDragSnapsAndUnsubscribes\": " + s_disableSnapped.ToString().ToLowerInvariant() +
                ",\n  \"uiResetRestoresAllBusesWithoutDragging\": " + s_resetWorked.ToString().ToLowerInvariant() +
                ",\n  \"error\": \"" + error.Replace("\\", "\\\\").Replace("\"", "\\\"")
                    .Replace("\r", "\\r").Replace("\n", "\\n") + "\"\n}";
            try
            {
                Directory.CreateDirectory(ArtLabPreview.OutputRoot + "/Reports");
                File.WriteAllText(ArtLabPreview.OutputRoot + "/Reports/review_fixes_play_probe.json", report);
            }
            finally
            {
                RestoreInput();
                Application.runInBackground = s_previousRunInBackground;
                SessionState.SetBool(SessionKey, false);
                EditorApplication.isPlaying = false;
            }
        }

        private static void RestoreInput()
        {
            if (s_mouse != null && s_mouse.added)
            {
                InputSystem.RemoveDevice(s_mouse);
            }

            if (s_previousMouse != null && s_previousMouse.added)
            {
                s_previousMouse.MakeCurrent();
            }

            s_mouse = null;

            if (s_touch != null && s_touch.added)
            {
                InputSystem.RemoveDevice(s_touch);
            }

            if (s_previousTouch != null && s_previousTouch.added)
            {
                s_previousTouch.MakeCurrent();
            }

            s_touch = null;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode &&
                SessionState.GetBool(SessionKey + ".RestorePause", false))
            {
                EditorApplication.isPaused = SessionState.GetBool(SessionKey + ".Paused", false);
                SessionState.EraseBool(SessionKey + ".RestorePause");
            }

            if (SessionState.GetBool(SessionKey, false) == false)
            {
                return;
            }

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                s_previousRunInBackground = Application.runInBackground;
                Application.runInBackground = true;
                EditorApplication.isPaused = false;
                Type gameViewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
                EditorWindow.GetWindow(gameViewType).Focus();
                Scene scene = SceneManager.GetActiveScene();
                s_buses = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PreviewBus>())
                    .OrderByDescending(bus => bus.transform.position.z).ToArray();
                s_drag = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<PreviewBusDrag>()).Single();
                s_camera = ArtLabPreview.GetCamera();
                Vector3 center = s_buses[0].GetPhysicalBounds().center;
                s_pick = s_camera.WorldToScreenPoint(center);
                s_destination = s_camera.WorldToScreenPoint(center + Vector3.right * 4);
                s_beforeSnap = s_camera.WorldToScreenPoint(center + Vector3.right * BeforeSnapOffset);
                s_afterSnap = s_camera.WorldToScreenPoint(center + Vector3.right * AfterSnapOffset);
                Button reset = s_drag.GetComponentInChildren<Button>();
                RectTransform resetRect = (RectTransform)reset.transform;
                s_reset = RectTransformUtility.WorldToScreenPoint(s_camera, resetRect.TransformPoint(resetRect.rect.center));
                s_previousMouse = Mouse.current;
                s_mouse = InputSystem.AddDevice<Mouse>("ArtLabDiagnosticMouse");
                s_previousTouch = Touchscreen.current;
                s_touch = InputSystem.AddDevice<Touchscreen>("ArtLabDiagnosticTouch");
                s_phase = 0;
                s_moved = false;
                s_resetWorked = false;
                s_rowsStable = false;
                s_touchMoved = false;
                s_mouseFinalPosition = false;
                s_touchFinalPosition = false;
                s_disableSnapped = false;
                s_frame = Time.frameCount;
                s_deadline = EditorApplication.timeSinceStartup + 30;
                EditorApplication.update += Poll;
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= Poll;
                RestoreInput();
                Application.runInBackground = s_previousRunInBackground;
                SessionState.SetBool(SessionKey, false);
            }
        }
    }
}
