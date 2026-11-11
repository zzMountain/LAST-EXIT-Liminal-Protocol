using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LastExit.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LastExit.Editor.Gameplay
{
    // Authors the saved gameplay look through the Editor; never changes board geometry or level data.
    public static class StationArtDirection
    {
        private const string ApplyCommand = "LAST EXIT/Gameplay/Apply Station Art Direction";
        private const string NumericBadgesCommand = "LAST EXIT/Gameplay/Apply Numeric Bus Badges";
        private const string Folder = "Assets/_LAST_EXIT/Art/StationArtDirection";
        private const string BackupPath = "Assets/_LAST_EXIT/Scenes/StationL07_BeforeArtDirection.unity";
        private const string VolumePath = Folder + "/StationVolume.asset";

        [MenuItem(ApplyCommand)]
        public static void Apply()
        {
            Scene scene = RequireScene();

            if (File.Exists(BackupPath) == false)
            {
                if (EditorSceneManager.SaveScene(scene, BackupPath, true) == false)
                {
                    throw new IOException("Could not preserve the scene before station art authoring.");
                }
            }

            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            UseBalanced();
            AssignMaterials(scene);
            ConfigureLighting(scene);
            ConfigureVolume(scene);
            ConfigureBadge(scene);
            EditorSceneManager.MarkSceneDirty(scene);

            if (EditorSceneManager.SaveScene(scene) == false)
            {
                throw new IOException("Could not save the station art direction.");
            }

            AssetDatabase.SaveAssets();
            File.WriteAllText("TestResults/StationArtDirection.txt", Inspect());
        }

        public static void UseBalanced()
        {
            int index = Array.IndexOf(QualitySettings.names, "Balanced");

            if (index < 0)
            {
                throw new InvalidOperationException("The existing WebGL Balanced quality preset is required.");
            }

            QualitySettings.SetQualityLevel(index, true);
        }

        [MenuItem(NumericBadgesCommand)]
        public static void ApplyNumericBadges()
        {
            Scene scene = RequireScene();
            ConfigureNumericBadges(scene);
            EditorSceneManager.MarkSceneDirty(scene);

            if (EditorSceneManager.SaveScene(scene) == false)
            {
                throw new IOException("Could not save the numeric bus badges.");
            }
        }

        public static string Inspect()
        {
            Scene scene = SceneManager.GetActiveScene();
            StringBuilder report = new StringBuilder();
            report.AppendLine("Scene=" + scene.path + ";dirty=" + scene.isDirty);
            report.AppendLine("Quality=" + QualitySettings.names[QualitySettings.GetQualityLevel()]);
            report.AppendLine("Pipeline=" + AssetDatabase.GetAssetPath(GraphicsSettings.currentRenderPipeline));

            foreach (Camera camera in GetComponents<Camera>(scene))
            {
                report.AppendLine("Camera=" + camera.name + ";position=" + camera.transform.position +
                    ";rotation=" + camera.transform.eulerAngles + ";size=" + camera.orthographicSize);
            }

            foreach (Light light in GetComponents<Light>(scene))
            {
                report.AppendLine("Light=" + light.name + ";intensity=" + light.intensity + ";color=" + light.color);
            }

            foreach (Material material in GetComponents<Renderer>(scene).SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null).Distinct().OrderBy(material => material.name))
            {
                report.AppendLine("Material=" + material.name + ";path=" + AssetDatabase.GetAssetPath(material));
            }

            return report.ToString();
        }

        private static Scene RequireScene()
        {
            Scene scene = SceneManager.GetActiveScene();

            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != LevelSceneAuthoring.ScenePath)
            {
                throw new InvalidOperationException("Open StationL07 in Edit Mode before authoring its look.");
            }

            if (scene.isDirty)
            {
                throw new InvalidOperationException("Save authored scene changes before applying the art pass.");
            }

            return scene;
        }

        private static T[] GetComponents<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        }

        private static void AssignMaterials(Scene scene)
        {
            Dictionary<Material, Material> copies = new Dictionary<Material, Material>();

            foreach (Renderer renderer in GetComponents<Renderer>(scene))
            {
                Material[] slots = renderer.sharedMaterials;

                for (int index = 0; index < slots.Length; index++)
                {
                    Material source = slots[index];

                    if (source == null || source.shader.name != "Universal Render Pipeline/Lit" ||
                        source.name.StartsWith("Route_", StringComparison.Ordinal) || source.name.Contains("Emblem"))
                    {
                        continue;
                    }

                    if (copies.TryGetValue(source, out Material copy) == false)
                    {
                        string path = Folder + "/" + source.name + ".mat";
                        copy = AssetDatabase.LoadAssetAtPath<Material>(path);

                        if (copy == null)
                        {
                            copy = new Material(source) { name = source.name };
                            AssetDatabase.CreateAsset(copy, path);
                        }

                        ConfigureMaterial(copy);
                        copies.Add(source, copy);
                    }

                    slots[index] = copy;
                }

                renderer.sharedMaterials = slots;
            }
        }

        private static void ConfigureMaterial(Material material)
        {
            switch (material.name)
            {
                case "Floor":
                    SetSurface(material, "#343C42", 0, .3f);
                    material.SetTexture("_BaseMap", null);
                    material.SetFloat("_BumpScale", .1f);
                    break;
                case "Wall":
                    SetSurface(material, "#879095", 0, .25f);
                    material.SetFloat("_BumpScale", .12f);
                    break;
                case "Body":
                    SetSurface(material, "#9BA7A9", .05f, .38f);
                    break;
                case "PolishRoof":
                    SetSurface(material, "#75858B", .05f, .32f);
                    break;
                case "PolishEdge":
                    SetSurface(material, "#C3CFCD", .15f, .45f);
                    break;
                case "Steel":
                    SetSurface(material, "#899393", .65f, .45f);
                    material.SetFloat("_BumpScale", .25f);
                    break;
                case "Rubber":
                    material.SetFloat("_Smoothness", .18f);
                    material.SetFloat("_BumpScale", .15f);
                    break;
                case "WetFloor":
                    SetSurface(material, "#3C474E", 0, .68f);
                    break;
                case "Marking":
                    SetSurface(material, "#6B7B80", 0, .1f);
                    break;
                case "UxRoadMarking":
                    SetSurface(material, "#56676D", 0, .1f);
                    break;
                case "PolishEntry":
                    SetSurface(material, "#EEE0BB", 0, .25f);
                    material.SetColor("_EmissionColor", new Color(.5f, .4f, .23f));
                    material.EnableKeyword("_EMISSION");
                    break;
            }

            EditorUtility.SetDirty(material);
        }

        private static void SetSurface(Material material, string hex, float metallic, float smoothness)
        {
            ColorUtility.TryParseHtmlString(hex, out Color color);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
        }

        private static void ConfigureLighting(Scene scene)
        {
            RenderSettings.ambientEquatorColor = new Color(.30f, .33f, .36f);

            foreach (Light light in GetComponents<Light>(scene))
            {
                if (light.name == "Neutral Key")
                {
                    light.intensity = 1.65f;
                    light.color = new Color(.96f, .97f, 1);
                }
                else if (light.name == "Atmosphere Fill")
                {
                    light.intensity = .35f;
                    light.color = new Color(.78f, .84f, .90f);
                }
                else if (light.name.StartsWith("Ceiling Fill", StringComparison.Ordinal))
                {
                    light.intensity = 3;
                    light.color = new Color(.80f, .87f, .92f);
                }
                else if (light.name.StartsWith("Warm Utility Pool", StringComparison.Ordinal))
                {
                    light.intensity = 4.5f;
                }
            }
        }

        private static void ConfigureVolume(Scene scene)
        {
            foreach (Volume volume in GetComponents<Volume>(scene))
            {
                VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);

                if (profile == null)
                {
                    profile = ScriptableObject.CreateInstance<VolumeProfile>();
                    AssetDatabase.CreateAsset(profile, VolumePath);

                    foreach (VolumeComponent source in volume.sharedProfile.components)
                    {
                        VolumeComponent copy = Object.Instantiate(source);
                        profile.components.Add(copy);
                        AssetDatabase.AddObjectToAsset(copy, profile);
                    }
                }

                if (profile.TryGet(out ColorAdjustments adjustments))
                {
                    adjustments.postExposure.Override(.35f);
                    adjustments.contrast.Override(6);
                    adjustments.saturation.Override(0);
                }

                if (profile.TryGet(out Bloom bloom))
                {
                    bloom.active = false;
                }

                volume.sharedProfile = profile;
                EditorUtility.SetDirty(profile);
            }
        }

        private static void ConfigureBadge(Scene scene)
        {
            LevelCameraFraming framing = GetComponents<LevelCameraFraming>(scene).Single();
            SerializedObject cameraSettings = new SerializedObject(framing);
            cameraSettings.FindProperty("_landscapeSize").floatValue = 9.8f;
            cameraSettings.ApplyModifiedPropertiesWithoutUndo();
            ConfigureNumericBadges(scene);
        }

        private static void ConfigureNumericBadges(Scene scene)
        {
            LevelHud hud = GetComponents<LevelHud>(scene).Single();
            SerializedObject serialized = new SerializedObject(hud);
            GameObject template = (GameObject)serialized.FindProperty("_badgeTemplate").objectReferenceValue;
            RectTransform rect = (RectTransform)template.transform;
            rect.sizeDelta = new Vector2(80, 30);
            template.GetComponentInChildren<LastExit.ArtLab.Preview.RouteEmblemGraphic>(true)
                .gameObject.SetActive(false);
            template.transform.Find("Route Accent").gameObject.SetActive(false);
            template.transform.Find("Horizontal Selection").gameObject.SetActive(false);
            Text count = template.transform.Find("Occupancy").GetComponent<Text>();
            count.fontSize = 24;
            count.rectTransform.sizeDelta = new Vector2(76, 28);
            count.rectTransform.anchoredPosition = Vector2.zero;
        }
    }
}
