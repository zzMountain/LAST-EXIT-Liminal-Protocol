using System;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using LastExit.ArtLab.Preview;
using LastExit.Presentation;
using LastExit.Platform;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LastExit.Editor.Gameplay
{
    // Upgrades the saved gameplay scene in place; does not regenerate the authored Art Lab scene.
    public static class CampaignSceneAuthoring
    {
        private const string ApplyCommand = "LAST EXIT/Gameplay/Apply LEVEL-01–15 Campaign";
        private const string ExpandCommand = "LAST EXIT/Gameplay/Expand Saved Campaign to LEVEL-15";
        private const string ContentFolder = "Assets/_LAST_EXIT/Resources/Levels/";
        private const string BackupPath = "Assets/_LAST_EXIT/Scenes/StationL07_BeforeCampaign.unity";

        // Content-only expansion preserves the current scan, audio and authored pace bindings.
        [MenuItem(ExpandCommand)]
        public static void Expand()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Exit Play mode before expanding the campaign.");
            }

            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                if (SceneManager.GetSceneAt(index).isDirty)
                {
                    throw new InvalidOperationException("Save modified scenes before expanding the campaign.");
                }
            }

            const string ExpansionBackup = "Assets/_LAST_EXIT/Scenes/StationL07_BeforeCampaign15.unity";

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ExpansionBackup) == null &&
                AssetDatabase.CopyAsset(LevelSceneAuthoring.ScenePath, ExpansionBackup) == false)
            {
                throw new InvalidOperationException("Could not preserve the scene before campaign expansion.");
            }

            string sourceHash = GetHash(LevelSceneAuthoring.SourcePath);
            SyncLevelContent.Sync();
            Scene scene = EditorSceneManager.OpenScene(LevelSceneAuthoring.ScenePath, OpenSceneMode.Single);
            LevelComposition composition = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LevelComposition>(true)).Single();
            LevelBoardView board = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LevelBoardView>(true)).Single();
            AddLongTemplateAndRoads(board, composition.transform.parent.Find("Gameplay Templates"));
            BindLevels(composition);
            YandexPlatform platform = composition.GetComponent<YandexPlatform>();

            if (platform == null)
            {
                platform = composition.gameObject.AddComponent<YandexPlatform>();
            }

            SetObject(composition, "_platform", platform);
            EditorSceneManager.MarkSceneDirty(scene);

            if (EditorSceneManager.SaveScene(scene) == false)
            {
                throw new InvalidOperationException("Could not save the expanded campaign.");
            }

            AssetDatabase.SaveAssets();

            if (GetHash(LevelSceneAuthoring.SourcePath) != sourceHash)
            {
                throw new InvalidOperationException("Campaign expansion changed the saved Polish source.");
            }
        }

        [MenuItem(ApplyCommand)]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Exit Play mode before applying campaign scene bindings.");
            }

            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                if (SceneManager.GetSceneAt(index).isDirty)
                {
                    throw new InvalidOperationException("Save modified scenes before applying campaign bindings.");
                }
            }

            string sourceHash = GetHash(LevelSceneAuthoring.SourcePath);

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BackupPath) == null &&
                AssetDatabase.CopyAsset(LevelSceneAuthoring.ScenePath, BackupPath) == false)
            {
                throw new InvalidOperationException("Could not preserve the scene before campaign migration.");
            }

            Scene scene = SceneManager.GetSceneByPath(LevelSceneAuthoring.ScenePath);

            if (scene.isLoaded == false)
            {
                scene = EditorSceneManager.OpenScene(LevelSceneAuthoring.ScenePath, OpenSceneMode.Single);
            }

            LevelComposition composition = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LevelComposition>(true)).Single();
            LevelHud hud = composition.GetComponent<LevelHud>();
            RectTransform canvas = (RectTransform)hud.transform;
            AuthorFraming(hud, canvas.GetComponent<Canvas>().worldCamera);
            Button source = canvas.Find("Header/Reset Layout").GetComponent<Button>();
            Transform look = composition.transform.parent;
            LevelBoardView board = look.GetComponentInChildren<LevelBoardView>(true);
            Transform templates = look.Find("Gameplay Templates");
            AddStandardTemplate(board, templates);
            AddLongTemplateAndRoads(board, templates);
            BindLevels(composition);
            YandexPlatform platform = composition.GetComponent<YandexPlatform>();

            if (platform == null)
            {
                platform = composition.gameObject.AddComponent<YandexPlatform>();
            }

            SetObject(composition, "_platform", platform);
            AuthorResults(hud, canvas, source);
            AuthorSecurity(hud, canvas, source);
            EditorSceneManager.MarkSceneDirty(scene);

            if (EditorSceneManager.SaveScene(scene) == false)
            {
                throw new InvalidOperationException("Could not save the campaign scene bindings.");
            }

            AssetDatabase.SaveAssets();

            if (GetHash(LevelSceneAuthoring.SourcePath) != sourceHash)
            {
                throw new InvalidOperationException("Campaign migration changed the saved Polish source.");
            }
        }

        private static void BindLevels(LevelComposition composition)
        {
            SerializedObject serialized = new SerializedObject(composition);
            SerializedProperty levels = serialized.FindProperty("_levels");
            levels.arraySize = 15;

            for (int index = 0; index < levels.arraySize; index++)
            {
                string path = ContentFolder + "level_" + (index + 1).ToString("00") + ".json";
                TextAsset level = AssetDatabase.LoadAssetAtPath<TextAsset>(path);

                if (level == null)
                {
                    throw new InvalidOperationException("Sync campaign content first. Missing " + path);
                }

                levels.GetArrayElementAtIndex(index).objectReferenceValue = level;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AuthorFraming(LevelHud hud, Camera camera)
        {
            LevelCameraFraming framing = camera.GetComponent<LevelCameraFraming>();

            if (framing == null)
            {
                framing = camera.gameObject.AddComponent<LevelCameraFraming>();
                SerializedObject serialized = new SerializedObject(framing);
                serialized.FindProperty("_camera").objectReferenceValue = camera;
                serialized.FindProperty("_basePosition").vector3Value = camera.transform.position;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            SetObject(hud, "_framing", framing);
            PreviewCameraFraming previous = camera.GetComponent<PreviewCameraFraming>();

            if (previous != null)
            {
                Object.DestroyImmediate(previous);
            }
        }

        private static void AddStandardTemplate(LevelBoardView board, Transform templates)
        {
            Transform standard = templates.Find("Shared STANDARD Template");

            if (standard == null)
            {
                Scene source = SceneManager.GetSceneByPath(LevelSceneAuthoring.SourcePath);
                bool openedForMigration = source.isLoaded == false;

                if (openedForMigration)
                {
                    source = EditorSceneManager.OpenScene(LevelSceneAuthoring.SourcePath, OpenSceneMode.Additive);
                }

                try
                {
                    Transform look = source.GetRootGameObjects().Single(root => root.name == "LOOK_ColdLiminal").transform;
                    PreviewBus reference = look.GetComponentsInChildren<PreviewBus>(true)
                        .First(bus => bus.name.StartsWith("LE_BUS_STANDARD_", StringComparison.Ordinal));
                    GameObject clone = Object.Instantiate(reference.gameObject, templates, true);
                    clone.name = "Shared STANDARD Template";
                    clone.SetActive(false);

                    foreach (PreviewBus preview in clone.GetComponentsInChildren<PreviewBus>(true))
                    {
                        Object.DestroyImmediate(preview);
                    }

                    standard = clone.transform;
                    Transform sourceRoads = look.Find("UX Road Markings");
                    Transform targetRoads = templates.parent.Find("UX Road Markings");

                    foreach (Transform road in sourceRoads)
                    {
                        if (road.name.StartsWith("Road R3 ", StringComparison.Ordinal) &&
                            targetRoads.Find(road.name) == null)
                        {
                            GameObject roadClone = Object.Instantiate(road.gameObject, targetRoads, true);
                            roadClone.name = road.name;
                        }
                    }
                }
                finally
                {
                    if (openedForMigration)
                    {
                        EditorSceneManager.CloseScene(source, true);
                    }
                }
            }

            SetObject(board, "_standardBusTemplate", standard.gameObject);
        }

        private static void AddLongTemplateAndRoads(LevelBoardView board, Transform templates)
        {
            Scene source = SceneManager.GetSceneByPath(LevelSceneAuthoring.SourcePath);
            bool openedForMigration = source.isLoaded == false;

            if (openedForMigration)
            {
                source = EditorSceneManager.OpenScene(LevelSceneAuthoring.SourcePath, OpenSceneMode.Additive);
            }

            try
            {
                Transform look = source.GetRootGameObjects().Single(root => root.name == "LOOK_ColdLiminal").transform;
                Transform longBus = templates.Find("Shared LONG Template");

                if (longBus == null)
                {
                    PreviewBus reference = look.GetComponentsInChildren<PreviewBus>(true)
                        .First(bus => bus.name.StartsWith("LE_BUS_LONG_", StringComparison.Ordinal));
                    GameObject clone = Object.Instantiate(reference.gameObject, templates, true);
                    clone.name = "Shared LONG Template";
                    clone.SetActive(false);

                    foreach (PreviewBus preview in clone.GetComponentsInChildren<PreviewBus>(true))
                    {
                        Object.DestroyImmediate(preview);
                    }

                    longBus = clone.transform;
                }

                SetObject(board, "_longBusTemplate", longBus.gameObject);
                Transform sourceRoads = look.Find("UX Road Markings");
                Transform targetRoads = templates.parent.Find("UX Road Markings");

                foreach (Transform road in sourceRoads)
                {
                    if ((road.name.StartsWith("Road R4 ", StringComparison.Ordinal) ||
                        road.name.StartsWith("Road R5 ", StringComparison.Ordinal)) &&
                        targetRoads.Find(road.name) == null)
                    {
                        GameObject clone = Object.Instantiate(road.gameObject, targetRoads, true);
                        clone.name = road.name;
                    }
                }
            }
            finally
            {
                if (openedForMigration)
                {
                    EditorSceneManager.CloseScene(source, true);
                }
            }
        }

        private static void AuthorResults(LevelHud hud, RectTransform canvas, Button source)
        {
            RectTransform card = canvas.Find("Level Victory/Victory Card").GetComponent<RectTransform>();
            Text title = card.Find("Victory Title").GetComponent<Text>();
            SetTextRect(title, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -40), new Vector2(-32, 50));
            title.fontSize = 30;
            Text details = RequireText(card, "Result Details", source.GetComponentInChildren<Text>());
            SetTextRect(details, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -94), new Vector2(-40, 55));
            details.fontSize = 24;
            details.text = string.Empty;
            Button next = RequireButton(card, "Next Level", "Следующий уровень", source);
            SetObject(hud, "_next", next);
            SetObject(hud, "_resultTitle", title);
            SetObject(hud, "_resultDetails", details);
            SetObject(hud, "_resultCard", card);
        }

        private static void AuthorSecurity(LevelHud hud, RectTransform canvas, Button source)
        {
            RectTransform footer = canvas.Find("Footer").GetComponent<RectTransform>();
            RectTransform controls = RequireRect(footer, "Security Controls");
            controls.gameObject.SetActive(false);
            Stretch(controls);
            LevelSecurityPanel security = controls.GetComponent<LevelSecurityPanel>();

            if (security == null)
            {
                security = controls.gameObject.AddComponent<LevelSecurityPanel>();
            }

            Button scan = RequireButton(controls, "CCTV Scan", "CCTV · проверить головы", source);
            SetObject(security, "_scan", scan);
            SerializedObject serialized = new SerializedObject(security);
            SerializedProperty quarantine = serialized.FindProperty("_quarantine");
            quarantine.arraySize = 4;

            for (int index = 0; index < quarantine.arraySize; index++)
            {
                string lane = "ABCD"[index].ToString();
                Button button = RequireButton(controls, "Quarantine " + lane, lane + "\nКарантин", source);
                quarantine.GetArrayElementAtIndex(index).objectReferenceValue = button;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            RectTransform modal = RequireRect(canvas, "Security Check");
            Stretch(modal);
            SetPanel(modal, new Color(.015f, .035f, .045f, .94f), true);
            RectTransform card = RequireRect(modal, "Security Card");
            SetPanel(card, new Color(.055f, .09f, .11f), false);
            Text explanation = RequireText(card, "Security Explanation", source.GetComponentInChildren<Text>());
            Button checkScan = RequireButton(card, "Security Scan", "Сканировать", source);
            Button risk = RequireButton(card, "Accept Risk", "Открыть с риском · −1", source);
            SetObject(security, "_checkPanel", modal.gameObject);
            SetObject(security, "_checkCard", card);
            SetObject(security, "_checkText", explanation);
            SetObject(security, "_checkScan", checkScan);
            SetObject(security, "_risk", risk);
            SetObject(hud, "_security", security);
            modal.gameObject.SetActive(false);
        }

        private static Button RequireButton(RectTransform parent, string name, string label, Button source)
        {
            Transform child = parent.Find(name);
            Button button = child == null ? Object.Instantiate(source, parent, false) : child.GetComponent<Button>();
            button.name = name;
            button.onClick = new Button.ButtonClickedEvent();
            button.gameObject.SetActive(true);
            Text text = button.GetComponentInChildren<Text>();
            text.text = label;
            text.fontSize = 26;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(4, 2);
            text.rectTransform.offsetMax = new Vector2(-4, -2);
            return button;
        }

        private static Text RequireText(RectTransform parent, string name, Text source)
        {
            Transform child = parent.Find(name);
            Text text = child == null ? Object.Instantiate(source, parent, false) : child.GetComponent<Text>();
            text.name = name;
            text.raycastTarget = false;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static RectTransform RequireRect(RectTransform parent, string name)
        {
            Transform child = parent.Find(name);

            if (child != null)
            {
                return (RectTransform)child;
            }

            RectTransform rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(.5f, .5f);
            rect.anchorMax = rect.anchorMin;
            return rect;
        }

        private static void SetPanel(RectTransform rect, Color color, bool raycastTarget)
        {
            Image image = rect.GetComponent<Image>();

            if (image == null)
            {
                image = rect.gameObject.AddComponent<Image>();
            }

            image.color = color;
            image.raycastTarget = raycastTarget;
        }

        private static void SetTextRect(Text text, Vector2 minimum, Vector2 maximum, Vector2 position, Vector2 size)
        {
            text.rectTransform.anchorMin = minimum;
            text.rectTransform.anchorMax = maximum;
            text.rectTransform.pivot = new Vector2(.5f, .5f);
            text.rectTransform.anchoredPosition = position;
            text.rectTransform.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetObject(Object target, string field, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string GetHash(string path)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)));
            }
        }
    }
}
