using System.Runtime.InteropServices;
using UnityEngine;

namespace LastExit.Presentation
{
    // Rendering pixels and browser CSS pixels are separate coordinate systems.
    public static class WebViewportMetrics
    {
        public static float RenderDensity
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return Mathf.Max(1, LastExitRenderDensity());
#else
                return 1;
#endif
            }
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern float LastExitRenderDensity();
#endif
    }
}
