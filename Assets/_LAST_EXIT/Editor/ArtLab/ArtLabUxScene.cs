using System;
using System.Collections.Generic;
using System.Linq;
using LastExit.ArtLab.Preview;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LastExit.Editor.ArtLab
{
    public static class ArtLabUxScene
    {
        public const string ScenePath = "Assets/_LAST_EXIT/Scenes/ArtLab/StationL07_ArtLab_Polish.unity";
        public const float FloorFront = -16.8f;
        public const float LandscapeSize = 15.3f;
        public const float PortraitSize = 18.6f;
        private const string BuildCommand = "LAST EXIT/Art Lab/Build UX Preview";
        private const string GenerateCopyCommand = "LAST EXIT/Art Lab/Generate UX Copy";
        private const float RowSpacing = 3.7f;

        public static bool IsPreviewScene(string path)
        {
            return path.StartsWith("Assets/_LAST_EXIT/Scenes/ArtLab/StationL07_ArtLab_Polish", StringComparison.Ordinal) &&
                path.EndsWith(".unity", StringComparison.Ordinal);
        }

        [MenuItem(BuildCommand)]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                throw new InvalidOperationException("The editable Polish scene already exists. Open it directly or use Generate UX Copy; rebuilding must not overwrite Inspector edits.");
            }

            BuildAtPath(ScenePath);
        }

        [MenuItem(GenerateCopyCommand)]
        public static void GenerateCopy()
        {
            BuildAtPath(AssetDatabase.GenerateUniqueAssetPath(ScenePath));
        }

        private static void BuildAtPath(string outputPath)
        {
            EnsureSavedScenes();
            Scene scene = EditorSceneManager.OpenScene(ArtLabSceneBuilder.ScenePath, OpenSceneMode.Single);
            EditorSceneManager.SaveScene(scene, outputPath);

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name.StartsWith(ArtLabSceneBuilder.LookPrefix) && root.activeSelf == false)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }

            Transform look = scene.GetRootGameObjects().Single(root => root.activeSelf &&
                root.name.StartsWith(ArtLabSceneBuilder.LookPrefix)).transform;
            Dictionary<string, Material> materials = LoadMaterials();
            ExtendStation(look);
            Transform[] buses = BuildBuses(look, materials);
            ArtLabVehicleDetail.Build(buses, materials);

            foreach (Transform passenger in look.Cast<Transform>().Where(child => child.name.StartsWith("Passenger_")))
            {
                passenger.localScale *= 1.18f;
            }

            BuildRoads(look, materials);
            Camera camera = ArtLabPreview.GetCamera();
            camera.transform.position = new Vector3(0, 36, -16);
            camera.transform.LookAt(new Vector3(0, .5f, -1.8f));
            camera.orthographicSize = LandscapeSize;
            camera.backgroundColor = new Color(.10f, .14f, .17f);
            camera.gameObject.AddComponent<PreviewCameraFraming>().Initialize(LandscapeSize, PortraitSize);
            ArtLabUxOverlay.Build(camera, look, buses, new[] { "R", "T", "B", "G", "T" },
                new[] { 4, 2, 6, 2, 4 });
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = camera.gameObject;
        }

        private static void EnsureSavedScenes()
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                if (SceneManager.GetSceneAt(index).isDirty)
                {
                    throw new InvalidOperationException("Save the modified scene before building the UX preview.");
                }
            }
        }

        private static Dictionary<string, Material> LoadMaterials()
        {
            Dictionary<string, Material> result = new Dictionary<string, Material>();
            string folder = ArtLabImporter.MaterialFolder + "/ColdLiminal/";

            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { folder.TrimEnd('/') }))
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                result[material.name] = material;
            }

            return result;
        }

        private static void ExtendStation(Transform look)
        {
            Transform oldWayfinding = look.Find("Board Wayfinding");
            UnityEngine.Object.DestroyImmediate(oldWayfinding.gameObject);
            Transform floor = look.Find("Floor");
            float length = ArtLabLayout.FloorBackZ - FloorFront;
            floor.localPosition = new Vector3(0, -.16f, (ArtLabLayout.FloorBackZ + FloorFront) * .5f);
            floor.localScale = new Vector3(20, .3f, length);

            foreach (Transform item in look.GetComponentsInChildren<Transform>(true))
            {
                if (item.name.StartsWith("Concrete Slab "))
                {
                    float fraction = Mathf.InverseLerp(ArtLabLayout.FloorFrontZ, ArtLabLayout.FloorBackZ,
                        item.position.z);
                    item.position = new Vector3(item.position.x, item.position.y,
                        Mathf.Lerp(FloorFront, ArtLabLayout.FloorBackZ, fraction));
                    item.localScale = new Vector3(item.localScale.x, item.localScale.y, length / 8 - .03f);
                }

                if ((item.name.Contains("Kerb") || item.name.StartsWith("Drain Cover")) && item.position.z < 0)
                {
                    item.position += Vector3.back * 7.2f;
                }

                bool servicePart = item.parent != null && item.parent.name == "Station Service Equipment" &&
                    (item.name.StartsWith("CCTV") || item.name == "LABEL_CCTV" ||
                        item.name == "Operator Console Visual Only");
                bool serviceRoot = item.parent == look && item.name.StartsWith("CCTV");

                if (servicePart || serviceRoot || item.name == "CCTV Angled Board")
                {
                    item.position += Vector3.back * 7.2f;
                }

                if (item.name.StartsWith("Corridor "))
                {
                    item.gameObject.SetActive(false);
                }
            }
        }

        private static Transform[] BuildBuses(Transform look, Dictionary<string, Material> materials)
        {
            Transform[] originals = look.Cast<Transform>().Where(child => child.name.StartsWith("LE_BUS_"))
                .OrderByDescending(child => child.position.z).ToArray();
            int[] sourceIndices = { 1, 0, 2, 0, 1 };
            int[] doorLanes = { 0, 1, 3, 2, 0 };
            string[] routes = { "R", "T", "B", "G", "T" };
            Transform[] result = new Transform[sourceIndices.Length];

            for (int row = 0; row < result.Length; row++)
            {
                GameObject instance = UnityEngine.Object.Instantiate(originals[sourceIndices[row]].gameObject,
                    look, true);
                instance.name = originals[sourceIndices[row]].name.Substring(0,
                    originals[sourceIndices[row]].name.LastIndexOf('_')) + "_" + routes[row] + "_R" + (row + 1);
                Transform door = instance.GetComponentsInChildren<Transform>().Single(child =>
                    child.name.EndsWith("DOOR_ANCHOR"));
                instance.transform.position += new Vector3(ArtLabLayout.LaneX[doorLanes[row]] - door.position.x,
                    0, 2 - row * RowSpacing - instance.transform.position.z);
                UnityEngine.Object.DestroyImmediate(instance.transform.Find("Door Presentation").gameObject);
                UnityEngine.Object.DestroyImmediate(instance.transform.Find("Boarding Threshold").gameObject);
                ArtLabMaterials.Apply(instance, materials, routes[row]);
                result[row] = instance.transform;
            }

            foreach (Transform original in originals)
            {
                UnityEngine.Object.DestroyImmediate(original.gameObject);
            }

            return result;
        }

        private static void BuildRoads(Transform look, Dictionary<string, Material> materials)
        {
            GameObject roads = new GameObject("UX Road Markings");
            roads.transform.SetParent(look, false);
            const string MaterialPath = "Assets/_LAST_EXIT/ArtLab/Rendering/UxRoadMarking.mat";
            Material marking = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

            if (marking == null)
            {
                marking = new Material(materials["Marking"]);
                marking.SetColor("_BaseColor", new Color(.35f, .44f, .47f));
                AssetDatabase.CreateAsset(marking, MaterialPath);
            }

            for (int row = 0; row < 5; row++)
            {
                float z = 2 - row * RowSpacing;

                for (int edge = 0; edge < 2; edge++)
                {
                    GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    line.name = "Road R" + (row + 1) + " Edge " + edge;
                    line.transform.SetParent(roads.transform, false);
                    line.transform.localPosition = new Vector3(0, .038f, z + (edge == 0 ? -1.4f : 1.4f));
                    line.transform.localScale = new Vector3(19.4f, .018f, .035f);
                    line.GetComponent<Renderer>().sharedMaterial = marking;
                    line.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                    UnityEngine.Object.DestroyImmediate(line.GetComponent<Collider>());
                }
            }
        }
    }
}
