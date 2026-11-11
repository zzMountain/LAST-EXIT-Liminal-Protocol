using System.Collections.Generic;
using System.IO;
using LastExit.Content;
using LastExit.Platform;
using LastExit.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastExit.Tests.Gameplay
{
    public class CampaignBindingsTests
    {
        private const string ScenePath = "Assets/_LAST_EXIT/Scenes/StationL07.unity";
        private const string LevelsPath = "Assets/_LAST_EXIT/Resources/Levels/";
        private const int CampaignLength = 15;

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        [TestCase(11)]
        [TestCase(12)]
        [TestCase(13)]
        [TestCase(14)]
        [TestCase(15)]
        public void CampaignResourceMatchesAuthoritativeSourceBytes(int levelIndex)
        {
            string fileName = "level_" + levelIndex.ToString("00") + ".json";
            string projectPath = Directory.GetParent(Application.dataPath).FullName;
            string sourcePath = Path.Combine(projectPath, "SourceData", "Levels", fileName);
            string importedPath = Path.Combine(Application.dataPath, "_LAST_EXIT", "Resources", "Levels", fileName);
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(LevelsPath + fileName);
            Assert.That(asset, Is.Not.Null, fileName);
            CollectionAssert.AreEqual(File.ReadAllBytes(sourcePath), File.ReadAllBytes(importedPath), fileName);
            Assert.That(LevelJsonImporter.Parse(asset.text).Id, Is.EqualTo("LEVEL-" + levelIndex.ToString("00")));
        }

        [Test]
        public void SavedCampaignHasOrderedContentCompleteBindingsAndNoMissingScripts()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedForInspection = scene.isLoaded == false;

            if (openedForInspection)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }

            try
            {
                Assert.IsFalse(scene.isDirty, "Serialized campaign inspection requires a saved scene.");
                GameObject[] roots = scene.GetRootGameObjects();

                foreach (GameObject root in roots)
                {
                    foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    {
                        Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject),
                            Is.Zero, child.name);
                    }
                }

                LevelComposition composition = GetOnlyComponent<LevelComposition>(roots);
                LevelBoardView board = GetOnlyComponent<LevelBoardView>(roots);
                LevelHud hud = GetOnlyComponent<LevelHud>(roots);
                LevelSecurityPanel security = GetOnlyComponent<LevelSecurityPanel>(roots);
                YandexPlatform platform = GetOnlyComponent<YandexPlatform>(roots);
                AssertReferences(composition, "_level", "_camera", "_input", "_board", "_hud", "_drag", "_sequence", "_platform", "_audio");
                AssertReferences(board, "_busTemplate", "_standardBusTemplate", "_longBusTemplate", "_passengerTemplate",
                    "_passengerController", "_actors");
                AssertReferences(hud, "_canvas", "_scaler", "_layout", "_framing", "_header", "_footer",
                    "_counts", "_title", "_subtitle", "_hint", "_arrivals", "_status", "_restart", "_continue",
                    "_victoryPanel", "_repeat", "_next", "_resultTitle", "_resultDetails", "_resultCard",
                    "_security", "_badgeTemplate", "_overlay");
                AssertReferences(security, "_scan", "_checkPanel", "_checkCard", "_checkText", "_checkScan", "_risk");
                AssertReferenceArray(security, "_quarantine", 4);
                AssertReferenceArray(board, "_routeMaterials", 4);
                AssertReferenceArray(board, "_emblemMaterials", 4);

                SerializedObject serialized = new SerializedObject(composition);
                SerializedProperty levels = serialized.FindProperty("_levels");
                Assert.That(levels, Is.Not.Null);
                Assert.That(levels.arraySize, Is.EqualTo(CampaignLength));

                for (int index = 0; index < CampaignLength; index++)
                {
                    string assetPath = LevelsPath + "level_" + (index + 1).ToString("00") + ".json";
                    TextAsset expected = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
                    Assert.That(expected, Is.Not.Null, assetPath);
                    Assert.That(levels.GetArrayElementAtIndex(index).objectReferenceValue, Is.EqualTo(expected));
                }

                Assert.That(serialized.FindProperty("_board").objectReferenceValue, Is.EqualTo(board));
                Assert.That(serialized.FindProperty("_hud").objectReferenceValue, Is.EqualTo(hud));
                Assert.That(serialized.FindProperty("_platform").objectReferenceValue, Is.EqualTo(platform));
                Assert.That(new SerializedObject(hud).FindProperty("_security").objectReferenceValue,
                    Is.EqualTo(security));
            }
            finally
            {
                if (openedForInspection)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static T GetOnlyComponent<T>(IEnumerable<GameObject> roots) where T : Component
        {
            List<T> components = new List<T>();

            foreach (GameObject root in roots)
            {
                components.AddRange(root.GetComponentsInChildren<T>(true));
            }

            Assert.That(components.Count, Is.EqualTo(1), typeof(T).Name);
            return components[0];
        }

        private static void AssertReferences(UnityEngine.Object owner, params string[] names)
        {
            SerializedObject serialized = new SerializedObject(owner);

            foreach (string name in names)
            {
                SerializedProperty property = serialized.FindProperty(name);
                Assert.That(property, Is.Not.Null, owner.name + "." + name);
                Assert.That(property.objectReferenceValue, Is.Not.Null, owner.name + "." + name);
            }
        }

        private static void AssertReferenceArray(UnityEngine.Object owner, string name, int expectedLength)
        {
            SerializedProperty array = new SerializedObject(owner).FindProperty(name);
            Assert.That(array, Is.Not.Null, owner.name + "." + name);
            Assert.That(array.arraySize, Is.EqualTo(expectedLength), owner.name + "." + name);

            for (int index = 0; index < expectedLength; index++)
            {
                Assert.That(array.GetArrayElementAtIndex(index).objectReferenceValue, Is.Not.Null,
                    owner.name + "." + name + "[" + index + "]");
            }
        }
    }
}
