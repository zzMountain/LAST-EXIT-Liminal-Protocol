using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using LastExit.ArtLab.Preview;
using LastExit.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LastExit.Editor.Gameplay
{
    // Copies the saved authored scene. Never invokes an Art Lab generator or modifies shared assets.
    public static class LevelSceneAuthoring
    {
        public const string ScenePath = "Assets/_LAST_EXIT/Scenes/StationL07.unity";
        public const string SourcePath = "Assets/_LAST_EXIT/Scenes/ArtLab/StationL07_ArtLab_Polish.unity";
        public const string BackupPath = "Assets/_LAST_EXIT/Scenes/StationL07_BeforeGameplay.unity";
        private const string MaterialFolder = "Assets/_LAST_EXIT/Art/BlenderKit/Materials_Refined/ColdLiminal/";
        private const string CreateCommand = "LAST EXIT/Gameplay/Create LEVEL-01 From Saved Polish";

        private static readonly string[] s_routes = { "R", "T", "B", "G" };

        [MenuItem(CreateCommand)]
        public static void Create()
        {
            EnsureSavedScenes();
            EnsureTargetIsNotGameplay();
            string sourceHash = GetHash(SourcePath);
            SyncLevelContent.Sync();
            TextAsset level = RequireAsset<TextAsset>(SyncLevelContent.AssetPath);

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BackupPath) == null &&
                AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                if (AssetDatabase.CopyAsset(ScenePath, BackupPath) == false)
                {
                    throw new InvalidOperationException("Could not preserve the previous StationL07 scene.");
                }
            }

            Scene scene = EditorSceneManager.OpenScene(SourcePath, OpenSceneMode.Single);

            if (EditorSceneManager.SaveScene(scene, ScenePath) == false)
            {
                throw new InvalidOperationException("Could not create the gameplay scene from saved Polish.");
            }

            Transform look = scene.GetRootGameObjects().Single(root => root.name == "LOOK_ColdLiminal").transform;
            Canvas canvas = look.GetComponentsInChildren<Canvas>(true).Single();
            Camera camera = canvas.worldCamera;
            PreviewHudLayout layout = canvas.GetComponent<PreviewHudLayout>();
            PreviewCanvasScaler scaler = canvas.GetComponent<PreviewCanvasScaler>();
            PreviewPointerInput input = canvas.GetComponent<PreviewPointerInput>();
            PreviewCameraFraming framing = camera.GetComponent<PreviewCameraFraming>();
            Transform templates = CreateRoot("Gameplay Templates", look);
            GameObject busTemplate = CloneTemplate(look.Find("LE_BUS_MINI_T_R2").gameObject,
                templates, "Shared MINI Template");
            GameObject passengerTemplate = CloneTemplate(look.Find("Passenger_R_0").gameObject,
                templates, "Shared Passenger Template");
            PreviewBusBadge sourceBadge = canvas.GetComponentsInChildren<PreviewBusBadge>(true)
                .First(badge => badge.name.StartsWith("Route R ", StringComparison.Ordinal));
            GameObject badgeTemplate = CloneTemplate(sourceBadge.gameObject, canvas.transform, "Bus Badge Template");
            RemoveComponents<PreviewBus>(busTemplate);
            RemoveComponents<PreviewBusBadge>(badgeTemplate);
            Transform actors = CreateRoot("Level Actors", look);

            RemovePreview(look, canvas);
            RectTransform overlay = CreateRect("Gameplay Overlay", (RectTransform)canvas.transform);
            Stretch(overlay);
            overlay.SetAsFirstSibling();

            RectTransform header = RequireChild<RectTransform>(canvas.transform, "Header");
            RectTransform footer = RequireChild<RectTransform>(canvas.transform, "Footer");
            Button restart = RequireChild<Button>(header, "Reset Layout");
            restart.onClick = new Button.ButtonClickedEvent();
            Button continueButton = CloneButton(restart, footer, "Continue", "Продолжить");
            RectTransform continueRect = (RectTransform)continueButton.transform;
            continueRect.anchorMin = new Vector2(1, 0);
            continueRect.anchorMax = new Vector2(1, 0);
            continueRect.pivot = new Vector2(1, 0);
            continueRect.anchoredPosition = new Vector2(-12, 8);
            continueRect.sizeDelta = new Vector2(170, 48);
            continueButton.gameObject.SetActive(false);
            GameObject victory = CreateVictory((RectTransform)canvas.transform, restart, out Button repeat);

            LevelBoardView board = actors.gameObject.AddComponent<LevelBoardView>();
            SetObject(board, "_busTemplate", busTemplate);
            SetObject(board, "_passengerTemplate", passengerTemplate);
            SetObject(board, "_actors", actors);
            SetMaterials(board, "_routeMaterials", "Route_");
            SetMaterials(board, "_emblemMaterials", "Emblem_");
            SetFloat(board, "_originX", -5);
            SetFloat(board, "_cellSize", 1);
            SetFloat(board, "_rowStartZ", 2);
            SetFloat(board, "_rowSpacing", 3.7f);
            SetFloat(board, "_queueHeadZ", 6.6f);
            SetFloat(board, "_queueSpacing", 1.35f);

            LevelHud hud = canvas.gameObject.AddComponent<LevelHud>();
            SetObject(hud, "_canvas", (RectTransform)canvas.transform);
            SetObject(hud, "_scaler", scaler);
            SetObject(hud, "_layout", layout);
            // CampaignSceneAuthoring owns the LevelCameraFraming binding after preview migration.
            SetObject(hud, "_header", header);
            SetObject(hud, "_footer", footer);
            SetObject(hud, "_counts", RequireChild<Text>(header, "Scene Counts"));
            SetObject(hud, "_title", RequireChild<Text>(header, "Station"));
            SetObject(hud, "_subtitle", RequireChild<Text>(header, "Preview Boundary"));
            SetObject(hud, "_hint", RequireChild<Text>(footer, "Drag Hint"));
            SetObject(hud, "_arrivals", RequireChild<Text>(footer, "Arrivals Unconfigured"));
            SetObject(hud, "_status", RequireChild<Text>(footer, "Safety Unavailable"));
            SetObject(hud, "_restart", restart);
            SetObject(hud, "_continue", continueButton);
            SetObject(hud, "_victoryPanel", victory);
            SetObject(hud, "_repeat", repeat);
            SetObject(hud, "_badgeTemplate", badgeTemplate);
            SetObject(hud, "_overlay", overlay);

            LevelBusDrag drag = canvas.gameObject.AddComponent<LevelBusDrag>();
            LevelSequencePlayer sequence = canvas.gameObject.AddComponent<LevelSequencePlayer>();
            LevelComposition composition = canvas.gameObject.AddComponent<LevelComposition>();
            SetObject(composition, "_level", level);
            SetObject(composition, "_camera", camera);
            SetObject(composition, "_input", input);
            SetObject(composition, "_board", board);
            SetObject(composition, "_hud", hud);
            SetObject(composition, "_drag", drag);
            SetObject(composition, "_sequence", sequence);

            // DEC-002 temporary framing. Domain coordinates remain identical in either orientation.
            camera.transform.position = new Vector3(0, 24, -11);
            camera.transform.LookAt(new Vector3(0, .5f, 4));
            framing.Initialize(10.5f, 12);

            if (EditorSceneManager.SaveScene(scene) == false)
            {
                throw new IOException("Could not save the initial gameplay scene before applying campaign bindings.");
            }

            GameplayFontAuthoring.Apply();
            PassengerAnimationAuthoring.Apply();
            CampaignSceneAuthoring.Apply();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();

            if (GetHash(SourcePath) != sourceHash)
            {
                throw new InvalidOperationException("Saved Polish source changed during gameplay migration.");
            }

            Debug.Log("LEVEL-01 authored from saved Polish; source unchanged; previous StationL07 preserved at " +
                BackupPath + ". Shared template calibration is owned by LevelBoardView.");
        }

        private static void RemovePreview(Transform look, Canvas canvas)
        {
            RemoveComponents<PreviewComposition>(canvas.gameObject);
            RemoveComponents<ArtLabPreviewHud>(canvas.gameObject);
            RemoveComponents<PreviewBusDrag>(canvas.gameObject);

            foreach (Transform child in look.Cast<Transform>().ToArray())
            {
                if (child.name.StartsWith("LE_BUS_", StringComparison.Ordinal) ||
                    child.name.StartsWith("Passenger_", StringComparison.Ordinal))
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }

            Object.DestroyImmediate(canvas.transform.Find("Physical Path Guides").gameObject);
            Object.DestroyImmediate(canvas.transform.Find("Bus And Queue Badges").gameObject);
            Transform roads = look.Find("UX Road Markings");

            foreach (Transform road in roads.Cast<Transform>().ToArray())
            {
                if (road.name.StartsWith("Road R3 ", StringComparison.Ordinal) ||
                    road.name.StartsWith("Road R4 ", StringComparison.Ordinal) ||
                    road.name.StartsWith("Road R5 ", StringComparison.Ordinal))
                {
                    Object.DestroyImmediate(road.gameObject);
                }
            }
        }

        private static GameObject CreateVictory(RectTransform canvas, Button source, out Button repeat)
        {
            RectTransform modal = CreateRect("Level Victory", canvas);
            Stretch(modal);
            Image veil = modal.gameObject.AddComponent<Image>();
            veil.color = new Color(.015f, .035f, .045f, .9f);
            veil.raycastTarget = true;
            RectTransform card = CreateRect("Victory Card", modal);
            card.sizeDelta = new Vector2(340, 210);
            Image panel = card.gameObject.AddComponent<Image>();
            panel.color = new Color(.055f, .09f, .11f);
            panel.raycastTarget = false;
            Text sourceText = source.GetComponentInChildren<Text>();
            GameObject label = Object.Instantiate(sourceText.gameObject, card, false);
            label.name = "Victory Title";
            Text text = label.GetComponent<Text>();
            text.text = "Уровень пройден";
            text.fontSize = 28;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.rectTransform.anchorMin = new Vector2(.5f, .5f);
            text.rectTransform.anchorMax = new Vector2(.5f, .5f);
            text.rectTransform.anchoredPosition = new Vector2(0, 46);
            text.rectTransform.sizeDelta = new Vector2(320, 65);
            repeat = CloneButton(source, card, "Repeat", "Повторить");
            RectTransform repeatRect = (RectTransform)repeat.transform;
            repeatRect.anchorMin = new Vector2(.5f, .5f);
            repeatRect.anchorMax = new Vector2(.5f, .5f);
            repeatRect.pivot = new Vector2(.5f, .5f);
            repeatRect.anchoredPosition = new Vector2(0, -46);
            repeatRect.sizeDelta = new Vector2(180, 52);
            modal.gameObject.SetActive(false);
            return modal.gameObject;
        }

        private static Button CloneButton(Button source, RectTransform parent, string name, string label)
        {
            Button button = Object.Instantiate(source, parent, false);
            button.name = name;
            button.onClick = new Button.ButtonClickedEvent();
            Text text = button.GetComponentInChildren<Text>();
            text.text = label;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.sizeDelta = Vector2.zero;
            text.rectTransform.anchoredPosition = Vector2.zero;
            return button;
        }

        private static GameObject CloneTemplate(GameObject source, Transform parent, string name)
        {
            GameObject template = Object.Instantiate(source, parent, true);
            template.name = name;
            template.SetActive(false);
            return template;
        }

        private static Transform CreateRoot(string name, Transform parent)
        {
            Transform root = new GameObject(name).transform;
            root.SetParent(parent, false);
            return root;
        }

        private static RectTransform CreateRect(string name, RectTransform parent)
        {
            RectTransform rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(.5f, .5f);
            rect.anchorMax = rect.anchorMin;
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void RemoveComponents<T>(GameObject root) where T : Component
        {
            foreach (T component in root.GetComponentsInChildren<T>(true))
            {
                Object.DestroyImmediate(component);
            }
        }

        private static T RequireChild<T>(Transform parent, string name) where T : Component
        {
            Transform transform = parent.Find(name);
            T child = transform == null ? null : transform.GetComponent<T>();

            if (child == null)
            {
                throw new InvalidOperationException("Saved Polish is missing required component: " + name);
            }

            return child;
        }

        private static T RequireAsset<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);

            if (asset == null)
            {
                throw new InvalidOperationException("Required authored asset missing: " + path);
            }

            return asset;
        }

        private static void SetObject(Object target, string field, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string field, float value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetMaterials(Object target, string field, string prefix)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty array = serialized.FindProperty(field);
            array.arraySize = s_routes.Length;

            for (int index = 0; index < s_routes.Length; index++)
            {
                array.GetArrayElementAtIndex(index).objectReferenceValue =
                    RequireAsset<Material>(MaterialFolder + prefix + s_routes[index] + ".mat");
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureSavedScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Exit Play mode before authoring LEVEL-01.");
            }

            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                if (SceneManager.GetSceneAt(index).isDirty)
                {
                    throw new InvalidOperationException("Save modified scenes before authoring LEVEL-01.");
                }
            }
        }

        private static void EnsureTargetIsNotGameplay()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                return;
            }

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedForInspection = scene.isLoaded == false;

            if (openedForInspection)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }

            bool authored = scene.GetRootGameObjects().Any(root =>
                root.GetComponentInChildren<LevelComposition>(true) != null);

            if (openedForInspection)
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            if (authored)
            {
                throw new InvalidOperationException("StationL07 already contains gameplay. Open the saved scene; " +
                    "migration refuses to overwrite its authored changes with the Polish source.");
            }
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
