using System;
using LastExit.ArtLab.Preview;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LastExit.Tests.ArtLab
{
    public class ArtLabPreviewUiTests
    {
        private GameObject _root;
        private Camera _camera;
        private PreviewHudLayoutPreset _preset;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Preview UI Regression", typeof(RectTransform));
            GameObject cameraObject = new GameObject("Preview UI Camera", typeof(Camera));
            cameraObject.transform.SetParent(_root.transform, false);
            _camera = cameraObject.GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.transform.position = new Vector3(0, 10, -10);
            _camera.transform.LookAt(Vector3.zero);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);

            if (_preset != null)
            {
                Object.DestroyImmediate(_preset);
            }
        }

        [Test]
        public void BindingSynchronizesSelectionBeforeAnyChangeEvent()
        {
            ArtLabPreviewHud hud = CreateHud();
            SelectionSource selection = new SelectionSource(1);
            BadgeRecorder first = new BadgeRecorder();
            BadgeRecorder second = new BadgeRecorder();
            LaneRecorder lane = new LaneRecorder();
            hud.Bind(selection, new LayoutRecorder(), new FramingRecorder(),
                new IPreviewBusBadge[] { first, second }, new IPreviewLaneGuide[] { lane });

            Assert.That(first.IsSelected, Is.False);
            Assert.That(second.IsSelected, Is.True);
            Assert.That(selection.SubscriberCount, Is.EqualTo(1));
            hud.RefreshLayout();
            Assert.That(lane.SelectedIndex, Is.EqualTo(1));
        }

        [Test]
        public void RebindingDetachesOldSelectionAndDoesNotDuplicateNewSubscription()
        {
            ArtLabPreviewHud hud = CreateHud();
            SelectionSource previous = new SelectionSource(0);
            SelectionSource current = new SelectionSource(-1);
            BadgeRecorder badge = new BadgeRecorder();
            IPreviewBusBadge[] badges = { badge };
            IPreviewLaneGuide[] lanes = Array.Empty<IPreviewLaneGuide>();
            LayoutRecorder layout = new LayoutRecorder();
            FramingRecorder framing = new FramingRecorder();
            hud.Bind(previous, layout, framing, badges, lanes);
            hud.Bind(current, layout, framing, badges, lanes);
            hud.Bind(current, layout, framing, badges, lanes);
            int callsBeforeEvent = badge.SelectionCalls;

            Assert.That(previous.SubscriberCount, Is.Zero);
            Assert.That(current.SubscriberCount, Is.EqualTo(1));
            previous.Select(-1);
            Assert.That(badge.SelectionCalls, Is.EqualTo(callsBeforeEvent));
            current.Select(0);
            Assert.That(badge.SelectionCalls, Is.EqualTo(callsBeforeEvent + 1));
            Assert.That(badge.IsSelected, Is.True);
        }

        [Test]
        public void DisabledHudStopsListeningAndResynchronizesOnceWhenEnabled()
        {
            ArtLabPreviewHud hud = CreateHud();
            SelectionSource selection = new SelectionSource(0);
            BadgeRecorder badge = new BadgeRecorder();
            hud.Bind(selection, new LayoutRecorder(), new FramingRecorder(),
                new IPreviewBusBadge[] { badge }, Array.Empty<IPreviewLaneGuide>());
            hud.enabled = false;
            int callsWhileDisabled = badge.SelectionCalls;

            Assert.That(selection.SubscriberCount, Is.Zero);
            selection.Select(-1);
            Assert.That(badge.SelectionCalls, Is.EqualTo(callsWhileDisabled));
            hud.enabled = true;
            Assert.That(selection.SubscriberCount, Is.EqualTo(1));
            Assert.That(badge.IsSelected, Is.False);
            Assert.That(badge.SelectionCalls, Is.EqualTo(callsWhileDisabled + 1));
            selection.Select(0);
            Assert.That(badge.SelectionCalls, Is.EqualTo(callsWhileDisabled + 2));
        }

        [Test]
        public void BoundHudKeepsItsMembershipWhenTheCallerReusesBindingArrays()
        {
            ArtLabPreviewHud hud = CreateHud();
            SelectionSource selection = new SelectionSource(-1);
            BadgeRecorder badge = new BadgeRecorder();
            LaneRecorder lane = new LaneRecorder();
            IPreviewBusBadge[] badges = { badge };
            IPreviewLaneGuide[] lanes = { lane };
            hud.Bind(selection, new LayoutRecorder(), new FramingRecorder(), badges, lanes);
            badges[0] = null;
            lanes[0] = null;

            selection.Select(0);
            hud.RefreshLayout();

            Assert.That(badge.IsSelected, Is.True);
            Assert.That(lane.SelectedIndex, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void InspectorColorChangeRefreshesWithoutAnotherSelectionEvent(bool isSelected)
        {
            RectTransform canvas = (RectTransform)_root.transform;
            canvas.sizeDelta = new Vector2(720, 1280);
            Image background = CreateRect("Badge", canvas).gameObject.AddComponent<Image>();
            PreviewBusBadge badge = background.gameObject.AddComponent<PreviewBusBadge>();
            RectTransform door = CreateRect("Door Hint", canvas);
            GameObject arrows = CreateRect("Selection Arrows", background.rectTransform).gameObject;
            RectTransform row = CreateRect("Row Label", canvas);
            badge.Initialize(background, door, arrows, row);
            badge.Bind(new BusShape());
            badge.SetSelected(isSelected);
            Color editedColor = new Color(.8f, .2f, .6f, .7f);
            SerializedObject inspector = new SerializedObject(badge);
            inspector.FindProperty(isSelected ? "_selectedColor" : "_normalColor").colorValue = editedColor;
            inspector.ApplyModifiedPropertiesWithoutUndo();
            badge.Refresh(_camera, canvas);

            Assert.That(background.color, Is.EqualTo(editedColor));
            Assert.That(door.gameObject.activeSelf, Is.EqualTo(isSelected));
            Assert.That(arrows.activeSelf, Is.EqualTo(isSelected));
        }

        [Test]
        public void RebindingLayoutAppliesCachedPresetToNewRectanglesAndText()
        {
            RectTransform canvas = (RectTransform)_root.transform;
            RectTransform header = CreateRect("Original Header", canvas);
            RectTransform footer = CreateRect("Original Footer", canvas);
            RectTransform reset = CreateRect("Original Reset", header);
            Text title = CreateText("Original Title", header);
            title.text = "Authored title";
            header.anchoredPosition = new Vector2(23, -41);
            _preset = ScriptableObject.CreateInstance<PreviewHudLayoutPreset>();
            _preset.Capture(header, footer, reset, new[] { title }, Array.Empty<Text>());
            PreviewHudLayout layout = _root.AddComponent<PreviewHudLayout>();
            layout.Initialize(header, footer, reset, new[] { title }, Array.Empty<Text>());
            layout.SetPresets(_preset, _preset);
            layout.Apply(true, .5f);

            RectTransform replacementHeader = CreateRect("Replacement Header", canvas);
            RectTransform replacementFooter = CreateRect("Replacement Footer", canvas);
            RectTransform replacementReset = CreateRect("Replacement Reset", replacementHeader);
            Text replacementTitle = CreateText("Replacement Title", replacementHeader);
            replacementTitle.text = "Unconfigured title";
            layout.Initialize(replacementHeader, replacementFooter, replacementReset,
                new[] { replacementTitle }, Array.Empty<Text>());
            layout.Apply(true, .5f);

            Assert.That(replacementHeader.anchoredPosition, Is.EqualTo(header.anchoredPosition));
            Assert.That(replacementTitle.text, Is.EqualTo("Authored title"));
        }

        private static RectTransform CreateRect(string name, RectTransform parent)
        {
            GameObject item = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)item.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static Text CreateText(string name, RectTransform parent)
        {
            Text text = CreateRect(name, parent).gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return text;
        }

        private ArtLabPreviewHud CreateHud()
        {
            ArtLabPreviewHud hud = _root.AddComponent<ArtLabPreviewHud>();
            Canvas canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _camera;
            hud.Initialize(_camera);
            return hud;
        }

        private class SelectionSource : IPreviewSelection
        {
            public SelectionSource(int selectedIndex)
            {
                SelectedIndex = selectedIndex;
            }

            public event Action<int> SelectionChanged;

            public int SelectedIndex { get; private set; }
            public int SubscriberCount => SelectionChanged?.GetInvocationList().Length ?? 0;

            public void Select(int index)
            {
                SelectedIndex = index;
                SelectionChanged?.Invoke(index);
            }
        }

        private class BadgeRecorder : IPreviewBusBadge
        {
            public bool IsSelected { get; private set; }
            public int SelectionCalls { get; private set; }

            public void Refresh(Camera camera, RectTransform canvas)
            {
            }

            public void SetSelected(bool isSelected)
            {
                IsSelected = isSelected;
                SelectionCalls++;
            }
        }

        private class LaneRecorder : IPreviewLaneGuide
        {
            public int SelectedIndex { get; private set; }

            public void Refresh(Camera camera, RectTransform canvas, int selectedIndex)
            {
                SelectedIndex = selectedIndex;
            }
        }

        private class LayoutRecorder : IPreviewHudLayout
        {
            public void Apply(bool portrait, float canvasScale = 1)
            {
            }
        }

        private class FramingRecorder : IPreviewCameraFraming
        {
            public void RefreshFraming()
            {
            }
        }

        private class BusShape : IPreviewBusShape
        {
            public Vector3 DoorPosition => Vector3.zero;

            public Bounds GetPhysicalBounds()
            {
                return new Bounds(Vector3.zero, new Vector3(4, 2, 2));
            }
        }
    }
}
