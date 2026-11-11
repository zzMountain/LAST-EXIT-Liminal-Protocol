using System.Runtime.InteropServices;

namespace LastExit.Presentation
{
    public static class LevelAudioDiagnostics
    {
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void ReportCue(LevelSound sound)
        {
#if UNITY_WEBGL && !UNITY_EDITOR && DEVELOPMENT_BUILD
            LastExitAudioCue(sound.ToString());
#endif
        }

        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void ReportState(bool activated, bool paused, bool muted, int voices)
        {
#if UNITY_WEBGL && !UNITY_EDITOR && DEVELOPMENT_BUILD
            LastExitAudioState(activated ? 1 : 0, paused ? 1 : 0, muted ? 1 : 0, voices);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR && DEVELOPMENT_BUILD
        [DllImport("__Internal")]
        private static extern void LastExitAudioCue(string sound);

        [DllImport("__Internal")]
        private static extern void LastExitAudioState(int activated, int paused, int muted, int voices);
#endif
    }
}
