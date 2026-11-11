using System.Linq;
using LastExit.Editor.ArtLab;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastExit.Tests.ArtLab
{
    public class ArtLabReadabilityTests
    {
        [Test]
        public void DoorsFaceQueuesAndTheirMarkersFollowRealAnchors()
        {
            Transform look = OpenLook();
            Transform[] buses = GetBuses(look);

            for (int row = 0; row < buses.Length; row++)
            {
                Transform bus = buses[row];
                Transform door = bus.GetComponentsInChildren<Transform>(true).Single(child =>
                    child.name.EndsWith("DOOR_ANCHOR"));
                Transform marker = bus.Find("Door Presentation");
                Assert.That(door.position.z - bus.position.z, Is.GreaterThan(.8f));
                Assert.That(door.position.x, Is.EqualTo(ArtLabLayout.LaneX[row]).Within(.005f));
                Assert.That(marker.position.x, Is.EqualTo(door.position.x).Within(.005f));
                Assert.That(marker.position.z, Is.GreaterThan(door.position.z));
                Assert.That(bus.position.z, Is.EqualTo(ArtLabLayout.RowZ[row]).Within(.005f));
            }
        }

        [TestCase(16f / 9, ArtLabLayout.LandscapeSize)]
        [TestCase(9f / 16, ArtLabLayout.PortraitSize)]
        public void QueueHeadsAreUnobstructedAndServicesStayInFrame(float aspect, float size)
        {
            Transform look = OpenLook();
            Camera camera = ArtLabPreview.GetCamera();
            camera.aspect = aspect;
            camera.orthographicSize = size;
            Renderer[] busParts = GetBuses(look).SelectMany(bus => bus.GetComponentsInChildren<Renderer>())
                .Where(part => part.name.StartsWith("LE_BUS_")).ToArray();

            foreach (string route in ArtLabMaterials.Routes)
            {
                Transform head = look.Find("Passenger_" + route + "_0");

                foreach (float height in new[] { .8f, 1.5f })
                {
                    Vector3 point = head.position + Vector3.up * height;
                    AssertInFrame(camera, point);
                    Ray ray = new Ray(camera.transform.position, point - camera.transform.position);
                    float pointDistance = Vector3.Distance(ray.origin, point);

                    foreach (Renderer busPart in busParts)
                    {
                        bool hits = busPart.bounds.IntersectRay(ray, out float distance);
                        Assert.That(hits && distance < pointDistance - .05f, Is.False,
                            route + " is hidden behind " + busPart.name);
                    }
                }
            }

            AssertInFrame(camera, ArtLabLayout.Q3Position + new Vector3(-1.3f, 3.7f, 0));
            AssertInFrame(camera, ArtLabLayout.CctvPosition + new Vector3(1.3f, 2.8f, 0));
        }

        [Test]
        public void ApproachesStopAtFirstPhysicalBodyAndSideAccessIsClear()
        {
            Transform look = OpenLook();
            Transform[] buses = GetBuses(look);

            for (int lane = 0; lane < ArtLabLayout.LaneX.Count; lane++)
            {
                float x = ArtLabLayout.LaneX[lane];
                float firstZ = ArtLabLayout.FloorFrontZ;

                foreach (Transform bus in buses)
                {
                    Renderer[] parts = bus.GetComponentsInChildren<Renderer>().Where(part =>
                        part.name.StartsWith("LE_BUS_")).ToArray();
                    Bounds bounds = parts[0].bounds;

                    foreach (Renderer part in parts)
                    {
                        bounds.Encapsulate(part.bounds);
                    }

                    if (x >= bounds.min.x - .01f && x <= bounds.max.x + .01f)
                    {
                        firstZ = Mathf.Max(firstZ, bounds.max.z);
                    }
                }

                Transform stop = look.GetComponentsInChildren<Transform>().Single(child =>
                    child.name == "First Body Stop " + (char)('A' + lane));
                Assert.That(stop.position.z - firstZ, Is.InRange(.1f, .25f));
            }

            Renderer[] barriers = look.GetComponentsInChildren<Renderer>().Where(renderer =>
                renderer.name.Contains("Wall") || renderer.name.Contains("Kerb") ||
                renderer.name.Contains("Steel Kerb Cap")).ToArray();

            foreach (float rowZ in ArtLabLayout.RowZ)
            {
                foreach (Renderer barrier in barriers)
                {
                    Bounds bounds = barrier.bounds;
                    bool crossesRow = bounds.min.z < rowZ + 1.2f && bounds.max.z > rowZ - 1.2f;
                    bool atEdge = Mathf.Abs(bounds.center.x) > 8;
                    Assert.That(crossesRow && atEdge, Is.False, barrier.name + " blocks a row opening");
                }
            }
        }

        private static Transform OpenLook()
        {
            Scene scene = EditorSceneManager.OpenScene(ArtLabSceneBuilder.ScenePath, OpenSceneMode.Single);
            return scene.GetRootGameObjects().Single(root => root.activeSelf &&
                root.name.StartsWith(ArtLabSceneBuilder.LookPrefix)).transform;
        }

        private static Transform[] GetBuses(Transform look)
        {
            return look.Cast<Transform>().Where(child => child.name.StartsWith("LE_BUS_"))
                .OrderByDescending(child => child.position.z).ToArray();
        }

        private static void AssertInFrame(Camera camera, Vector3 point)
        {
            Vector3 viewport = camera.WorldToViewportPoint(point);
            Assert.That(viewport.z, Is.GreaterThan(0));
            Assert.That(viewport.x, Is.InRange(.02f, .98f));
            Assert.That(viewport.y, Is.InRange(.02f, .98f));
        }
    }
}
