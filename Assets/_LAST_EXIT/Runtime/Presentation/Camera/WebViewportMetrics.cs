#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using UnityEngine;

namespace LastExit.Presentation
{
    // Rendering pixels and browser CSS pixels are separate coordinate systems.
    public static class WebViewportMetrics
    {
        public static bool HasDrawableCamera(Camera camera)
        {
            return camera != null && camera.pixelWidth > 0 && camera.pixelHeight > 0 &&
                camera.aspect > 0 && float.IsFinite(camera.aspect);
        }

        public static bool HasArea(Rect rect)
        {
            return rect.width > 0 && rect.height > 0 &&
                float.IsFinite(rect.xMin) && float.IsFinite(rect.xMax) &&
                float.IsFinite(rect.yMin) && float.IsFinite(rect.yMax);
        }

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
