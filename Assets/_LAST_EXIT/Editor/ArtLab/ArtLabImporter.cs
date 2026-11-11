using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LastExit.Editor.ArtLab
{
    public static class ArtLabImporter
    {
        public const string Root = "Assets/_LAST_EXIT/Art/BlenderKit";
        public const string PrefabFolder = Root + "/Prefabs_Refined";
        public const string MaterialFolder = Root + "/Materials_Refined";
        private const string ImportCommand = "LAST EXIT/Art Lab/Import Models";

        private static readonly string[] s_modelNames =
        {
            "BUS_BASE_LIBRARY", "PASSENGER_BASE", "QUEUE_RAILING", "CCTV_TERMINAL", "CEILING_LAMP", "Q3_DOOR"
        };

        public static string GetModelPath(string name)
        {
            return Root + "/Models/" + name + (name == "BUS_BASE_LIBRARY" ? "_v02.fbx" : "_v01.fbx");
        }

        [MenuItem(ImportCommand)]
        public static void Import()
        {
            AssetDatabase.Refresh();
            StringBuilder report = new StringBuilder();

            foreach (string name in s_modelNames)
            {
                string path = GetModelPath(name);
                ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.importCameras = false;
                importer.importLights = false;
                importer.importBlendShapes = false;
                importer.bakeAxisConversion = true;
                importer.meshCompression = ModelImporterMeshCompression.Off;
                importer.isReadable = false;
                importer.animationType = name == "PASSENGER_BASE" || name == "Q3_DOOR"
                    ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
                importer.importAnimation = name == "PASSENGER_BASE" || name == "Q3_DOOR";
                ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;

                foreach (ModelImporterClipAnimation clip in clips)
                {
                    clip.loopTime = clip.name.Contains("Idle") || clip.name.Contains("Walk");
                }

                importer.clipAnimations = clips;
                importer.SaveAndReimport();
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                report.AppendLine(name + ": wrapper=" + model.transform.localRotation.eulerAngles);

                foreach (Transform child in model.transform)
                {
                    report.AppendLine("  " + child.name + " position=" + child.localPosition +
                        " rotation=" + child.localRotation.eulerAngles + " scale=" + child.localScale);
                }

                foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    report.Append("  " + renderer.name + " bounds=" + renderer.bounds.size + " materials=");

                    foreach (Material material in renderer.sharedMaterials)
                    {
                        report.Append((material == null ? "NULL" : material.name) + ",");
                    }

                    report.AppendLine();
                }
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/Textures" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
                bool normal = path.Contains("_normal");
                importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = normal == false && path.Contains("metallic_smoothness") == false;
                importer.maxTextureSize = 512;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = path.Contains("emblem") ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.SaveAndReimport();
            }

            Directory.CreateDirectory("TestResults");
            File.WriteAllText("TestResults/ArtLab-import.txt", report.ToString());
        }

        public static void CreatePrefabs()
        {
            Directory.CreateDirectory(PrefabFolder);
            AssetDatabase.Refresh();
            Dictionary<string, Material> materials = ArtLabMaterials.Create(ArtLabLook.ColdLiminal);

            foreach (string name in s_modelNames)
            {
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(GetModelPath(name));

                if (name == "BUS_BASE_LIBRARY")
                {
                    foreach (Transform child in model.transform)
                    {
                        SavePrefab(child.gameObject, child.name, materials);
                    }
                }
                else
                {
                    SavePrefab(model, name, materials);
                }
            }
        }

        public static Bounds GetBounds(GameObject instance)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = renderers[0].bounds;

            foreach (Renderer renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }

            return bounds;
        }

        private static void SavePrefab(GameObject source, string name, Dictionary<string, Material> materials)
        {
            GameObject instance = UnityEngine.Object.Instantiate(source);
            instance.name = name;
            instance.transform.position = Vector3.zero;
            ArtLabMaterials.Apply(instance, materials, "R");
            PrefabUtility.SaveAsPrefabAsset(instance, PrefabFolder + "/" + name + ".prefab");
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }
}
