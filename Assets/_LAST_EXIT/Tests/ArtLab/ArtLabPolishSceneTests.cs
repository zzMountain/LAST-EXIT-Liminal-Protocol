using System.Linq;
using LastExit.ArtLab.Preview;
using LastExit.Editor.ArtLab;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastExit.Tests.ArtLab
{
    public class ArtLabPolishSceneTests
    {
        [Test]
        public void SceneRebuilderCannotOverwriteTheExistingEditableScene()
        {
            Assert.Throws<System.InvalidOperationException>(() => ArtLabUxScene.Build());
        }

        [Test]
        public void FramingChangesWithAspectWithoutMovingBoardObjects()
        {
            Scene scene = EditorSceneManager.OpenScene(ArtLabUxScene.ScenePath, OpenSceneMode.Single);
            PreviewBus[] buses = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<PreviewBus>()).ToArray();
            Vector3[] positions = buses.Select(bus => bus.transform.position).ToArray();
            Camera camera = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Camera>()).Single();
            PreviewCameraFraming framing = camera.GetComponent<PreviewCameraFraming>();
            PreviewHudLayout layout = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<PreviewHudLayout>()).Single();
            Assert.That(layout.PortraitLayout, Is.Not.Null);
            Assert.That(layout.LandscapeLayout, Is.Not.Null);
            float initialAspect = camera.aspect;

            try
            {
                camera.aspect = 16f / 9;
                framing.RefreshFraming();
                Assert.That(camera.orthographicSize, Is.EqualTo(ArtLabUxScene.LandscapeSize));
                camera.aspect = 9f / 16;
                framing.RefreshFraming();
                Assert.That(camera.orthographicSize, Is.EqualTo(ArtLabUxScene.PortraitSize));
                CollectionAssert.AreEqual(positions, buses.Select(bus => bus.transform.position).ToArray());
            }
            finally
            {
                camera.aspect = initialAspect;
                framing.RefreshFraming();
            }
        }

        [Test]
        public void EveryBusHasOneLeftEntryAndDressingDoesNotEnlargeItsGameplayBounds()
        {
            Scene scene = EditorSceneManager.OpenScene(ArtLabUxScene.ScenePath, OpenSceneMode.Single);
            PreviewBus[] buses = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<PreviewBus>()).ToArray();
            Assert.That(buses.Length, Is.EqualTo(5));

            foreach (PreviewBus bus in buses)
            {
                Transform detail = bus.transform.Find("Neutral Vehicle Detail");
                Assert.That(detail, Is.Not.Null);
                Assert.That(detail.GetComponentsInChildren<Collider>(), Is.Empty);
                Transform step = detail.Find("Entry Step");
                Assert.That(step.position.x, Is.EqualTo(bus.DoorPosition.x).Within(.001f));
                Assert.That(step.position.z, Is.GreaterThan(bus.GetPhysicalBounds().max.z));
                Bounds physical = bus.GetPhysicalBounds();
                Transform canopy = detail.Find("Entry Canopy");
                Assert.That(canopy.position.y, Is.GreaterThan(physical.max.y));
                Renderer front = bus.GetComponentsInChildren<Renderer>()
                    .Single(renderer => renderer.name.EndsWith("_front"));
                float frontSide = Mathf.Sign(front.bounds.center.x - physical.center.x);
                Assert.That(Mathf.Sign(detail.Find("Cabin Brow").position.x - physical.center.x),
                    Is.EqualTo(frontSide));
                Assert.That(Mathf.Sign(detail.Find("Rear Vent Housing").position.x - physical.center.x),
                    Is.EqualTo(-frontSide));
                // Dressing travels with its bus, but selecting a vehicle still uses only modular body renderers.
                Assert.That(detail.GetComponentsInChildren<Renderer>().All(renderer =>
                    renderer.name.StartsWith("LE_BUS_") == false), Is.True);
            }
        }
    }
}
