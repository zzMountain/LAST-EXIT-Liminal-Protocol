using LastExit.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LastExit.Tests.EditMode
{
    public class ProjectBaselineTests
    {
        [Test]
        public void BuildContainsOnlyStationL07()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            Assert.That(scenes, Has.Length.EqualTo(1));
            Assert.That(scenes[0].enabled, Is.True);
            Assert.That(scenes[0].path, Is.EqualTo(ProjectSetup.ScenePath));
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenes[0].path), Is.Not.Null);
        }

        [Test]
        public void StationHasOneOrthographicCameraAndNoMissingScripts()
        {
            Scene scene = EditorSceneManager.OpenScene(ProjectSetup.ScenePath, OpenSceneMode.Single);
            GameObject[] roots = scene.GetRootGameObjects();
            int cameras = 0;
            int listeners = 0;

            foreach (GameObject root in roots)
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    int missingScripts = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject);
                    Assert.That(missingScripts, Is.Zero, child.name);
                }

                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                {
                    cameras++;
                    Assert.That(camera.orthographic, Is.True);
                    Assert.That(camera.CompareTag("MainCamera"), Is.True);
                }

                listeners += root.GetComponentsInChildren<AudioListener>(true).Length;
            }

            Assert.That(cameras, Is.EqualTo(1));
            Assert.That(listeners, Is.EqualTo(1));
        }

        [Test]
        public void WebGlBaselineUsesUrpAndIl2CppWithoutThreads()
        {
#if !ENABLE_INPUT_SYSTEM
            Assert.Fail("The new Input System backend must be active after an Editor restart.");
#endif
            Assert.That(Application.unityVersion, Is.EqualTo("2022.3.62f2"));
            Assert.That(EditorUserBuildSettings.activeBuildTarget, Is.EqualTo(BuildTarget.WebGL));
            Assert.That(PlayerSettings.GetScriptingBackend(NamedBuildTarget.WebGL),
                Is.EqualTo(ScriptingImplementation.IL2CPP));
            Assert.That(GraphicsSettings.defaultRenderPipeline, Is.Not.Null);
            Assert.That(PlayerSettings.WebGL.threadsSupport, Is.False);
            Assert.That(EditorSettings.serializationMode, Is.EqualTo(SerializationMode.ForceText));
            Assert.That(VersionControlSettings.mode, Is.EqualTo("Visible Meta Files"));
        }
    }
}
