using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LastExit.Editor.Gameplay
{
    // Embed glyph data explicitly: the Editor's installed font fallback is unavailable in WebGL.
    public static class GameplayFontAuthoring
    {
        public const string FontPath = "Assets/_LAST_EXIT/Art/Fonts/Inter-Regular.otf";
        private const string ApplyCommand = "LAST EXIT/Gameplay/Apply Bundled Cyrillic Font";
        private const string Cyrillic = "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ" +
            "абвгдеёжзийклмнопрстуфхцчшщъыьэюя";

        [MenuItem(ApplyCommand)]
        public static void Apply()
        {
            EnsureSavedScenes();
            string sourceHash = GetHash(LevelSceneAuthoring.SourcePath);
            Font font = ImportFont();
            Scene scene = SceneManager.GetSceneByPath(LevelSceneAuthoring.ScenePath);

            if (scene.isLoaded == false)
            {
                scene = EditorSceneManager.OpenScene(LevelSceneAuthoring.ScenePath, OpenSceneMode.Single);
            }

            int uiLabels = 0;
            int worldLabels = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Text label in root.GetComponentsInChildren<Text>(true))
                {
                    label.font = font;
                    EditorUtility.SetDirty(label);
                    uiLabels++;
                }

                foreach (TextMesh label in root.GetComponentsInChildren<TextMesh>(true))
                {
                    label.font = font;
                    Renderer renderer = label.GetComponent<Renderer>();
                    renderer.sharedMaterial = font.material;
                    EditorUtility.SetDirty(label);
                    EditorUtility.SetDirty(renderer);
                    worldLabels++;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);

            if (EditorSceneManager.SaveScene(scene) == false)
            {
                throw new InvalidOperationException("Could not save the bundled font links in StationL07.");
            }

            AssetDatabase.SaveAssets();

            if (GetHash(LevelSceneAuthoring.SourcePath) != sourceHash)
            {
                throw new InvalidOperationException("Saved Polish changed during the gameplay font migration.");
            }

            Debug.Log("StationL07 uses embedded Inter font: UI=" + uiLabels + "; World=" + worldLabels +
                "; Cyrillic verified; saved Polish unchanged.");
        }

        private static Font ImportFont()
        {
            AssetDatabase.ImportAsset(FontPath, ImportAssetOptions.ForceUpdate);
            TrueTypeFontImporter importer = AssetImporter.GetAtPath(FontPath) as TrueTypeFontImporter;

            if (importer == null)
            {
                throw new InvalidOperationException("Copy the licensed Inter-Regular.otf to " + FontPath + " first.");
            }

            importer.fontTextureCase = FontTextureCase.Dynamic;
            importer.includeFontData = true;
            importer.fontSize = 26;
            importer.fontNames = new[] { "Inter" };
            importer.fontReferences = Array.Empty<Font>();
            importer.SaveAndReimport();
            Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);

            if (font == null)
            {
                throw new InvalidOperationException("The embedded Inter font failed to import.");
            }

            foreach (char character in Cyrillic)
            {
                if (font.HasCharacter(character) == false)
                {
                    throw new InvalidOperationException("The bundled font has no Cyrillic glyph: " + character);
                }
            }

            return font;
        }

        private static void EnsureSavedScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Exit Play mode before applying the bundled font.");
            }

            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                if (SceneManager.GetSceneAt(index).isDirty)
                {
                    throw new InvalidOperationException("Save modified scenes before applying the bundled font.");
                }
            }
        }

        private static string GetHash(string assetPath)
        {
            string projectPath = Directory.GetParent(Application.dataPath).FullName;

            using (SHA256 sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(projectPath, assetPath))));
            }
        }
    }
}
