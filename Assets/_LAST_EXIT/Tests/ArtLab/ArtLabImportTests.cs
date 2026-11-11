using System.Linq;
using LastExit.Editor.ArtLab;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastExit.Tests.ArtLab
{
    public class ArtLabImportTests
    {
        [Test]
        public void BusAssembliesShareTheSameFourSourceMeshes()
        {
            GameObject library = AssetDatabase.LoadAssetAtPath<GameObject>(ArtLabImporter.Root +
                "/Models/BUS_BASE_LIBRARY_v02.fbx");
            MeshFilter[] parts = library.GetComponentsInChildren<MeshFilter>(true);
            Assert.That(parts.Length, Is.EqualTo(12));
            Assert.That(parts.Select(part => part.sharedMesh).Distinct().Count(), Is.EqualTo(4));
            string[] names = { "LE_BUS_MINI", "LE_BUS_STANDARD", "LE_BUS_LONG" };
            float[] lengths = { 4, 6, 8 };

            for (int index = 0; index < names.Length; index++)
            {
                Transform bus = library.transform.Find(names[index]);
                Bounds bounds = ArtLabImporter.GetBounds(bus.gameObject);
                Assert.That(bounds.size.x, Is.EqualTo(lengths[index]).Within(.005f));
                Assert.That(bounds.size.y, Is.GreaterThan(2));
                Transform anchor = bus.GetComponentsInChildren<Transform>(true).Single(child =>
                    child.name.EndsWith("DOOR_ANCHOR"));
                Assert.That(Mathf.Abs(anchor.position.z - bus.position.z), Is.GreaterThan(.8f));
            }
        }

        [Test]
        public void PassengerPreservesOneSkinnedMeshAndThreeClips()
        {
            GameObject passenger = AssetDatabase.LoadAssetAtPath<GameObject>(ArtLabImporter.Root +
                "/Models/PASSENGER_BASE_v01.fbx");
            SkinnedMeshRenderer[] skins = passenger.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.That(skins.Length, Is.EqualTo(1));
            Assert.That(skins[0].bones.Length, Is.GreaterThanOrEqualTo(16));
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(ArtLabImporter.Root +
                "/Models/PASSENGER_BASE_v01.fbx").OfType<AnimationClip>().Where(clip =>
                    clip.name.StartsWith("__preview__") == false).ToArray();
            Assert.That(clips.Length, Is.EqualTo(3));
            Assert.That(clips.All(clip => clip.length > 0), Is.True);
        }

        [Test]
        public void RouteColorsAndAtlasOffsetsMatchTheContractInEveryLook()
        {
            foreach (ArtLabLook look in System.Enum.GetValues(typeof(ArtLabLook)))
            {
                for (int index = 0; index < ArtLabMaterials.Routes.Count; index++)
                {
                    string folder = ArtLabImporter.MaterialFolder + "/" + look + "/";
                    Material route = AssetDatabase.LoadAssetAtPath<Material>(folder + "Route_" +
                        ArtLabMaterials.Routes[index] + ".mat");
                    Assert.That(ColorUtility.ToHtmlStringRGB(route.GetColor("_BaseColor")),
                        Is.EqualTo(ArtLabMaterials.RouteHex[index].Substring(1)));
                    Material emblem = AssetDatabase.LoadAssetAtPath<Material>(folder + "Emblem_" +
                        ArtLabMaterials.Routes[index] + ".mat");
                    Assert.That(emblem.GetTextureOffset("_BaseMap"),
                        Is.EqualTo(new Vector2(index % 2 * .5f, index / 2 * -.5f)));
                }
            }
        }

        [Test]
        public void BusesRemainInSeparateWorldXRows()
        {
            Scene scene = EditorSceneManager.OpenScene(ArtLabSceneBuilder.ScenePath, OpenSceneMode.Single);
            Transform look = scene.GetRootGameObjects().Single(root => root.activeSelf &&
                root.name.StartsWith(ArtLabSceneBuilder.LookPrefix)).transform;
            Transform[] buses = look.Cast<Transform>().Where(child => child.name.StartsWith("LE_BUS_")).ToArray();
            Assert.That(buses.Length, Is.EqualTo(3));
            Assert.That(buses.Select(bus => bus.position.z).Distinct().Count(), Is.EqualTo(3));

            foreach (Transform bus in buses)
            {
                Assert.That(Mathf.Abs(Vector3.Dot(bus.right, Vector3.right)), Is.EqualTo(1).Within(.0001f));
                Bounds bounds = ArtLabImporter.GetBounds(bus.gameObject);
                Assert.That(bounds.size.x, Is.GreaterThan(bounds.size.z));
            }
        }

        [Test]
        public void SavedArtLabHasNoMissingScriptsAndStaysOutsideTheBuild()
        {
            Scene scene = EditorSceneManager.OpenScene(ArtLabSceneBuilder.ScenePath, OpenSceneMode.Single);
            ArtLabSceneReport report = ArtLabPreview.WriteSceneReport();
            Assert.That(report.Passed, Is.True);
            Assert.That(report.BuildScenes.Any(path => path == ArtLabSceneBuilder.ScenePath), Is.False);
            Assert.That(scene.GetRootGameObjects().Count(root => root.activeSelf &&
                root.name.StartsWith(ArtLabSceneBuilder.LookPrefix)), Is.EqualTo(1));
            Assert.That(scene.GetRootGameObjects().SelectMany(root =>
                root.GetComponentsInChildren<Camera>(true)).Count(), Is.EqualTo(1));
            Assert.That(scene.GetRootGameObjects().SelectMany(root =>
                root.GetComponentsInChildren<AudioListener>(true)).Count(), Is.EqualTo(1));
        }
    }
}
