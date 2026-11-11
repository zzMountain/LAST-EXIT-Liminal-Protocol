using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace LastExit.Editor.ArtLab
{
    public static class ArtLabSceneBuilder
    {
        public const string ScenePath = "Assets/_LAST_EXIT/Scenes/ArtLab/StationL07_ArtLab_Readability.unity";
        public const string LookPrefix = "LOOK_";
        public const string CameraName = "Art Lab Camera";
        private const string BuildCommand = "LAST EXIT/Art Lab/Build Scene";

        public static void SaveSnapshot()
        {
            Scene scene = SceneManager.GetActiveScene();

            if (scene.path.StartsWith("Assets/_LAST_EXIT/Scenes/ArtLab/") == false)
            {
                throw new InvalidOperationException("Only Art Lab scenes can be snapshotted by this tool.");
            }

            string path = AssetDatabase.GenerateUniqueAssetPath(
                "Assets/_LAST_EXIT/Scenes/ArtLab/StationL07_BeforeReadability.unity");
            EditorSceneManager.SaveScene(scene, path);
        }

        [MenuItem(BuildCommand)]
        public static void Build()
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene existing = SceneManager.GetSceneAt(index);
                GameObject[] roots = existing.GetRootGameObjects();
                bool ownFailedDraft = string.IsNullOrEmpty(existing.path) && roots.Length > 0 &&
                    roots.All(root => root.name == CameraName || root.name.StartsWith(LookPrefix));

                if (existing.isDirty && ownFailedDraft == false)
                {
                    throw new InvalidOperationException("Save your modified scene before rebuilding Art Lab.");
                }
            }

            ArtLabImporter.Import();
            ArtLabImporter.CreatePrefabs();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            AssetDatabase.Refresh();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera camera = CreateCamera();
            AnimatorController passengerController = CreateController("PASSENGER_BASE", "PassengerPreview");
            AnimatorController doorController = CreateController("Q3_DOOR", "Q3Preview");

            foreach (ArtLabLook look in Enum.GetValues(typeof(ArtLabLook)))
            {
                Dictionary<string, Material> materials = ArtLabMaterials.Create(look);
                GameObject group = new GameObject(LookPrefix + look);
                BuildStation(group.transform, materials);
                ArtLabSetDressing.Build(group.transform, materials);
                BuildAssets(group.transform, materials, passengerController, doorController);
                ArtLabWayfinding.Build(group.transform, materials);
                ArtLabLabels.Build(group.transform);
                CreateLights(group.transform, look);
                group.SetActive(look == ArtLabLook.ColdLiminal);
            }

            ArtLabRendering.Configure(camera);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = camera.gameObject;
            SceneView sceneView = SceneView.lastActiveSceneView;

            if (sceneView != null)
            {
                sceneView.AlignViewToObject(camera.transform);
                sceneView.orthographic = true;
                sceneView.size = camera.orthographicSize;
            }

            ArtLabPreview.WriteSceneReport();
        }

        private static Camera CreateCamera()
        {
            GameObject instance = new GameObject(CameraName, typeof(Camera), typeof(AudioListener));
            instance.tag = "MainCamera";
            Camera camera = instance.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = ArtLabLayout.LandscapeSize;
            camera.transform.position = new Vector3(0, 29, -15);
            camera.transform.LookAt(new Vector3(0, .5f, 1));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.032f, .045f, .058f);
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 100;
            camera.allowHDR = true;
            camera.allowMSAA = true;
            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            return camera;
        }

        private static void BuildStation(Transform parent, Dictionary<string, Material> materials)
        {
            float floorLength = ArtLabLayout.FloorBackZ - ArtLabLayout.FloorFrontZ;
            CreateBox(parent, "Floor", new Vector3(0, -.16f,
                (ArtLabLayout.FloorBackZ + ArtLabLayout.FloorFrontZ) / 2),
                new Vector3(20, .3f, floorLength), materials["Floor"]);
            CreateBox(parent, "Back Wall", new Vector3(0, 1.8f, 11.7f), new Vector3(20, 3.6f, .4f), materials["Wall"]);
            CreateBox(parent, "Left Queue Wall", new Vector3(-9.8f, 1.5f, 8.9f),
                new Vector3(.4f, 3, 5.6f), materials["Wall"]);
            CreateBox(parent, "Right Queue Wall", new Vector3(9.8f, 1.5f, 8.9f),
                new Vector3(.4f, 3, 5.6f), materials["Wall"]);
            CreateBox(parent, "Back Skirting", new Vector3(0, .28f, 11.4f), new Vector3(20, .55f, .12f), materials["Dark"]);

            for (int index = 0; index < 5; index++)
            {
                float x = -8 + index * 4;
                CreateBox(parent, "Wall Pillar " + index, new Vector3(x, 2, 11.35f),
                    new Vector3(.26f, 4, .4f), materials["Steel"]);
            }

            for (int lane = 0; lane < 4; lane++)
            {
                float x = ArtLabLayout.LaneX[lane];
                CreateBox(parent, "Corridor " + (char)('A' + lane), new Vector3(x, .012f, 8.1f),
                    new Vector3(.05f, .02f, 5), materials["Marking"]);
            }

            for (int index = 0; index < 3; index++)
            {
                GameObject lamp = PlaceAsset(parent, "CEILING_LAMP", new Vector3(-6 + index * 6, 4.6f, 8.8f));
                ArtLabMaterials.Apply(lamp, materials, "R");
            }
        }

        private static void BuildAssets(Transform parent, Dictionary<string, Material> materials,
            AnimatorController passengerController, AnimatorController doorController)
        {
            string[] buses = { "LE_BUS_MINI", "LE_BUS_STANDARD", "LE_BUS_LONG" };
            string[] routes = { "R", "T", "B" };

            for (int index = 0; index < buses.Length; index++)
            {
                GameObject bus = PlaceAsset(parent, buses[index], Vector3.zero);
                bus.transform.rotation = Quaternion.Euler(0, 180, 0);
                Bounds bounds = ArtLabImporter.GetBounds(bus);
                Transform doorAnchor = bus.GetComponentsInChildren<Transform>(true).Single(child =>
                    child.name.EndsWith("DOOR_ANCHOR"));
                bus.transform.position += new Vector3(ArtLabLayout.LaneX[index] - doorAnchor.position.x,
                    -bounds.min.y, ArtLabLayout.RowZ[index]);
                bus.name = buses[index] + "_" + routes[index];
                ArtLabMaterials.Apply(bus, materials, routes[index]);
            }

            for (int lane = 0; lane < 4; lane++)
            {
                string route = ArtLabMaterials.Routes[lane];

                for (int person = 0; person < 3; person++)
                {
                    GameObject passenger = PlaceAsset(parent, "PASSENGER_BASE",
                        new Vector3(ArtLabLayout.LaneX[lane], 0,
                        ArtLabLayout.QueueHeadZ + person * ArtLabLayout.QueueSpacing));
                    passenger.name = "Passenger_" + route + "_" + person;
                    ArtLabMaterials.Apply(passenger, materials, route);
                    Animator animator = passenger.GetComponentInChildren<Animator>(true);

                    if (animator == null)
                    {
                        animator = passenger.transform.GetChild(0).gameObject.AddComponent<Animator>();
                    }

                    animator.runtimeAnimatorController = passengerController;
                    animator.applyRootMotion = false;
                }
            }

            for (int boundary = 0; boundary < 5; boundary++)
            {
                for (int section = 0; section < 2; section++)
                {
                    GameObject rail = PlaceAsset(parent, "QUEUE_RAILING", new Vector3(-4 + boundary * 2, 0,
                        ArtLabLayout.QueueHeadZ + .35f + section * 2.08f));
                    rail.transform.rotation = Quaternion.AngleAxis(90, Vector3.up) * rail.transform.rotation;
                    ArtLabMaterials.Apply(rail, materials, "R");
                }
            }

            GameObject terminal = PlaceAsset(parent, "CCTV_TERMINAL", ArtLabLayout.CctvPosition);
            ArtLabMaterials.Apply(terminal, materials, "R");
            GameObject door = PlaceAsset(parent, "Q3_DOOR", ArtLabLayout.Q3Position);
            ArtLabMaterials.Apply(door, materials, "R");
            Animator doorAnimator = door.GetComponentInChildren<Animator>(true);

            if (doorAnimator == null)
            {
                doorAnimator = door.transform.GetChild(0).gameObject.AddComponent<Animator>();
            }

            doorAnimator.runtimeAnimatorController = doorController;
            doorAnimator.applyRootMotion = false;
        }

        private static GameObject PlaceAsset(Transform parent, string name, Vector3 position)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArtLabImporter.PrefabFolder + "/" +
                name + ".prefab");
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            GameObject placement = new GameObject(name);
            placement.transform.SetParent(parent, false);
            placement.transform.localPosition = position;
            instance.transform.SetParent(placement.transform, false);
            instance.transform.localPosition = Vector3.zero;
            return placement;
        }

        private static void CreateBox(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = position;
            instance.transform.localScale = scale;
            instance.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(instance.GetComponent<Collider>());
        }

        private static void CreateLights(Transform parent, ArtLabLook look)
        {
            GameObject keyObject = new GameObject("Neutral Key", typeof(Light));
            keyObject.transform.SetParent(parent, false);
            keyObject.transform.rotation = Quaternion.Euler(52, -28, 0);
            Light key = keyObject.GetComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(.96f, .97f, 1);
            key.intensity = look == ArtLabLook.Neutral ? 1.7f : 1.8f;
            key.shadows = LightShadows.Soft;
            key.shadowBias = .03f;
            key.shadowNormalBias = .2f;
            GameObject fillObject = new GameObject("Atmosphere Fill", typeof(Light));
            fillObject.transform.SetParent(parent, false);
            fillObject.transform.rotation = Quaternion.Euler(28, 145, 0);
            Light fill = fillObject.GetComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = look == ArtLabLook.WarmIndustrial ? new Color(1, .8f, .57f) : new Color(.58f, .8f, 1);
            fill.intensity = look == ArtLabLook.Neutral ? .4f : .65f;
            fill.shadows = LightShadows.None;

            for (int index = 0; index < 3; index++)
            {
                GameObject localObject = new GameObject("Ceiling Fill " + index, typeof(Light));
                localObject.transform.SetParent(parent, false);
                localObject.transform.localPosition = new Vector3(-6 + index * 6, 3.5f, 8.8f);
                Light local = localObject.GetComponent<Light>();
                local.type = LightType.Point;
                local.color = look == ArtLabLook.WarmIndustrial ? new Color(1, .76f, .48f) :
                    look == ArtLabLook.ColdLiminal ? new Color(.52f, .8f, 1) : Color.white;
                local.intensity = 5;
                local.range = 9;
                local.shadows = LightShadows.None;
            }

            for (int row = 0; row < 3; row++)
            {
                GameObject lightObject = new GameObject("Warm Utility Pool " + row, typeof(Light));
                lightObject.transform.SetParent(parent, false);
                lightObject.transform.localPosition = new Vector3(-5.3f, 4.2f, ArtLabLayout.RowZ[row]);
                Light utility = lightObject.GetComponent<Light>();
                utility.type = LightType.Point;
                utility.color = new Color(1, .78f, .53f);
                utility.intensity = 7;
                utility.range = 9;
                utility.shadows = LightShadows.None;
            }

            CreateServiceLight(parent, "Q3 Task Light", ArtLabLayout.Q3Position + new Vector3(0, 2.5f, -1.7f),
                new Color(1, .7f, .58f));
            CreateServiceLight(parent, "CCTV Task Light", ArtLabLayout.CctvPosition + new Vector3(0, 2.7f, -1),
                new Color(.65f, .88f, 1));
        }

        private static void CreateServiceLight(Transform parent, string name, Vector3 position, Color color)
        {
            GameObject instance = new GameObject(name, typeof(Light));
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = position;
            Light light = instance.GetComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = 10;
            light.range = 5;
            light.shadows = LightShadows.None;
        }

        private static AnimatorController CreateController(string modelName, string name)
        {
            string folder = ArtLabImporter.Root + "/Animations";
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            string path = folder + "/" + name + ".controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);

            if (controller != null)
            {
                return controller;
            }

            controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(ArtLabImporter.Root + "/Models/" +
                modelName + "_v01.fbx").OfType<AnimationClip>().Where(clip =>
                    clip.name.StartsWith("__preview__") == false).ToArray();

            foreach (AnimationClip clip in clips)
            {
                AnimatorState state = controller.AddMotion(clip);

                if (clip.name.Contains("Idle"))
                {
                    controller.layers[0].stateMachine.defaultState = state;
                }
            }

            return controller;
        }
    }
}
