using System.Linq;
using LastExit.ArtLab.Preview;
using LastExit.Editor.ArtLab;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LastExit.Tests.ArtLab
{
    public class ArtLabUxTests
    {
        private GameObject _testRoot;

        [TearDown]
        public void TearDown()
        {
            if (_testRoot != null)
            {
                Object.DestroyImmediate(_testRoot);
            }
        }

        [Test]
        public void FirstBodyBlocksMatchingBusFurtherDownUntilPhysicallyMovedAway()
        {
            _testRoot = new GameObject("UX Geometry Test");
            PreviewBus upper = CreateBus("R", new Vector3(0, 1, 2));
            PreviewBus lower = CreateBus("T", new Vector3(0, 1, -2));
            PreviewBus[] buses = { lower, upper };
            Assert.That(PreviewBoardGeometry.GetFirstBody(buses, 0), Is.SameAs(upper));
            Assert.That(PreviewBoardGeometry.IsDoorAvailable(upper, 0, "T"), Is.False);
            upper.transform.position += Vector3.right * 5;
            Assert.That(PreviewBoardGeometry.GetFirstBody(buses, 0), Is.SameAs(lower));
            Assert.That(PreviewBoardGeometry.IsDoorAvailable(lower, 0, "T"), Is.True);
        }

        [Test]
        public void DoorAlignmentAndRouteAreBothRequiredAndBadgeCannotChangeBounds()
        {
            _testRoot = new GameObject("UX Door Test");
            PreviewBus bus = CreateBus("G", new Vector3(0, 1, 2));
            Bounds before = bus.GetPhysicalBounds();
            GameObject decoration = GameObject.CreatePrimitive(PrimitiveType.Cube);
            decoration.name = "Oversized badge, not physical body";
            decoration.transform.SetParent(bus.transform, false);
            decoration.transform.localScale = Vector3.one * 30;
            Assert.That(bus.GetPhysicalBounds(), Is.EqualTo(before));
            Assert.That(PreviewBoardGeometry.IsDoorAvailable(bus, 0, "G"), Is.True);
            Assert.That(PreviewBoardGeometry.IsDoorAvailable(bus, .3f, "G"), Is.False);
            Assert.That(PreviewBoardGeometry.IsDoorAvailable(bus, 0, "R"), Is.False);
            Assert.That(PreviewBoardGeometry.GetFirstBody(new[] { bus }, 4), Is.Null);
        }

        [Test]
        public void ResetRestoresAllInitialPositionsAndClearsSelection()
        {
            _testRoot = new GameObject("UX Reset Test");
            PreviewBus first = CreateBus("R", new Vector3(-1, 1, 2));
            PreviewBus second = CreateBus("T", new Vector3(1, 1, -2));
            Camera camera = _testRoot.AddComponent<Camera>();
            PreviewPointerInput input = _testRoot.AddComponent<PreviewPointerInput>();
            input.Initialize(camera, new RectTransform[0]);
            PreviewBusDrag drag = _testRoot.AddComponent<PreviewBusDrag>();
            drag.Initialize(camera, input, new[] { first, second });
            first.transform.position += Vector3.right * 4;
            second.transform.position += Vector3.left * 2;
            drag.ResetLayout();
            Assert.That(first.transform.position, Is.EqualTo(first.InitialPosition));
            Assert.That(second.transform.position, Is.EqualTo(second.InitialPosition));
            Assert.That(drag.Selected, Is.Null);
        }

        [Test]
        public void PhysicalBodyMembershipSurvivesCallerArrayReuseAndStillTracksItsRenderer()
        {
            _testRoot = new GameObject("UX Body Membership Test");
            PreviewBus bus = CreateBus("R", Vector3.zero);
            Renderer body = bus.GetComponentInChildren<Renderer>();
            Transform door = bus.transform.Find("TEST_DOOR_ANCHOR");
            Renderer[] renderers = { body };
            bus.Initialize(renderers, door, "R", 4);
            Bounds original = bus.GetPhysicalBounds();
            renderers[0] = null;
            body.transform.position += Vector3.right * 2;

            Assert.That(bus.GetPhysicalBounds().center, Is.EqualTo(original.center + Vector3.right * 2));
            Assert.That(bus.GetPhysicalBounds().size, Is.EqualTo(original.size));
        }

        [Test]
        public void CaptureScaleUsesCurrentCameraTargetImmediately()
        {
            _testRoot = new GameObject("UX Canvas Scale Test", typeof(RectTransform));
            GameObject cameraObject = new GameObject("Capture Camera", typeof(Camera));
            cameraObject.transform.SetParent(_testRoot.transform);
            Camera camera = cameraObject.GetComponent<Camera>();
            Canvas canvas = _testRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            PreviewCanvasScaler scaler = _testRoot.AddComponent<PreviewCanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1080);
            RenderTexture target = new RenderTexture(360, 640, 0);

            try
            {
                camera.targetTexture = target;
                camera.aspect = 360f / 640;
                scaler.RefreshScale();
                Assert.That(canvas.scaleFactor, Is.EqualTo(.5f).Within(.001f));
            }
            finally
            {
                camera.targetTexture = null;
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void SavedUxSceneHasFiveMixedRowsAndSerializedPreviewBindings()
        {
            Scene scene = EditorSceneManager.OpenScene(ArtLabUxScene.ScenePath, OpenSceneMode.Single);
            Transform look = scene.GetRootGameObjects().Single(root => root.activeSelf &&
                root.name.StartsWith(ArtLabSceneBuilder.LookPrefix)).transform;
            PreviewBus[] buses = look.GetComponentsInChildren<PreviewBus>();
            Assert.That(buses.Length, Is.EqualTo(5));
            CollectionAssert.AreEquivalent(new[] { 2, 4, 6 }, buses.Select(bus => bus.Capacity).Distinct());
            Assert.That(buses.Select(bus => bus.transform.position.z).Distinct().Count(), Is.EqualTo(5));
            ArtLabPreviewHud hud = look.GetComponentInChildren<ArtLabPreviewHud>();
            SerializedObject bindings = new SerializedObject(hud.GetComponent<PreviewComposition>());
            Assert.That(bindings.FindProperty("_camera").objectReferenceValue, Is.Not.Null);
            Assert.That(bindings.FindProperty("_drag").objectReferenceValue, Is.Not.Null);
            Assert.That(bindings.FindProperty("_buses").arraySize, Is.EqualTo(5));
            Assert.That(bindings.FindProperty("_badges").arraySize, Is.EqualTo(5));
            Assert.That(bindings.FindProperty("_lanes").arraySize, Is.EqualTo(4));
            Assert.That(bindings.FindProperty("_uiRegions").arraySize, Is.EqualTo(2));

            foreach (string field in new[] { "_input", "_hud", "_layout", "_framing" })
            {
                Assert.That(bindings.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            }

            foreach (Transform child in look.GetComponentsInChildren<Transform>(true))
            {
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject),
                    Is.Zero, child.name);
            }

            Assert.That(look.GetComponentsInChildren<PreviewComposition>().Length, Is.EqualTo(1));
            Assert.That(look.GetComponentsInChildren<PreviewPointerInput>().Length, Is.EqualTo(1));
            Assert.That(look.GetComponentsInChildren<PreviewBusDrag>().Length, Is.EqualTo(1));
            Assert.That(hud.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera));
            Assert.That(hud.GetComponentsInChildren<Button>().Single().onClick.GetPersistentEventCount(),
                Is.EqualTo(1));

            RouteEmblemGraphic[] emblems = hud.GetComponentsInChildren<RouteEmblemGraphic>(true);
            Assert.That(emblems.Length, Is.GreaterThanOrEqualTo(9));

            foreach (RouteEmblemGraphic emblem in emblems)
            {
                Assert.That(emblem.GetComponent<CanvasRenderer>(), Is.Not.Null, emblem.name);
            }
        }

        private PreviewBus CreateBus(string route, Vector3 position)
        {
            GameObject root = new GameObject("Test bus " + route);
            root.transform.SetParent(_testRoot.transform);
            root.transform.position = position;
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "LE_BUS_TEST_BODY";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(4, 2, 2);
            GameObject door = new GameObject("TEST_DOOR_ANCHOR");
            door.transform.SetParent(root.transform, false);
            door.transform.localPosition = new Vector3(0, 0, 1);
            PreviewBus bus = root.AddComponent<PreviewBus>();
            bus.Initialize(new[] { body.GetComponent<Renderer>() }, door.transform, route, 4);
            return bus;
        }
    }
}
