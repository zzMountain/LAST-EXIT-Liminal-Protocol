using LastExit.ArtLab.Preview;
using LastExit.Editor.ArtLab;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LastExit.Tests.ArtLab
{
    public class ArtLabPolishLayoutTests
    {
        private GameObject _root;
        private RectTransform _header;
        private RectTransform _footer;
        private RectTransform _reset;
        private Text[] _headerText;
        private Text[] _footerText;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }
        }

        [TestCase(1600, 900)]
        [TestCase(640, 360)]
        public void LandscapePanelsLeaveTheCentralBoardClearAndResetStaysAtLeast44Pixels(int width, int height)
        {
            float scale = Mathf.Sqrt(width * height) / 1080f;
            PreviewHudLayout layout = CreateLayout(width / scale, height / scale);
            layout.Apply(false, scale);
            RectTransform rootRect = (RectTransform)_root.transform;
            Rect board = new Rect(-rootRect.rect.width * .25f, -rootRect.rect.height * .5f,
                rootRect.rect.width * .5f, rootRect.rect.height);
            Assert.That(GetBounds(_header).Overlaps(board), Is.False);
            Assert.That(GetBounds(_footer).Overlaps(board), Is.False);
            Assert.That(_reset.rect.height * scale, Is.GreaterThanOrEqualTo(43.99f));
            Assert.That(GetBounds(_reset).Overlaps(GetBounds(_headerText[2].rectTransform)), Is.False);

            foreach (Text text in _footerText)
            {
                Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height), text.name);
                Assert.That(text.preferredWidth, Is.LessThanOrEqualTo(text.rectTransform.rect.width), text.name);
                Assert.That(GetBounds(_footer).Contains(GetBounds(text.rectTransform).min), Is.True, text.name);
                Assert.That(GetBounds(_footer).Contains(GetBounds(text.rectTransform).max), Is.True, text.name);
            }
        }

        [Test]
        public void OrientationRoundTripRestoresPortraitBarsWithoutChangingButtonBinding()
        {
            PreviewHudLayout layout = CreateLayout(720, 1280);
            layout.Apply(true, .5f);
            Rect firstHeader = GetBounds(_header);
            Rect firstFooter = GetBounds(_footer);
            layout.Apply(false, .5f);
            layout.Apply(true, .5f);
            Assert.That(GetBounds(_header), Is.EqualTo(firstHeader));
            Assert.That(GetBounds(_footer), Is.EqualTo(firstFooter));
            Assert.That(_reset.rect.height * .5f, Is.GreaterThanOrEqualTo(44));
            Assert.That(_reset.parent, Is.SameAs(_header));
            Assert.That(_headerText[1].text, Does.Contain("БЕЗ СИМУЛЯЦИИ"));
        }

        [Test]
        public void InspectorTextChangeRefreshesWithoutChangingOrientationOrSourceAsset()
        {
            PreviewHudLayout layout = CreateLayout(720, 1280);
            PreviewHudLayoutPreset original = layout.PortraitLayout;
            PreviewHudLayoutPreset editable = Object.Instantiate(original);

            try
            {
                layout.SetPresets(editable, layout.LandscapeLayout);
                layout.Apply(true, .5f);
                string originalText = _headerText[0].text;
                SerializedObject inspector = new SerializedObject(editable);
                inspector.FindProperty("_headerText").GetArrayElementAtIndex(0).FindPropertyRelative("_text")
                    .stringValue = "Edited in Inspector";
                inspector.ApplyModifiedPropertiesWithoutUndo();
                layout.Apply(true, .5f);
                Assert.That(_headerText[0].text, Is.EqualTo("Edited in Inspector"));
                layout.SetPresets(original, layout.LandscapeLayout);
                layout.Apply(true, .5f);
                Assert.That(_headerText[0].text, Is.EqualTo(originalText));
            }
            finally
            {
                Object.DestroyImmediate(editable);
            }
        }

        [Test]
        public void ManualEditModeKeepsRectChangesAndCanCaptureThemIntoPreset()
        {
            PreviewHudLayout layout = CreateLayout(720, 1280);
            layout.Apply(true, .5f);
            PreviewHudLayoutPreset editable = Object.Instantiate(layout.PortraitLayout);

            try
            {
                SerializedObject inspector = new SerializedObject(layout);
                inspector.FindProperty("_previewInEditMode").boolValue = false;
                inspector.ApplyModifiedPropertiesWithoutUndo();
                Vector2 editedPosition = new Vector2(27, -19);
                _header.anchoredPosition = editedPosition;
                layout.Apply(false, .5f);
                Assert.That(_header.anchoredPosition, Is.EqualTo(editedPosition));
                layout.CaptureTo(editable);
                layout.SetPresets(editable, layout.LandscapeLayout);
                inspector.Update();
                inspector.FindProperty("_previewInEditMode").boolValue = true;
                inspector.ApplyModifiedPropertiesWithoutUndo();
                _header.anchoredPosition = Vector2.zero;
                layout.Apply(true, .5f);
                Assert.That(_header.anchoredPosition, Is.EqualTo(editedPosition));
            }
            finally
            {
                Object.DestroyImmediate(editable);
            }
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
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Rect GetBounds(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private PreviewHudLayout CreateLayout(float width, float height)
        {
            _root = new GameObject("Polish Layout Test", typeof(RectTransform));
            RectTransform rootRect = (RectTransform)_root.transform;
            rootRect.sizeDelta = new Vector2(width, height);
            _header = CreateRect("Header", rootRect);
            _footer = CreateRect("Footer", rootRect);
            _reset = CreateRect("Reset", _header);
            _headerText = new Text[4];
            _footerText = new Text[3];

            for (int index = 0; index < _headerText.Length; index++)
            {
                _headerText[index] = CreateText("Header Text " + index, index == 3 ? _reset : _header);
            }

            for (int index = 0; index < _footerText.Length; index++)
            {
                _footerText[index] = CreateText("Footer Text " + index, _footer);
            }

            PreviewHudLayout layout = _root.AddComponent<PreviewHudLayout>();
            layout.Initialize(_header, _footer, _reset, _headerText, _footerText);
            ArtLabHudAssets.Configure(layout);
            return layout;
        }
    }
}
