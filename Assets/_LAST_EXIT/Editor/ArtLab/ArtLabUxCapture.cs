using System;
using System.IO;
using System.Linq;
using LastExit.ArtLab.Preview;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastExit.Editor.ArtLab
{
    public static class ArtLabUxCapture
    {
        private const string CaptureCommand = "LAST EXIT/Art Lab/Capture UX Preview";
        public static string OutputFolder => ArtLabPreview.OutputRoot + "/Previews/ReviewFixes";

        [MenuItem(CaptureCommand)]
        public static void Capture()
        {
            CaptureFrames(string.Empty);
        }

        internal static void CaptureFrames(string suffix)
        {
            Scene scene = SceneManager.GetActiveScene();

            if (ArtLabUxScene.IsPreviewScene(scene.path) == false)
            {
                throw new InvalidOperationException("Open the UX preview scene before capturing it.");
            }

            Camera camera = ArtLabPreview.GetCamera();
            ArtLabPreviewHud hud = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<ArtLabPreviewHud>()).Single();
            float size = camera.orthographicSize;
            float aspect = camera.aspect;
            RenderTexture previousTarget = camera.targetTexture;
            Directory.CreateDirectory(OutputFolder);

            try
            {
                CaptureSize(camera, hud, 1600, 900, suffix);
                CaptureSize(camera, hud, 900, 1600, suffix);
                CaptureSize(camera, hud, 360, 640, suffix);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                camera.aspect = aspect;
                camera.orthographicSize = size;
                Canvas.ForceUpdateCanvases();
                hud.RefreshLayout();
            }

            if (Application.isPlaying == false)
            {
                EditorSceneManager.SaveScene(scene);
            }
        }

        private static void CaptureSize(Camera camera, ArtLabPreviewHud hud, int width, int height, string suffix)
        {
            RenderTexture target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);

            try
            {
                camera.targetTexture = target;
                camera.aspect = (float)width / height;
                camera.orthographicSize = width > height ? ArtLabUxScene.LandscapeSize : ArtLabUxScene.PortraitSize;
                Canvas.ForceUpdateCanvases();
                hud.RefreshLayout();
                Canvas.ForceUpdateCanvases();
                ArtLabPreview.Capture(camera, OutputFolder + "/UX_" + width + "x" + height + suffix + ".png",
                    width, height);
            }
            finally
            {
                camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
