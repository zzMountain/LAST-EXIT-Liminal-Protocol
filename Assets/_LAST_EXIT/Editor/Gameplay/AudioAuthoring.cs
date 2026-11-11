using System;
using System.IO;
using System.Linq;
using LastExit.ArtLab.Preview;
using LastExit.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LastExit.Editor.Gameplay
{
    public static class AudioAuthoring
    {
        private const string AudioFolder = "Assets/_LAST_EXIT/Audio";
        private const string ClipFolder = AudioFolder + "/Clips";
        private const string SourceFolder = @"D:\LAST_EXIT_AudioLab\feedback_sfx";
        private const string LibraryPath = AudioFolder + "/LevelAudioLibrary.asset";
        private const string RootName = "Audio Feedback";
        private const string ApplyCommand = "LAST EXIT/Gameplay/Apply Action Audio";
        private const string LicenseSourceFolder = @"D:\LAST_EXIT_AudioLab\reports\licenses";
        private const string ModelManifestSourcePath = @"D:\LAST_EXIT_AudioLab\reports\model_manifest.json";
        private static readonly string[] s_files =
        {
            "station_A_loop", "bus_drag_loop", "scan_loop", "ui_click", "bus_select", "bus_snap",
            "action_rejected", "passenger_board", "bus_arrive_A", "bus_stop_A", "bus_depart",
            "scan_start", "scan_end", "security_alert", "quarantine_open_A", "quarantine_close_A",
            "quarantine_discharge", "stability_loss", "level_win", "level_lose"
        };
        private static readonly string[] s_supportingFiles =
        {
            "manifest.json", "README.md", "generate_feedback.py", "timing_comparison.json"
        };
        private static readonly string[] s_licenseFiles = { "LICENSE.md", "LICENSE_GEMMA.md" };

        [MenuItem(ApplyCommand)]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Exit Play mode before authoring audio.");
            }

            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                if (SceneManager.GetSceneAt(index).isDirty)
                {
                    throw new InvalidOperationException("Save modified scenes before authoring audio.");
                }
            }

            ValidateSources();
            Directory.CreateDirectory(ClipFolder);

            foreach (string file in s_files)
            {
                string path = ClipFolder + "/" + file + ".wav";
                File.Copy(Path.Combine(SourceFolder, file + ".wav"), path, true);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                AudioImporter importer = (AudioImporter)AssetImporter.GetAtPath(path);
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = .75f;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                settings.preloadAudioData = true;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = true;
                SerializedObject importData = new SerializedObject(importer);
                SerializedProperty normalize = importData.FindProperty("m_Normalize");

                if (normalize == null)
                {
                    throw new InvalidOperationException("Audio importer requires its normalization property.");
                }

                normalize.boolValue = false;
                importData.ApplyModifiedPropertiesWithoutUndo();
                importer.SaveAndReimport();
            }

            foreach (string file in s_supportingFiles)
            {
                File.Copy(Path.Combine(SourceFolder, file), Path.Combine(AudioFolder, file), true);
            }

            Directory.CreateDirectory(AudioFolder + "/Licenses");

            foreach (string file in s_licenseFiles)
            {
                File.Copy(Path.Combine(LicenseSourceFolder, file),
                    AudioFolder + "/Licenses/" + file, true);
            }

            File.Copy(ModelManifestSourcePath,
                AudioFolder + "/Licenses/model_manifest.json", true);
            AssetDatabase.Refresh();
            LevelAudioLibrary library = AssetDatabase.LoadAssetAtPath<LevelAudioLibrary>(LibraryPath);

            if (library == null)
            {
                library = ScriptableObject.CreateInstance<LevelAudioLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            SerializedObject data = new SerializedObject(library);
            SerializedProperty cues = data.FindProperty("_cues");
            cues.arraySize = s_files.Length;

            for (int index = 0; index < s_files.Length; index++)
            {
                SerializedProperty cue = cues.GetArrayElementAtIndex(index);
                cue.FindPropertyRelative("_sound").enumValueIndex = index;
                cue.FindPropertyRelative("_clip").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<AudioClip>(ClipFolder + "/" + s_files[index] + ".wav");
                cue.FindPropertyRelative("_volume").floatValue = GetVolume((LevelSound)index);
            }

            data.ApplyModifiedPropertiesWithoutUndo();
            Scene scene = EditorSceneManager.OpenScene(LevelSceneAuthoring.ScenePath, OpenSceneMode.Single);
            LevelComposition composition = Find<LevelComposition>(scene);
            LevelHud hud = Find<LevelHud>(scene);
            LevelBusDrag drag = Find<LevelBusDrag>(scene);
            LevelSecurityPanel security = Find<LevelSecurityPanel>(scene);
            LevelSequencePlayer sequence = Find<LevelSequencePlayer>(scene);
            QuarantineChamber chamber = Find<QuarantineChamber>(scene);
            PreviewPointerInput pointer = Find<PreviewPointerInput>(scene);
            Canvas canvas = hud.GetComponent<Canvas>();
            Transform oldRoot = canvas.transform.Find(RootName);

            if (oldRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(oldRoot.gameObject);
            }

            GameObject root = new GameObject(RootName);
            root.SetActive(false);
            root.transform.SetParent(canvas.transform, false);
            LevelAudioPlayback playback = root.AddComponent<LevelAudioPlayback>();
            SetObject(playback, "_library", library);
            SetSources(playback, "_loops", root.transform, 3, true);
            SetSources(playback, "_voices", root.transform, 8, false);
            AudioOptionsView options = root.AddComponent<AudioOptionsView>();
            CreateControls(canvas, options);
            LevelAudioFeedback feedback = root.AddComponent<LevelAudioFeedback>();
            SetObject(feedback, "_playback", playback);
            SetObject(feedback, "_options", options);
            SetObject(feedback, "_pointer", pointer);
            SetObject(feedback, "_drag", drag);
            SetObject(feedback, "_hud", hud);
            SetObject(feedback, "_security", security);
            SetObject(feedback, "_sequence", sequence);
            SetObject(feedback, "_chamber", chamber);
            SetObject(composition, "_audio", feedback);
            SetObject(hud, "_audioOptions", options);
            root.SetActive(true);
            EditorUtility.SetDirty(library);
            EditorSceneManager.MarkSceneDirty(scene);

            if (EditorSceneManager.SaveScene(scene) == false)
            {
                throw new IOException("Could not save the authored action audio bindings.");
            }

            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("TestResults");
            File.WriteAllText("TestResults/Audio-Authoring.txt",
                "20 cues; 3 loops; 8 voices; variants A; authored scene=" + scene.path);
        }

        private static void ValidateSources()
        {
            foreach (string file in s_files)
            {
                RequireSource(Path.Combine(SourceFolder, file + ".wav"));
            }

            foreach (string file in s_supportingFiles)
            {
                RequireSource(Path.Combine(SourceFolder, file));
            }

            foreach (string file in s_licenseFiles)
            {
                RequireSource(Path.Combine(LicenseSourceFolder, file));
            }

            RequireSource(ModelManifestSourcePath);
        }

        private static void RequireSource(string path)
        {
            if (File.Exists(path) == false)
            {
                throw new FileNotFoundException("Required audio authoring source is missing.", path);
            }
        }

        private static float GetVolume(LevelSound sound)
        {
            switch (sound)
            {
                case LevelSound.Atmosphere: return .65f;
                case LevelSound.BusDrag: return .45f;
                case LevelSound.ScanLoop: return .4f;
                case LevelSound.PassengerBoard: return .9f;
                case LevelSound.BusArrive: return .3f;
                case LevelSound.BusStop: return .3f;
                case LevelSound.QuarantineOpen:
                case LevelSound.QuarantineClose: return .4f;
                default: return .65f;
            }
        }

        private static T Find<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
        }

        private static void SetObject(UnityEngine.Object target, string name, UnityEngine.Object value)
        {
            SerializedObject data = new SerializedObject(target);
            data.FindProperty(name).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSources(LevelAudioPlayback playback, string field, Transform root, int count, bool loop)
        {
            SerializedObject data = new SerializedObject(playback);
            SerializedProperty array = data.FindProperty(field);
            array.arraySize = count;

            for (int index = 0; index < count; index++)
            {
                GameObject child = new GameObject((loop ? "Loop " : "Voice ") + index);
                child.transform.SetParent(root, false);
                AudioSource source = child.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = loop;
                source.spatialBlend = 0;
                source.volume = 0;
                array.GetArrayElementAtIndex(index).objectReferenceValue = source;
            }

            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateControls(Canvas canvas, AudioOptionsView options)
        {
            Transform header = canvas.transform.Find("Header");
            Transform oldButton = header.Find("Audio Options");
            Transform oldPanel = canvas.transform.Find("Audio Options Panel");

            if (oldButton != null)
            {
                UnityEngine.Object.DestroyImmediate(oldButton.gameObject);
            }

            if (oldPanel != null)
            {
                UnityEngine.Object.DestroyImmediate(oldPanel.gameObject);
            }

            Button template = header.GetComponentsInChildren<Button>(true).First();
            Button button = UnityEngine.Object.Instantiate(template, header);
            button.name = "Audio Options";
            button.onClick = new Button.ButtonClickedEvent();
            button.gameObject.SetActive(true);
            Text label = button.GetComponentInChildren<Text>();
            label.text = "Звук";
            GameObject panelObject = new GameObject("Audio Options Panel", typeof(RectTransform), typeof(Image));
            RectTransform panel = (RectTransform)panelObject.transform;
            panel.SetParent(canvas.transform, false);
            panelObject.GetComponent<Image>().color = new Color(.08f, .13f, .16f, .98f);
            Text heading = CloneText(label, panel, "Громкость", new Vector2(0, -16), new Vector2(230, 24));
            SetRect(heading.rectTransform, new Vector2(0, -16), new Vector2(230, 24));
            heading.fontSize = 14;
            GameObject toggleObject = new GameObject("Sound Enabled", typeof(RectTransform), typeof(Image), typeof(Toggle));
            RectTransform toggleRect = (RectTransform)toggleObject.transform;
            toggleRect.SetParent(panel, false);
            SetRect(toggleRect, new Vector2(0, -48), new Vector2(230, 44));
            Toggle toggle = toggleObject.GetComponent<Toggle>();
            toggle.targetGraphic = toggleObject.GetComponent<Image>();
            toggleObject.GetComponent<Image>().color = new Color(.18f, .3f, .32f);
            GameObject indicator = new GameObject("Enabled", typeof(RectTransform), typeof(Image));
            RectTransform indicatorRect = (RectTransform)indicator.transform;
            indicatorRect.SetParent(toggleRect, false);
            indicatorRect.anchorMin = new Vector2(.5f, .5f);
            indicatorRect.anchorMax = indicatorRect.anchorMin;
            indicatorRect.anchoredPosition = new Vector2(-100, 0);
            indicatorRect.sizeDelta = new Vector2(18, 18);
            indicator.GetComponent<Image>().color = new Color(.4f, .8f, .78f);
            toggle.graphic = indicator.GetComponent<Image>();
            Text toggleText = CloneText(label, toggleRect, "Звук включён", new Vector2(10, 0), new Vector2(180, 40));
            toggleText.fontSize = 13;
            Slider atmosphere = CreateSlider(panel, label, "Атмосфера", -100);
            Slider effects = CreateSlider(panel, label, "Эффекты", -150);
            SetObject(options, "_button", button);
            SetObject(options, "_panel", panel);
            SetObject(options, "_mute", toggle);
            SetObject(options, "_atmosphere", atmosphere);
            SetObject(options, "_effects", effects);
            SetObject(options, "_label", label);
            panelObject.SetActive(false);
        }

        private static Slider CreateSlider(RectTransform parent, Text template, string title, float y)
        {
            GameObject gameObject = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(Slider));
            gameObject.GetComponent<Image>().color = Color.clear;
            RectTransform rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            SetRect(rect, new Vector2(0, y), new Vector2(230, 44));
            Text label = CloneText(template, rect, title, new Vector2(-63, 0), new Vector2(100, 40));
            label.fontSize = 12;
            GameObject trackObject = new GameObject("Track", typeof(RectTransform), typeof(Image));
            RectTransform track = (RectTransform)trackObject.transform;
            track.SetParent(rect, false);
            track.anchorMin = new Vector2(.48f, .5f);
            track.anchorMax = new Vector2(.48f, .5f);
            track.pivot = new Vector2(0, .5f);
            track.sizeDelta = new Vector2(112, 10);
            trackObject.GetComponent<Image>().color = new Color(.24f, .35f, .38f);
            GameObject handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            RectTransform handle = (RectTransform)handleObject.transform;
            handle.SetParent(track, false);
            handle.sizeDelta = new Vector2(20, 34);
            handleObject.GetComponent<Image>().color = new Color(.4f, .8f, .78f);
            Slider slider = gameObject.GetComponent<Slider>();
            slider.handleRect = handle;
            slider.targetGraphic = handleObject.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
        }

        private static Text CloneText(Text template, RectTransform parent, string text, Vector2 position, Vector2 size)
        {
            Text label = UnityEngine.Object.Instantiate(template, parent);
            label.name = text;
            label.text = text;
            label.raycastTarget = false;
            label.alignment = TextAnchor.MiddleCenter;
            SetRect(label.rectTransform, position, size);
            label.rectTransform.anchorMin = new Vector2(.5f, .5f);
            label.rectTransform.anchorMax = label.rectTransform.anchorMin;
            return label;
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(.5f, 1);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
