using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LastExit.Tests.Gameplay
{
    public class GameplayFontTests
    {
        private const string FontPath = "Assets/_LAST_EXIT/Art/Fonts/Inter-Regular.otf";
        private const string ScenePath = "Assets/_LAST_EXIT/Scenes/StationL07.unity";

        [Test]
        public void BundledDynamicFontEmbedsCyrillicForTutorialAndVictory()
        {
            Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            Assert.That(font, Is.Not.Null, "WebGL must carry its own font instead of using Editor fallbacks.");
            TrueTypeFontImporter importer = AssetImporter.GetAtPath(FontPath) as TrueTypeFontImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.fontTextureCase, Is.EqualTo(FontTextureCase.Dynamic));
            Assert.That(importer.includeFontData, Is.True, "WebGL cannot find font files on the host OS.");
            Assert.That(importer.fontReferences, Is.Empty, "Cyrillic must be provided by the bundled face itself.");
            string playerText = "Совместите дверь с красной очередью. Пассажиры садятся автоматически. " +
                "Полный автобус уезжает. На этот ряд приехал следующий автобус. " +
                "Продолжить. Уровень пройден. Повторить. Перезапуск. КАРАНТИН. " +
                "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя";

            foreach (char character in playerText.Distinct())
            {
                Assert.That(font.HasCharacter(character), Is.True, "Missing bundled glyph: " + character);
            }
        }

        [Test]
        public void SavedGameplayIncludesBundledFontInEveryUiTemplateAndWorldSign()
        {
            Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            Assert.That(font, Is.Not.Null);
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedForInspection = scene.isLoaded == false;

            if (openedForInspection)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }

            try
            {
                GameObject[] roots = scene.GetRootGameObjects();
                Text[] uiLabels = roots.SelectMany(root => root.GetComponentsInChildren<Text>(true)).ToArray();
                TextMesh[] worldLabels = roots.SelectMany(root =>
                    root.GetComponentsInChildren<TextMesh>(true)).ToArray();
                Assert.That(uiLabels, Is.Not.Empty);
                Assert.That(worldLabels, Is.Not.Empty);

                foreach (Text label in uiLabels)
                {
                    Assert.That(label.font, Is.SameAs(font), label.name + " must use the embedded font.");
                }

                foreach (TextMesh label in worldLabels)
                {
                    Assert.That(label.font, Is.SameAs(font), label.name + " world font is not bundled.");
                    Assert.That(label.GetComponent<Renderer>().sharedMaterial, Is.SameAs(font.material),
                        label.name + " must sample the embedded font atlas.");
                }
            }
            finally
            {
                if (openedForInspection)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
    }
}
