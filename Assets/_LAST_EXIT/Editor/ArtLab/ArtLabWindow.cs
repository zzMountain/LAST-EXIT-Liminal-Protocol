using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastExit.Editor.ArtLab
{
    public class ArtLabWindow : EditorWindow
    {
        private const string OpenCommand = "LAST EXIT/Art Lab/Open Panel";

        private static readonly Dictionary<ArtLabLook, string> s_lookLabels = new Dictionary<ArtLabLook, string>
        {
            { ArtLabLook.Neutral, "01 · Нейтральный" },
            { ArtLabLook.ColdLiminal, "02 · Холодный liminal" },
            { ArtLabLook.WarmIndustrial, "03 · Тёплый технический" }
        };

        private void OnGUI()
        {
            EditorGUILayout.LabelField("L-07 · Scene and asset authoring", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Рабочая сцена уже сохранена: правьте объекты, материалы и свет в Unity. " +
                "HUD использует два Layout asset. Генераторы нужны только для отдельных пробных копий.", MessageType.Info);
            EditorGUI.BeginDisabledGroup(EditorApplication.isPlayingOrWillChangePlaymode);

            if (GUILayout.Button("Открыть UX-пробу · 5 рядов"))
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(ArtLabUxScene.ScenePath);
                }
            }

            if (ArtLabUxScene.IsPreviewScene(SceneManager.GetActiveScene().path) &&
                GUILayout.Button("Снять UX · обе ориентации"))
            {
                ArtLabUxCapture.Capture();
            }

            if (GUILayout.Button("Настройки HUD · Portrait / Landscape"))
            {
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<DefaultAsset>(ArtLabHudAssets.Folder);
                EditorGUIUtility.PingObject(Selection.activeObject);
            }

            if (GUILayout.Button("Открыть тестовую сцену"))
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(ArtLabSceneBuilder.ScenePath);
                }
            }

            bool active = SceneManager.GetActiveScene().path == ArtLabSceneBuilder.ScenePath;
            EditorGUI.BeginDisabledGroup(active == false);

            foreach (KeyValuePair<ArtLabLook, string> entry in s_lookLabels)
            {
                if (GUILayout.Button(entry.Value))
                {
                    ArtLabPreview.SetLook(entry.Key);
                }
            }

            if (GUILayout.Button("Снять все варианты · 16:9 и 9:16"))
            {
                ArtLabPreview.CaptureAll();
            }

            if (GUILayout.Button("Сохранить выбранный вариант"))
            {
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            }

            EditorGUI.EndDisabledGroup();
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Play запускает пробные Idle и Q3-клипы.", EditorStyles.wordWrappedLabel);
        }

        [MenuItem(OpenCommand)]
        public static void Open()
        {
            ArtLabWindow window = GetWindow<ArtLabWindow>("LAST EXIT · Art Lab");
            window.minSize = new Vector2(340, 390);
            window.Show();
        }
    }
}
