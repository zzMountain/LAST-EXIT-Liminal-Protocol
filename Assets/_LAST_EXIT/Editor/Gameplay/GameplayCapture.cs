using System;
using System.IO;
using System.Linq;
using LastExit.Editor.ArtLab;
using LastExit.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LastExit.Editor.Gameplay
{
    public static class GameplayCapture
    {
        private const string CaptureCommand = "LAST EXIT/Gameplay/Capture Running Campaign Level";
        public static string OutputFolder => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..",
            "80_Assets", "Unity_ArtLab", "Previews", "Gameplay"));

        [MenuItem(CaptureCommand)]
        public static void Capture()
        {
            Scene scene = SceneManager.GetActiveScene();

            if (Application.isPlaying == false || scene.path != LevelSceneAuthoring.ScenePath)
            {
                throw new InvalidOperationException("Enter Play in StationL07 before capturing the gameplay scene.");
            }

            LevelHud hud = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LevelHud>()).Single();
            string levelId = hud.GetComponent<LevelComposition>().Game.LevelId.Replace("-", string.Empty);
            Camera camera = hud.GetComponent<Canvas>().worldCamera;
            RenderTexture previousTarget = camera.targetTexture;
            float previousAspect = camera.aspect;
            float previousSize = camera.orthographicSize;
            Directory.CreateDirectory(OutputFolder);

            try
            {
                CaptureSize(camera, hud, levelId, 1600, 900);
                CaptureSize(camera, hud, levelId, 360, 640);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                camera.aspect = previousAspect;
                camera.orthographicSize = previousSize;
                Canvas.ForceUpdateCanvases();
                hud.Refresh();
                PrepareText(camera.gameObject.scene);
                Canvas.ForceUpdateCanvases();
            }

            Debug.Log("Gameplay captures saved: " + OutputFolder);
        }

        private static void CaptureSize(Camera camera, LevelHud hud, string levelId, int width, int height)
        {
            RenderTexture target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);

            try
            {
                camera.targetTexture = target;
                camera.aspect = (float)width / height;
                Canvas.ForceUpdateCanvases();
                hud.Refresh();
                PrepareText(camera.gameObject.scene);
                Canvas.ForceUpdateCanvases();

                // Resolve dynamic font atlas growth before the exported frame reads the UI mesh.
                if (GraphicsSettings.currentRenderPipeline == null)
                {
                    camera.Render();
                }
                else
                {
                    // URP 14 supports StandardRequest; Camera.Render is for the built-in pipeline.
                    RenderPipeline.StandardRequest request = new RenderPipeline.StandardRequest
                    {
                        destination = target
                    };
                    RenderPipeline.SubmitRenderRequest(camera, request);
                }

                PrepareText(camera.gameObject.scene);
                Canvas.ForceUpdateCanvases();
                ArtLabPreview.Capture(camera, Path.Combine(OutputFolder, levelId + "_" + width + "x" + height + ".png"),
                    width, height);
            }
            finally
            {
                camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static void PrepareText(Scene scene)
        {
            Text[] uiLabels = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Text>(true)).ToArray();

            foreach (Text label in uiLabels)
            {
                if (label.font != null && label.font.dynamic)
                {
                    label.font.RequestCharactersInTexture(label.text, Mathf.Max(1, label.fontSize), label.fontStyle);

                    if (label.resizeTextForBestFit)
                    {
                        int fittedSize = Mathf.Max(1, label.cachedTextGenerator.fontSizeUsedForBestFit);
                        label.font.RequestCharactersInTexture(label.text, fittedSize, label.fontStyle);
                    }
                }
            }

            foreach (TextMesh label in scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<TextMesh>(true)))
            {
                if (label.font != null && label.font.dynamic)
                {
                    int size = label.fontSize > 0 ? label.fontSize : label.font.fontSize;
                    label.font.RequestCharactersInTexture(label.text, Mathf.Max(1, size), label.fontStyle);
                }
            }

            // Mark after all requests: a later request can rebuild the atlas used by an earlier label.
            foreach (Text label in uiLabels)
            {
                if (label.isActiveAndEnabled)
                {
                    label.SetAllDirty();
                }
            }
        }
    }
}
