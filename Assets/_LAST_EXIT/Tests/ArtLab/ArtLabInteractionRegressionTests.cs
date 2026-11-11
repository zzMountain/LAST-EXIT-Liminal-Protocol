using System.Collections.Generic;
using System.Reflection;
using LastExit.ArtLab.Preview;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace LastExit.Tests.ArtLab
{
    public class ArtLabInteractionRegressionTests : InputTestFixture
    {
        private GameObject _root;
        private Camera _camera;
        private RenderTexture _target;
        private PreviewPointerInput _input;
        private PreviewBusDrag _drag;
        private MotionFixture _bus;
        private Mouse _mouse;
        private Touchscreen _touch;
        private Vector2 _uiPoint;
        private int _sampleCount;
        private int _pressCount;

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            _sampleCount = 0;
            _pressCount = 0;
            _mouse = InputSystem.AddDevice<Mouse>("ArtLabRegressionMouse");
            _touch = InputSystem.AddDevice<Touchscreen>("ArtLabRegressionTouch");
            _root = new GameObject("Interaction Regression Fixture");
            GameObject cameraObject = new GameObject("Fixture Camera", typeof(Camera));
            cameraObject.transform.SetParent(_root.transform);
            _camera = cameraObject.GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 10;
            _camera.transform.position = new Vector3(0, 12, -12);
            _camera.transform.LookAt(Vector3.up);
            _target = new RenderTexture(800, 600, 0);
            _camera.targetTexture = _target;
            _camera.aspect = 800f / 600;
            RectTransform uiRegion = CreateUiRegion();
            _input = _root.AddComponent<PreviewPointerInput>();
            _input.Initialize(_camera, new[] { uiRegion });
            _input.Pressed += OnPressed;
            _bus = new MotionFixture();
            _drag = _root.AddComponent<PreviewBusDrag>();
            _drag.Initialize(_camera, _input, new IPreviewBusMotion[] { _bus });
            InvokeLifecycle(_drag, "OnEnable");
            Assert.That(RectTransformUtility.RectangleContainsScreenPoint(uiRegion, Project(0), _camera), Is.False,
                "The initial bus press must be outside the fixture UI blocker.");
        }

        [TearDown]
        public override void TearDown()
        {
            try
            {
                if (_root != null)
                {
                    if (_drag != null)
                    {
                        InvokeLifecycle(_drag, "OnDisable");
                    }

                    if (_input != null)
                    {
                        InvokeLifecycle(_input, "OnDisable");
                    }

                    if (_camera != null)
                    {
                        _camera.targetTexture = null;
                    }

                    Object.DestroyImmediate(_root);
                }

                if (_target != null)
                {
                    Object.DestroyImmediate(_target);
                }
            }
            finally
            {
                base.TearDown();
            }
        }

        [Test]
        public void MouseReleaseUsesItsFinalPositionAcrossTheSnapBoundary()
        {
            MouseAt(0, true);
            MouseAt(.8f, true);
            Assert.That(_bus.Position.x, Is.EqualTo(.8f).Within(.001f));
            MouseAt(1.2f, false);
            Assert.That(_bus.Position.x, Is.EqualTo(2).Within(.001f));
        }

        [Test]
        public void TouchEndUsesItsFinalPositionBeforeOtherFingersEnd()
        {
            TouchAt(1, 0, InputTouchPhase.Began);
            TouchAt(1, .8f, InputTouchPhase.Moved);
            TouchAt(2, 5, InputTouchPhase.Began);
            Assert.That(_bus.Position.x, Is.EqualTo(.8f).Within(.001f));
            TouchAt(1, 1.2f, InputTouchPhase.Ended);
            Assert.That(_bus.Position.x, Is.EqualTo(2).Within(.001f));
            TouchAt(2, 6, InputTouchPhase.Moved);
            Assert.That(_bus.Position.x, Is.EqualTo(2).Within(.001f), "The second finger cannot inherit a drag.");
        }

        [Test]
        public void TouchOnUiCannotHijackAnActiveMouseGesture()
        {
            MouseAt(0, true);
            MouseAt(.8f, true);
            QueueTouch(1, _uiPoint, InputTouchPhase.Began);
            Sample();
            Assert.That(_bus.Position.x, Is.EqualTo(.8f).Within(.001f));
            QueueTouch(1, _uiPoint + Vector2.right * 30, InputTouchPhase.Moved);
            Sample();
            Assert.That(_bus.Position.x, Is.EqualTo(.8f).Within(.001f));
            MouseAt(1.2f, false);
            Assert.That(_bus.Position.x, Is.EqualTo(2).Within(.001f));
        }

        [Test]
        public void MouseCannotHijackAnActiveTouchGesture()
        {
            TouchAt(1, 0, InputTouchPhase.Began);
            TouchAt(1, .8f, InputTouchPhase.Moved);
            MouseAt(5, true);
            Assert.That(_bus.Position.x, Is.EqualTo(.8f).Within(.001f));
            MouseAt(6, true);
            Assert.That(_bus.Position.x, Is.EqualTo(.8f).Within(.001f));
            TouchAt(1, 1.2f, InputTouchPhase.Ended);
            Assert.That(_bus.Position.x, Is.EqualTo(2).Within(.001f));
        }

        [Test]
        public void TouchCancellationSnapsTheLastAcceptedPosition()
        {
            TouchAt(1, 0, InputTouchPhase.Began);
            TouchAt(1, 1.2f, InputTouchPhase.Moved);
            TouchAt(1, 7, InputTouchPhase.Canceled);
            Assert.That(_bus.Position.x, Is.EqualTo(2).Within(.001f));
        }

        [Test]
        public void FocusLossCancelsAndRequiresANewPress()
        {
            MouseAt(0, true);
            MouseAt(1.2f, true);
            InvokeLifecycle(_input, "OnApplicationFocus", false);
            Assert.That(_bus.Position.x, Is.EqualTo(2).Within(.001f));
            MouseAt(5, true);
            Assert.That(_bus.Position.x, Is.EqualTo(2).Within(.001f));
        }

        [Test]
        public void DeviceRemovalCancelsInsteadOfLeavingAnOffGridBus()
        {
            MouseAt(0, true);
            MouseAt(1.2f, true);
            InputSystem.RemoveDevice(_mouse);
            InvokeLifecycle(_input, "Update");
            Assert.That(_bus.Position.x, Is.EqualTo(2).Within(.001f));
        }

        [Test]
        public void DragDisableSnapsBeforeUnsubscribingAndReenableRequiresANewPress()
        {
            MouseAt(0, true);
            MouseAt(1.2f, true);
            InvokeLifecycle(_drag, "OnDisable");
            Assert.That(_bus.Position.x, Is.EqualTo(2).Within(.001f));
            MouseAt(5, true);
            InvokeLifecycle(_drag, "OnEnable");
            MouseAt(6, true);
            MouseAt(6, false);
            Assert.That(_bus.Position.x, Is.EqualTo(2).Within(.001f));
            MouseAt(2, true);
            MouseAt(3.2f, false);
            Assert.That(_bus.Position.x, Is.EqualTo(4).Within(.001f));
        }

        [Test]
        public void ReinitializationAndRepeatedEnableDoNotDuplicateSubscriptionsOrAliasMembership()
        {
            IPreviewBusMotion[] membership = { _bus };
            _drag.Initialize(_camera, _input, membership);
            _drag.Initialize(_camera, _input, membership);
            InvokeLifecycle(_drag, "OnEnable");
            InvokeLifecycle(_drag, "OnEnable");
            membership[0] = new MotionFixture();
            MouseAt(0, true);
            _bus.ResetWrites();
            MouseAt(.8f, true);
            Assert.That(_bus.PositionWrites, Is.EqualTo(1));
            Assert.That(_drag.Selected, Is.SameAs(_bus));
            InvokeLifecycle(_drag, "OnDisable");
            _bus.ResetWrites();
            MouseAt(5, true);
            Assert.That(_bus.PositionWrites, Is.Zero);
        }

        [TestCase(-1)]
        [TestCase(1)]
        public void DragAndSnapKeepTheWholeBodyInsideTheFloor(int direction)
        {
            MouseAt(0, true);
            MouseAt(direction * 100, true);
            Assert.That(_bus.GetPhysicalBounds().min.x, Is.GreaterThanOrEqualTo(-9));
            Assert.That(_bus.GetPhysicalBounds().max.x, Is.LessThanOrEqualTo(9));
            MouseAt(direction * 100, false);
            Assert.That(_bus.Position.x, Is.EqualTo(direction * 6).Within(.001f));
            Assert.That(_bus.Position.y, Is.Zero);
            Assert.That(_bus.Position.z, Is.Zero);
        }

        [Test]
        public void SelectionEventsRepresentActualChangesOnly()
        {
            List<int> selections = new List<int>();
            _drag.SelectionChanged += selections.Add;
            MouseAt(0, true);
            MouseAt(0, false);
            MouseAt(0, true);
            MouseAt(0, false);
            _drag.ResetLayout();
            _drag.ResetLayout();
            CollectionAssert.AreEqual(new[] { 0, -1 }, selections);
        }

        private static void InvokeLifecycle(MonoBehaviour component, string method, params object[] arguments)
        {
            // SendMessage rejects non-ExecuteAlways behaviours in EditMode. Real queued device state still
            // drives the production sampling code; the finite Play probe verifies actual engine callbacks.
            MethodInfo callback = component.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(callback, Is.Not.Null, method);
            callback.Invoke(component, arguments);
        }

        private RectTransform CreateUiRegion()
        {
            GameObject canvasObject = new GameObject("Fixture UI", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(_root.transform);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _camera;
            canvas.planeDistance = 1;
            GameObject regionObject = new GameObject("Pointer Blocking Region", typeof(RectTransform));
            RectTransform region = (RectTransform)regionObject.transform;
            region.SetParent(canvasObject.transform, false);
            region.anchorMin = new Vector2(.5f, .5f);
            region.anchorMax = region.anchorMin;
            region.sizeDelta = new Vector2(100, 100);
            region.anchoredPosition = new Vector2(220, 0);
            Canvas.ForceUpdateCanvases();
            _uiPoint = RectTransformUtility.WorldToScreenPoint(_camera, region.TransformPoint(region.rect.center));
            Assert.That(RectTransformUtility.RectangleContainsScreenPoint(region, _uiPoint, _camera), Is.True);
            return region;
        }

        private void MouseAt(float worldX, bool pressed)
        {
            MouseState state = new MouseState();
            state.position = Project(worldX);
            state = state.WithButton(MouseButton.Left, pressed);
            InputSystem.QueueStateEvent(_mouse, state);
            Sample();
        }

        private void TouchAt(int id, float worldX, InputTouchPhase phase)
        {
            QueueTouch(id, Project(worldX), phase);
            Sample();
        }

        private void QueueTouch(int id, Vector2 position, InputTouchPhase phase)
        {
            TouchState state = new TouchState();
            state.touchId = id;
            state.position = position;
            state.phase = phase;
            InputSystem.QueueStateEvent(_touch, state);
        }

        private void Sample()
        {
            InputSystem.Update();
            _mouse.MakeCurrent();
            _touch.MakeCurrent();

            if (_sampleCount == 0)
            {
                bool freshPress = _mouse.leftButton.wasPressedThisFrame;

                foreach (UnityEngine.InputSystem.Controls.TouchControl touch in _touch.touches)
                {
                    freshPress |= touch.press.wasPressedThisFrame;
                }

                Assert.That(freshPress, Is.True,
                    "Queued device state must expose its first press in the EditMode input update.");
            }

            InvokeLifecycle(_input, "Update");

            if (_sampleCount == 0)
            {
                Assert.That(_pressCount, Is.EqualTo(1),
                    "The production input reader must publish the first fixture press.");
                Assert.That(_drag.SelectedIndex, Is.Zero,
                    "The enabled drag subscriber must select the fixture bus.");
            }

            _sampleCount++;
        }

        private Vector2 Project(float worldX)
        {
            return _camera.WorldToScreenPoint(new Vector3(worldX, 1, 0));
        }

        private void OnPressed(Vector2 position)
        {
            _pressCount++;
        }

        private class MotionFixture : IPreviewBusMotion
        {
            private Vector3 _position;

            public Vector3 Position
            {
                get => _position;
                set
                {
                    _position = value;
                    PositionWrites++;
                }
            }

            public Vector3 InitialPosition => Vector3.zero;
            public int PositionWrites { get; private set; }

            public Bounds GetPhysicalBounds()
            {
                return new Bounds(_position + Vector3.up, new Vector3(4, 2, 2));
            }

            public void ResetWrites()
            {
                PositionWrites = 0;
            }
        }
    }
}
