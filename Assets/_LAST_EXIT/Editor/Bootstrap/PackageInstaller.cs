using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace LastExit.Editor
{
    [InitializeOnLoad]
    public static class PackageInstaller
    {
        private const double TimeoutSeconds = 600;
        private const string RunningKey = "LastExit.PackageSetup.Running";
        private const string DeadlineKey = "LastExit.PackageSetup.Deadline";

        private static AddAndRemoveRequest s_request;
        private static ListRequest s_verification;
        private static double s_deadline;

        static PackageInstaller()
        {
            if (Application.isBatchMode && SessionState.GetBool(RunningKey, false))
            {
                s_deadline = SessionState.GetFloat(DeadlineKey, 0f);
                EditorApplication.update += Poll;
            }
        }

        public static void Install()
        {
            string[] additions = { "com.unity.inputsystem@1.7.0" };
            string[] removals =
            {
                "com.unity.collab-proxy", "com.unity.visualscripting", "com.unity.ide.vscode",
                "com.unity.timeline"
            };

            s_deadline = EditorApplication.timeSinceStartup + TimeoutSeconds;
            SessionState.SetBool(RunningKey, true);
            SessionState.SetFloat(DeadlineKey, (float)s_deadline);
            s_request = Client.AddAndRemove(additions, removals);
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup > s_deadline)
            {
                Debug.LogError("Package setup exceeded its ten-minute timeout.");
                Finish(2);
                return;
            }

            if (s_request != null)
            {
                if (s_request.IsCompleted == false)
                {
                    return;
                }

                if (s_request.Status != StatusCode.Success)
                {
                    Debug.LogError(s_request.Error.message);
                    Finish(1);
                    return;
                }

                s_request = null;
            }

            // Also runs after a domain reload: re-query UPM instead of trusting a lost request callback.
            if (s_verification == null)
            {
                s_verification = Client.List(false, false);
                return;
            }

            if (s_verification.IsCompleted == false)
            {
                return;
            }

            if (s_verification.Status != StatusCode.Success)
            {
                Debug.LogError(s_verification.Error.message);
                Finish(1);
                return;
            }

            bool hasInputSystem = false;

            foreach (UnityEditor.PackageManager.PackageInfo package in s_verification.Result)
            {
                if (package.name == "com.unity.inputsystem" && package.version == "1.7.0")
                {
                    hasInputSystem = true;
                }
            }

            if (hasInputSystem == false)
            {
                Debug.LogError("Input System 1.7.0 was not resolved. Inspect Packages/manifest.json.");
                Finish(1);
                return;
            }

            Debug.Log("LAST EXIT packages resolved successfully.");
            Finish(0);
        }

        private static void Finish(int exitCode)
        {
            SessionState.SetBool(RunningKey, false);
            EditorApplication.update -= Poll;
            EditorApplication.Exit(exitCode);
        }
    }
}
