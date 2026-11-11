using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace LastExit.Editor
{
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/_LAST_EXIT/Scenes/StationL07.unity";
        private const string ApplyCommand = "LAST EXIT/Apply Baseline Settings";
        private const string Root = "Assets/_LAST_EXIT/";

        [MenuItem(ApplyCommand)]
        public static void Apply()
        {
            if (Application.isBatchMode == false &&
                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false)
            {
                return;
            }

            if (Application.unityVersion != "2022.3.62f2")
            {
                throw new InvalidOperationException("Open this project with Unity 2022.3.62f2.");
            }

            string[] folders =
            {
                "Scenes", "Runtime/Domain", "Runtime/Rules", "Runtime/Content", "Runtime/Presentation",
                "Runtime/Platform", "Runtime/Audio", "Resources/Levels", "Art/Materials", "Art/Prefabs",
                "Tests/EditMode", "Tests/PlayMode"
            };

            foreach (string folder in folders)
            {
                Directory.CreateDirectory(Root + folder);
                File.WriteAllText(Root + folder + "/.gitkeep", string.Empty);
            }

            Directory.CreateDirectory("Assets/Plugins/WebGL");
            File.WriteAllText("Assets/Plugins/WebGL/.gitkeep", string.Empty);
            AssetDatabase.Refresh();
            ApplyPlayerSettings();
            ApplyRenderingSettings();
            EnsureScene();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("LAST EXIT baseline saved: Unity 2022.3.62f2, URP, StationL07, WebGL.");
        }

        private static void ApplyPlayerSettings()
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
            EditorSettings.projectGenerationRootNamespace = "LastExit";
            VersionControlSettings.mode = "Visible Meta Files";
            PlayerSettings.companyName = "Last Exit";
            PlayerSettings.productName = "LAST EXIT: Liminal Protocol";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.WebGL, "com.lastexit.liminalprotocol");
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = false;
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = true;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.WebGL, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.threadsSupport = false;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.initialMemorySize = 128;
            PlayerSettings.WebGL.maximumMemorySize = 512;
            PlayerSettings.WebGL.template = "APPLICATION:Default";

            UnityEngine.Object settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0];
            SerializedObject serializedSettings = new SerializedObject(settings);
            SerializedProperty inputHandler = serializedSettings.FindProperty("activeInputHandler");

            if (inputHandler == null)
            {
                throw new InvalidOperationException("Unity activeInputHandler setting was not found.");
            }

            inputHandler.intValue = 1;
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ApplyRenderingSettings()
        {
            string[] pipelineGuids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", new[] { "Assets" });

            foreach (string guid in pipelineGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                UniversalRenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                pipeline.supportsHDR = false;
                pipeline.msaaSampleCount = 1;
                pipeline.supportsCameraDepthTexture = false;
                pipeline.supportsCameraOpaqueTexture = false;
                pipeline.useSRPBatcher = true;
                pipeline.shadowDistance = 20f;
                EditorUtility.SetDirty(pipeline);
            }

            string[] rendererGuids = AssetDatabase.FindAssets("t:ScriptableRendererData", new[] { "Assets/Settings" });

            foreach (string guid in rendererGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ScriptableRendererData renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);

                foreach (ScriptableRendererFeature feature in renderer.rendererFeatures)
                {
                    if (feature != null && feature.GetType().Name == "ScreenSpaceAmbientOcclusion")
                    {
                        feature.SetActive(false);
                        EditorUtility.SetDirty(feature);
                    }
                }

                EditorUtility.SetDirty(renderer);
            }
        }

        private static void EnsureScene()
        {
            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath);
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 12f, -12f), Quaternion.Euler(45f, 0f, 0f));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 8f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.075f, 0.09f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.allowHDR = false;
            camera.allowMSAA = false;

            GameObject lightObject = new GameObject("Directional Light", typeof(Light));
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            light.shadows = LightShadows.None;
            RenderSettings.sun = light;
            if (EditorSceneManager.SaveScene(scene, ScenePath) == false)
            {
                throw new IOException("Could not save the baseline scene: " + ScenePath);
            }

            // Only remove the known template scene on the first setup, never an authored scene.
            AssetDatabase.DeleteAsset("Assets/Scenes/SampleScene.unity");
            AssetDatabase.DeleteAsset("Assets/Readme.asset");
            AssetDatabase.DeleteAsset("Assets/TutorialInfo");
        }
    }
}
