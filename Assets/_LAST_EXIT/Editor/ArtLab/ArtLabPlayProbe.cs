using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastExit.Editor.ArtLab
{
    [InitializeOnLoad]
    public static class ArtLabPlayProbe
    {
        private const string RunCommand = "LAST EXIT/Art Lab/Run Play Probe";
        private const string SessionKey = "LAST_EXIT.ArtLab.PlayProbe";

        private static readonly Dictionary<Transform, Vector3> s_positions = new Dictionary<Transform, Vector3>();
        private static readonly Dictionary<Transform, Quaternion> s_rotations = new Dictionary<Transform, Quaternion>();
        private static readonly HashSet<Animator> s_moving = new HashSet<Animator>();
        private static readonly Dictionary<Transform, Vector3> s_actorPositions = new Dictionary<Transform, Vector3>();
        private static Animator[] s_animators;
        private static double s_started;

        static ArtLabPlayProbe()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem(RunCommand)]
        public static void Run()
        {
            Scene scene = SceneManager.GetActiveScene();

            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                scene.path != ArtLabSceneBuilder.ScenePath || scene.isDirty)
            {
                throw new InvalidOperationException("Open and save Art Lab in Edit mode before its Play probe.");
            }

            SessionState.SetBool(SessionKey, true);
            EditorApplication.isPlaying = true;
        }

        private static void Poll()
        {
            if (EditorApplication.isPlaying == false)
            {
                return;
            }

            foreach (Animator animator in s_animators)
            {
                foreach (Transform child in animator.GetComponentsInChildren<Transform>())
                {
                    if (s_positions.TryGetValue(child, out Vector3 position) == false)
                    {
                        s_positions.Add(child, child.localPosition);
                        s_rotations.Add(child, child.localRotation);
                    }
                    else if (Vector3.Distance(child.localPosition, position) > .0001f ||
                        Quaternion.Angle(child.localRotation, s_rotations[child]) > .01f)
                    {
                        s_moving.Add(animator);
                    }
                }
            }

            if (EditorApplication.timeSinceStartup - s_started < 2)
            {
                return;
            }

            EditorApplication.update -= Poll;

            try
            {
                ArtLabPlayReport report = new ArtLabPlayReport();
                report.Scene = SceneManager.GetActiveScene().path;
                report.Animators = s_animators.Length;
                report.PlayingAnimators = s_animators.Count(animator => animator.GetCurrentAnimatorClipInfo(0).Length > 0);
                report.MovingHierarchies = s_moving.Count;
                report.PlacementStable = s_actorPositions.All(pair =>
                    Vector3.Distance(pair.Key.position, pair.Value) < .001f);
                report.Clips = s_animators.SelectMany(animator => animator.GetCurrentAnimatorClipInfo(0))
                    .Select(info => info.clip.name).Distinct().ToArray();
                report.Passed = report.Animators == 13 && report.PlayingAnimators == 13 && s_moving.Count == 13 &&
                    report.PlacementStable;
                Directory.CreateDirectory(ArtLabPreview.OutputRoot + "/Reports");
                File.WriteAllText(ArtLabPreview.OutputRoot + "/Reports/unity_play_probe.json",
                    ArtLabReportJson.Serialize(report, true));
                ArtLabPreview.Capture(ArtLabPreview.GetCamera(), ArtLabPreview.OutputRoot +
                    "/Previews/Readability/PlayMode_ColdLiminal_16x9.png", 1600, 900);
            }
            catch (Exception exception)
            {
                // The probe owns this Play session; record failure and return control to Edit Mode.
                Directory.CreateDirectory("TestResults");
                File.WriteAllText("TestResults/ArtLab-play-error.txt", exception.ToString());
                Debug.LogException(exception);
            }
            finally
            {
                SessionState.SetBool(SessionKey, false);
                EditorApplication.isPlaying = false;
            }
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (SessionState.GetBool(SessionKey, false) == false)
            {
                return;
            }

            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= Poll;
                SessionState.SetBool(SessionKey, false);
                return;
            }

            if (state != PlayModeStateChange.EnteredPlayMode)
            {
                return;
            }

            s_positions.Clear();
            s_rotations.Clear();
            s_moving.Clear();
            s_actorPositions.Clear();
            s_animators = SceneManager.GetActiveScene().GetRootGameObjects().Where(root => root.activeSelf)
                .SelectMany(root => root.GetComponentsInChildren<Animator>()).ToArray();
            s_started = EditorApplication.timeSinceStartup;

            foreach (Animator animator in s_animators)
            {
                Transform actor = animator.transform;

                while (actor.parent != null && actor.parent.name.StartsWith(ArtLabSceneBuilder.LookPrefix) == false)
                {
                    actor = actor.parent;
                }

                s_actorPositions.Add(actor, actor.position);
            }

            EditorApplication.update += Poll;
        }
    }
}
