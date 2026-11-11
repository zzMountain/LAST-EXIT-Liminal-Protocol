using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace LastExit.Editor.ArtLab
{
    public static class ArtLabPreview
    {
        private const string CaptureAllCommand = "LAST EXIT/Art Lab/Capture All";
        public static string OutputRoot => Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../80_Assets/Unity_ArtLab"));

        [MenuItem(CaptureAllCommand)]
        public static void CaptureAll()
        {
            Camera camera = GetCamera();
            Vector3 position = camera.transform.position;
            Quaternion rotation = camera.transform.rotation;
            float size = camera.orthographicSize;
            Scene scene = SceneManager.GetActiveScene();
            GameObject original = scene.GetRootGameObjects().Single(root => root.activeSelf &&
                root.name.StartsWith(ArtLabSceneBuilder.LookPrefix));
            ArtLabLook originalLook = (ArtLabLook)Enum.Parse(typeof(ArtLabLook),
                original.name.Substring(ArtLabSceneBuilder.LookPrefix.Length));
            Directory.CreateDirectory(OutputRoot + "/Previews/Readability");

            try
            {
                foreach (ArtLabLook look in Enum.GetValues(typeof(ArtLabLook)))
                {
                    SetLook(look);
                    camera.orthographicSize = ArtLabLayout.LandscapeSize;
                    Capture(camera, OutputRoot + "/Previews/Readability/" + look + "_16x9.png", 1600, 900);
                    camera.orthographicSize = ArtLabLayout.PortraitSize;
                    Capture(camera, OutputRoot + "/Previews/Readability/" + look + "_9x16.png", 900, 1600);
                }
            }
            finally
            {
                SetLook(originalLook);
                camera.transform.SetPositionAndRotation(position, rotation);
                camera.orthographicSize = size;
            }

            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            WriteSceneReport();
        }

        public static void SetLook(ArtLabLook look)
        {
            Scene scene = SceneManager.GetActiveScene();

            if (scene.path != ArtLabSceneBuilder.ScenePath)
            {
                throw new InvalidOperationException("Open the Art Lab scene first.");
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name.StartsWith(ArtLabSceneBuilder.LookPrefix))
                {
                    root.SetActive(root.name == ArtLabSceneBuilder.LookPrefix + look);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            SceneView.RepaintAll();
        }

        public static ArtLabSceneReport WriteSceneReport()
        {
            Scene scene = SceneManager.GetActiveScene();
            ArtLabSceneReport report = new ArtLabSceneReport();
            report.unityVersion = Application.unityVersion;
            report.scenePath = scene.path;
            HashSet<Mesh> passengerMeshes = new HashSet<Mesh>();
            HashSet<Mesh> busMeshes = new HashSet<Mesh>();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.activeSelf && root.name.StartsWith(ArtLabSceneBuilder.LookPrefix))
                {
                    report.activeLook = root.name;
                    report.activeLooks++;
                }

                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    report.missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject);
                }

                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    Mesh mesh = GetMesh(renderer);

                    if (renderer is SkinnedMeshRenderer && mesh != null)
                    {
                        passengerMeshes.Add(mesh);
                    }

                    if (renderer.name.StartsWith("LE_BUS_") && mesh != null)
                    {
                        busMeshes.Add(mesh);
                    }

                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (material == null || material.shader == null || material.shader.isSupported == false ||
                            (material.shader.name != "Universal Render Pipeline/Lit" &&
                            (renderer.GetComponent<TextMesh>() == null || material.shader.name != "GUI/Text Shader")))
                        {
                            report.invalidMaterials++;
                        }
                    }

                    if (renderer.gameObject.activeInHierarchy && mesh != null)
                    {
                        report.activeRenderers++;

                        for (int index = 0; index < mesh.subMeshCount; index++)
                        {
                            report.activeTriangles += (int)mesh.GetIndexCount(index) / 3;
                        }
                    }
                }
            }

            report.passengerMeshSources = passengerMeshes.Count;
            report.busMeshSources = busMeshes.Count;
            report.passengerClips = GetClipCount("PASSENGER_BASE");
            report.doorClips = GetClipCount("Q3_DOOR");
            report.buildScenes = EditorBuildSettings.scenes.Where(entry => entry.enabled).Select(entry => entry.path).ToArray();
            report.passed = report.missingScripts == 0 && report.invalidMaterials == 0 &&
                report.passengerMeshSources == 1 && report.busMeshSources == 4 && report.passengerClips == 3 &&
                report.doorClips == 1 && report.activeLooks == 1 && report.activeRenderers > 0 &&
                scene.path == ArtLabSceneBuilder.ScenePath;
            Directory.CreateDirectory(OutputRoot + "/Reports");
            File.WriteAllText(OutputRoot + "/Reports/unity_scene_audit.json", JsonUtility.ToJson(report, true));
            return report;
        }

        public static Camera GetCamera()
        {
            GameObject root = SceneManager.GetActiveScene().GetRootGameObjects().Single(instance =>
                instance.name == ArtLabSceneBuilder.CameraName);
            return root.GetComponent<Camera>();
        }

        public static void Capture(Camera camera, string path, int width, int height)
        {
            RenderTexture texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            float previousAspect = camera.aspect;
            Texture2D image = null;

            try
            {
                camera.aspect = (float)width / height;
                UniversalRenderPipeline.SingleCameraRequest request = new UniversalRenderPipeline.SingleCameraRequest
                {
                    destination = texture
                };
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = texture;
                image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.aspect = previousAspect;
                texture.Release();
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static Mesh GetMesh(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned)
            {
                return skinned.sharedMesh;
            }

            return renderer.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
        }

        private static int GetClipCount(string modelName)
        {
            return AssetDatabase.LoadAllAssetsAtPath(ArtLabImporter.Root + "/Models/" + modelName + "_v01.fbx")
                .OfType<AnimationClip>().Count(clip => clip.name.StartsWith("__preview__") == false);
        }
    }
}
