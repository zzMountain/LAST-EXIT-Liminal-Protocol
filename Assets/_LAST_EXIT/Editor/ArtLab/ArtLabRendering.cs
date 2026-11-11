using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace LastExit.Editor.ArtLab
{
    [InitializeOnLoad]
    public static class ArtLabRendering
    {
        private const string Folder = "Assets/_LAST_EXIT/ArtLab/Rendering";
        private const string PipelinePath = Folder + "/ArtLabRefinedPipeline.asset";
        private const string RendererPath = Folder + "/ArtLabRefinedRenderer.asset";
        private const string OverrideKey = "LastExit.ArtLab.Rendering.Override";
        private const string PreviousPathKey = "LastExit.ArtLab.Rendering.PreviousPath";
        private const string PreviousQualityKey = "LastExit.ArtLab.Rendering.PreviousQuality";
        private const string TestingKey = "LastExit.ArtLab.Rendering.Testing";

        private static readonly TestRunCallbacks s_testCallbacks = new TestRunCallbacks();
        private static readonly TestRunnerApi s_testApi;

        static ArtLabRendering()
        {
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
            EditorSceneManager.sceneSaved += OnSceneSaved;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += ReleaseEditorScope;
            EditorApplication.quitting += ReleaseEditorScope;
            s_testApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            s_testApi.hideFlags = HideFlags.HideAndDontSave;
            s_testApi.RegisterCallbacks(s_testCallbacks, int.MaxValue);
            EditorApplication.delayCall += ApplySceneScope;
        }

        public static void Configure(Camera camera)
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            CreatePipeline();
            camera.allowHDR = true;
            camera.allowMSAA = true;
            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.volumeLayerMask = 1;
            cameraData.requiresDepthTexture = true;
            CreateVolume(camera);
            ConfigureEnvironment();
            ApplySceneScope();
            AssetDatabase.SaveAssets();
        }

        private static void CreatePipeline()
        {
            if (AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath) == null)
            {
                AssetDatabase.CopyAsset("Assets/Settings/URP-Balanced.asset", PipelinePath);
            }

            if (AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath) == null)
            {
                AssetDatabase.CopyAsset("Assets/Settings/URP-Balanced-Renderer.asset", RendererPath);
            }

            UniversalRenderPipelineAsset pipeline =
                AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            UniversalRendererData renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            SerializedObject serialized = new SerializedObject(pipeline);
            SerializedProperty renderers = serialized.FindProperty("m_RendererDataList");
            renderers.arraySize = 1;
            renderers.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            serialized.FindProperty("m_DefaultRendererIndex").intValue = 0;
            serialized.FindProperty("m_MainLightShadowmapResolution").intValue = 2048;
            serialized.FindProperty("m_SoftShadowsSupported").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            pipeline.supportsHDR = true;
            pipeline.msaaSampleCount = 4;
            pipeline.shadowDistance = 45;
            pipeline.shadowCascadeCount = 2;
            pipeline.supportsCameraDepthTexture = true;
            ConfigureAmbientOcclusion(renderer);
            EditorUtility.SetDirty(pipeline);
        }

        private static void ConfigureAmbientOcclusion(UniversalRendererData renderer)
        {
            Type featureType = typeof(UniversalRendererData).Assembly.GetType(
                "UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion", true);
            ScriptableRendererFeature feature = null;

            for (int index = 0; index < renderer.rendererFeatures.Count; index++)
            {
                ScriptableRendererFeature candidate = renderer.rendererFeatures[index];

                if (candidate != null && candidate.GetType() == featureType)
                {
                    feature = candidate;

                    if (AssetDatabase.GetAssetPath(feature) != RendererPath)
                    {
                        feature = UnityEngine.Object.Instantiate(feature);
                        AssetDatabase.AddObjectToAsset(feature, renderer);
                        renderer.rendererFeatures[index] = feature;
                    }

                    break;
                }
            }

            if (feature == null)
            {
                feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(featureType);
                feature.name = "Art Lab Medium SSAO";
                AssetDatabase.AddObjectToAsset(feature, renderer);
                renderer.rendererFeatures.Add(feature);
            }

            SerializedObject serialized = new SerializedObject(feature);
            SerializedProperty settings = serialized.FindProperty("m_Settings");
            settings.FindPropertyRelative("AOMethod").enumValueIndex = 1;
            settings.FindPropertyRelative("Downsample").boolValue = true;
            settings.FindPropertyRelative("AfterOpaque").boolValue = false;
            settings.FindPropertyRelative("Source").enumValueIndex = 1;
            settings.FindPropertyRelative("NormalSamples").enumValueIndex = 1;
            settings.FindPropertyRelative("Intensity").floatValue = .6f;
            settings.FindPropertyRelative("DirectLightingStrength").floatValue = .15f;
            settings.FindPropertyRelative("Radius").floatValue = .35f;
            settings.FindPropertyRelative("Samples").enumValueIndex = 1;
            settings.FindPropertyRelative("BlurQuality").enumValueIndex = 1;
            settings.FindPropertyRelative("SampleCount").intValue = -1;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            feature.SetActive(true);
            feature.Create();
            SerializedObject rendererSerialized = new SerializedObject(renderer);
            SerializedProperty featureMap = rendererSerialized.FindProperty("m_RendererFeatureMap");
            featureMap.arraySize = renderer.rendererFeatures.Count;

            for (int index = 0; index < renderer.rendererFeatures.Count; index++)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[index],
                    out string _, out long localId);
                featureMap.GetArrayElementAtIndex(index).longValue = localId;
            }

            rendererSerialized.ApplyModifiedPropertiesWithoutUndo();
            renderer.SetDirty();
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(feature);
        }

        private static void CreateVolume(Camera camera)
        {
            const string ProfilePath = Folder + "/ArtLabRefinedVolume.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);

            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            if (profile.TryGet(out Tonemapping tonemapping) == false)
            {
                tonemapping = profile.Add<Tonemapping>(true);
                AssetDatabase.AddObjectToAsset(tonemapping, profile);
            }

            if (profile.TryGet(out Bloom bloom) == false)
            {
                bloom = profile.Add<Bloom>(true);
                AssetDatabase.AddObjectToAsset(bloom, profile);
            }

            if (profile.TryGet(out ColorAdjustments adjustments) == false)
            {
                adjustments = profile.Add<ColorAdjustments>(true);
                AssetDatabase.AddObjectToAsset(adjustments, profile);
            }

            tonemapping.mode.Override(TonemappingMode.ACES);
            bloom.intensity.Override(.15f);
            bloom.threshold.Override(1.05f);
            bloom.scatter.Override(.55f);
            adjustments.contrast.Override(12);
            adjustments.saturation.Override(-5);
            adjustments.postExposure.Override(.5f);
            EditorUtility.SetDirty(tonemapping);
            EditorUtility.SetDirty(bloom);
            EditorUtility.SetDirty(adjustments);
            EditorUtility.SetDirty(profile);
            GameObject instance = new GameObject("Refined Global Volume", typeof(Volume));
            SceneManager.MoveGameObjectToScene(instance, camera.gameObject.scene);
            Volume volume = instance.GetComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10;
            volume.sharedProfile = profile;
        }

        private static void ConfigureEnvironment()
        {
            const string SkyPath = Folder + "/ArtLabRefinedSky.mat";
            Material sky = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);

            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(sky, SkyPath);
            }

            sky.SetFloat("_SunDisk", 0);
            sky.SetFloat("_AtmosphereThickness", .65f);
            sky.SetFloat("_Exposure", .45f);
            sky.SetColor("_SkyTint", new Color(.32f, .4f, .48f));
            sky.SetColor("_GroundColor", new Color(.11f, .14f, .17f));
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.5f, .6f, .68f);
            RenderSettings.ambientEquatorColor = new Color(.25f, .32f, .38f);
            RenderSettings.ambientGroundColor = new Color(.1f, .1f, .1f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = CreateReflection();
            RenderSettings.reflectionIntensity = .7f;
            RenderSettings.fog = false;
        }

        private static Cubemap CreateReflection()
        {
            const string ReflectionPath = Folder + "/ArtLabStudioReflection.asset";
            const int Size = 64;
            Cubemap cubemap = AssetDatabase.LoadAssetAtPath<Cubemap>(ReflectionPath);

            if (cubemap == null)
            {
                cubemap = new Cubemap(Size, TextureFormat.RGBAHalf, true);
                cubemap.name = "Cold Studio Approximation";
                AssetDatabase.CreateAsset(cubemap, ReflectionPath);
            }

            cubemap.wrapMode = TextureWrapMode.Clamp;
            cubemap.filterMode = FilterMode.Trilinear;

            for (int face = 0; face < 6; face++)
            {
                Color[] pixels = new Color[Size * Size];

                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        float u = (x + .5f) / Size * 2 - 1;
                        float v = (y + .5f) / Size * 2 - 1;
                        Vector3 direction = GetFaceDirection((CubemapFace)face, u, v).normalized;
                        float gradient = Mathf.InverseLerp(-1, 1, direction.y);
                        Color color = Color.Lerp(new Color(.035f, .045f, .055f),
                            new Color(.34f, .4f, .46f), gradient);
                        float stripDistance = (direction.y - .58f) / .16f;
                        float strip = Mathf.Exp(-stripDistance * stripDistance) * 1.7f;
                        color += new Color(.78f, .89f, 1) * strip;
                        color.a = 1;
                        pixels[y * Size + x] = color;
                    }
                }

                cubemap.SetPixels(pixels, (CubemapFace)face);
            }

            cubemap.Apply(true, false);
            EditorUtility.SetDirty(cubemap);
            return cubemap;
        }

        private static Vector3 GetFaceDirection(CubemapFace face, float u, float v)
        {
            switch (face)
            {
                case CubemapFace.PositiveX: return new Vector3(1, -v, -u);
                case CubemapFace.NegativeX: return new Vector3(-1, -v, u);
                case CubemapFace.PositiveY: return new Vector3(u, 1, v);
                case CubemapFace.NegativeY: return new Vector3(u, -1, -v);
                case CubemapFace.PositiveZ: return new Vector3(u, -v, 1);
                default: return new Vector3(-u, -v, -1);
            }
        }

        private static void ApplySceneScope()
        {
            string path = SceneManager.GetActiveScene().path;
            bool ownScene = path == ArtLabSceneBuilder.ScenePath || ArtLabUxScene.IsPreviewScene(path) ||
                path == "Assets/_LAST_EXIT/Scenes/ArtLab/StationL07_ArtLab_UX.unity";

            if (ownScene == false || SessionState.GetBool(TestingKey, false))
            {
                RestorePipeline();
                return;
            }

            UniversalRenderPipelineAsset pipeline =
                AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);

            if (pipeline == null)
            {
                return;
            }

            if (SessionState.GetBool(OverrideKey, false) &&
                SessionState.GetInt(PreviousQualityKey, -1) != QualitySettings.GetQualityLevel())
            {
                RestorePipeline();
            }

            if (SessionState.GetBool(OverrideKey, false) == false)
            {
                SessionState.SetString(PreviousPathKey, AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));
                SessionState.SetInt(PreviousQualityKey, QualitySettings.GetQualityLevel());
                SessionState.SetBool(OverrideKey, true);
            }

            QualitySettings.renderPipeline = pipeline;
        }

        private static void RestorePipeline()
        {
            if (SessionState.GetBool(OverrideKey, false) == false)
            {
                return;
            }

            int activeQuality = QualitySettings.GetQualityLevel();
            int previousQuality = SessionState.GetInt(PreviousQualityKey, activeQuality);
            RenderPipelineAsset previous = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(
                SessionState.GetString(PreviousPathKey, string.Empty));

            if (previousQuality != activeQuality)
            {
                QualitySettings.SetQualityLevel(previousQuality, false);
            }

            QualitySettings.renderPipeline = previous;

            if (previousQuality != activeQuality)
            {
                QualitySettings.SetQualityLevel(activeQuality, false);
            }

            SessionState.SetBool(OverrideKey, false);
        }

        private static void ReleaseEditorScope()
        {
            EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
            EditorSceneManager.sceneSaved -= OnSceneSaved;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= ReleaseEditorScope;
            EditorApplication.quitting -= ReleaseEditorScope;
            EditorApplication.delayCall -= ApplySceneScope;

            try
            {
                RestorePipeline();
            }
            finally
            {
                if (s_testApi != null)
                {
                    try
                    {
                        s_testApi.UnregisterCallbacks(s_testCallbacks);
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(s_testApi);
                    }
                }
            }
        }

        private static void OnActiveSceneChanged(Scene previous, Scene current)
        {
            ApplySceneScope();
        }

        private static void OnSceneSaved(Scene scene)
        {
            ApplySceneScope();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
            {
                RestorePipeline();
                return;
            }

            ApplySceneScope();
        }

        private class TestRunCallbacks : IErrorCallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
                SessionState.SetBool(TestingKey, true);
                RestorePipeline();
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                SessionState.SetBool(TestingKey, false);
                EditorApplication.delayCall += ApplySceneScope;
            }

            public void OnError(string message)
            {
                // Prebuild/compilation failures are terminal too; do not retain the testing pipeline override.
                SessionState.SetBool(TestingKey, false);
                EditorApplication.delayCall += ApplySceneScope;
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
            }
        }
    }
}
