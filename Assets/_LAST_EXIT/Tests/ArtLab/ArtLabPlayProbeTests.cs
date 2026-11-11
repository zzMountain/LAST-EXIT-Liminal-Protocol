using System;
using System.Reflection;
using LastExit.Editor.ArtLab;
using NUnit.Framework;
using UnityEditor;

namespace LastExit.Tests.ArtLab
{
    public class ArtLabPlayProbeTests
    {
        private const string SessionKey = "LAST_EXIT.ArtLab.PlayProbe";

        [Test]
        public void ManualPlayStopRemovesProbePollingAndClearsSessionOwnership()
        {
            Assert.IsFalse(EditorApplication.isPlayingOrWillChangePlaymode);
            Assert.IsFalse(SessionState.GetBool(SessionKey, false), "Do not interrupt a user-owned probe.");
            MethodInfo poll = GetCallback("Poll");
            MethodInfo modeChanged = GetCallback("OnPlayModeChanged");
            Assert.IsFalse(IsPolling(poll), "The fixture must not replace an existing polling registration.");
            EditorApplication.CallbackFunction callback = (EditorApplication.CallbackFunction)Delegate.CreateDelegate(
                typeof(EditorApplication.CallbackFunction), poll);

            try
            {
                // Reproduce only the finite probe's pending ownership; never enter Play or mutate its scene.
                SessionState.SetBool(SessionKey, true);
                EditorApplication.update += callback;
                Assert.IsTrue(IsPolling(poll));
                modeChanged.Invoke(null, new object[] { PlayModeStateChange.ExitingPlayMode });
                Assert.IsFalse(SessionState.GetBool(SessionKey, false));
                Assert.IsFalse(IsPolling(poll));

                // Repeated exit/Edit notifications cannot register polling again or revive the operation.
                modeChanged.Invoke(null, new object[] { PlayModeStateChange.ExitingPlayMode });
                modeChanged.Invoke(null, new object[] { PlayModeStateChange.EnteredEditMode });
                Assert.IsFalse(SessionState.GetBool(SessionKey, false));
                Assert.IsFalse(IsPolling(poll));
            }
            finally
            {
                EditorApplication.update -= callback;
                SessionState.EraseBool(SessionKey);
            }
        }

        private static MethodInfo GetCallback(string name)
        {
            MethodInfo callback = typeof(ArtLabPlayProbe).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(callback, name);
            return callback;
        }

        private static bool IsPolling(MethodInfo poll)
        {
            FieldInfo update = typeof(EditorApplication).GetField("update",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(update, "Unity 2022.3 EditorApplication.update callback storage must be inspectable.");
            Delegate callbacks = (Delegate)update.GetValue(null);

            if (callbacks == null)
            {
                return false;
            }

            foreach (Delegate callback in callbacks.GetInvocationList())
            {
                if (callback.Method == poll)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
