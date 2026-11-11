using System;
using System.IO;
using System.Linq;
using LastExit.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastExit.Editor.Gameplay
{
    public static class MobileEchoAuthoring
    {
        private const string ApplyCommand = "LAST EXIT/Gameplay/Apply Mobile ECHO";
        private const string Backup = "Assets/_LAST_EXIT/Scenes/StationL07_BeforeMobileEcho.unity";
        private const string EffectMaterial = "Assets/_LAST_EXIT/Art/StationArtDirection/QuarantineParticles.mat";

        [MenuItem(ApplyCommand)]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Exit Play mode before authoring quarantine.");
            }

            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                if (SceneManager.GetSceneAt(index).isDirty)
                {
                    throw new InvalidOperationException("Save scene changes before applying Mobile ECHO bindings.");
                }
            }

            Scene scene = SceneManager.GetSceneByPath(LevelSceneAuthoring.ScenePath);

            if (scene.isLoaded == false)
            {
                scene = EditorSceneManager.OpenScene(LevelSceneAuthoring.ScenePath);
            }

            if (File.Exists(Backup) == false)
            {
                if (EditorSceneManager.SaveScene(scene, Backup, true) == false)
                {
                    throw new IOException("Could not preserve the scene before Mobile ECHO authoring.");
                }
            }

            Transform[] transforms = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            Transform leaf = transforms.Single(item => item.name == "LE_Q3_DOOR_PANEL");
            Transform door = leaf.GetComponentInParent<Animator>().transform;

            foreach (Animator animator in door.GetComponentsInChildren<Animator>(true))
            {
                animator.runtimeAnimatorController = null;
                animator.enabled = false;
            }

            leaf.localPosition = Vector3.zero;
            QuarantineChamber chamber = door.GetComponent<QuarantineChamber>();

            if (chamber == null)
            {
                chamber = door.gameObject.AddComponent<QuarantineChamber>();
            }

            Transform threshold = GetMarker(door, "Gameplay Threshold", new Vector3(0, 0, -1));
            Transform inside = GetMarker(door, "Gameplay Inside", new Vector3(0, 0, .45f));
            // Marker positions are world offsets: imported FBX has an axis conversion transform.
            Vector3 doorway = leaf.GetComponentsInChildren<Renderer>().Single().bounds.center;
            threshold.position = new Vector3(doorway.x, 0, doorway.z - 1);
            inside.position = new Vector3(doorway.x, 0, doorway.z + .45f);
            Light flash = GetMarker(door, "Gameplay Flash", Vector3.zero).GetComponent<Light>();

            if (flash == null)
            {
                flash = door.Find("Gameplay Flash").gameObject.AddComponent<Light>();
            }

            flash.transform.position = inside.position + Vector3.up;
            flash.type = LightType.Point;
            flash.color = new Color(1, .75f, .4f);
            flash.range = 4;
            flash.intensity = 0;
            flash.shadows = LightShadows.None;
            Material material = AssetDatabase.LoadAssetAtPath<Material>(EffectMaterial);

            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(material, EffectMaterial);
            }

            ParticleSystem sparks = GetParticles(door, "Gameplay Sparks", inside.position,
                new Color(1, .6f, .13f), 2.2f, .045f, material);
            ParticleSystem ash = GetParticles(door, "Gameplay Ash", inside.position,
                new Color(.22f, .24f, .25f), .45f, .095f, material);
            SerializedObject chamberData = new SerializedObject(chamber);
            SetReference(chamberData, "_leaf", leaf);
            SetReference(chamberData, "_threshold", threshold);
            SetReference(chamberData, "_inside", inside);
            SetReference(chamberData, "_flash", flash);
            SetReference(chamberData, "_sparks", sparks);
            SetReference(chamberData, "_ash", ash);
            chamberData.FindProperty("_closedPosition").vector3Value = Vector3.zero;
            chamberData.ApplyModifiedPropertiesWithoutUndo();
            LevelSequencePlayer sequence = transforms.Select(item => item.GetComponent<LevelSequencePlayer>())
                .Single(item => item != null);
            SerializedObject sequenceData = new SerializedObject(sequence);
            SetReference(sequenceData, "_chamber", chamber);
            sequenceData.ApplyModifiedPropertiesWithoutUndo();
            LevelBoardView board = transforms.Select(item => item.GetComponent<LevelBoardView>())
                .Single(item => item != null);
            SerializedObject boardData = new SerializedObject(board);
            boardData.FindProperty("_isolationPosition").vector3Value = threshold.position;
            boardData.ApplyModifiedPropertiesWithoutUndo();
            SyncLevelContent.Sync();
            EditorSceneManager.MarkSceneDirty(scene);

            if (EditorSceneManager.SaveScene(scene) == false)
            {
                throw new IOException("Could not save the Mobile ECHO bindings.");
            }

            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("TestResults");
            File.WriteAllText("TestResults/MobileEcho-Authoring.txt",
                "Scene=" + scene.path + ";Door=" + door.name + ";Leaf=" + leaf.name +
                ";Threshold=" + threshold.position + ";Inside=" + inside.position +
                ";PreviewControllers=0;QuarantineChambers=1");
        }

        private static void SetReference(SerializedObject target, string field, UnityEngine.Object value)
        {
            target.FindProperty(field).objectReferenceValue = value;
        }

        private static Transform GetMarker(Transform parent, string name, Vector3 position)
        {
            Transform marker = parent.Find(name);

            if (marker == null)
            {
                marker = new GameObject(name).transform;
                marker.SetParent(parent, false);
                marker.localPosition = position;
            }

            return marker;
        }

        private static ParticleSystem GetParticles(Transform parent, string name, Vector3 position,
            Color color, float speed, float size, Material material)
        {
            Transform marker = GetMarker(parent, name, Vector3.zero);
            marker.position = position + Vector3.up;
            ParticleSystem particles = marker.GetComponent<ParticleSystem>();

            if (particles == null)
            {
                particles = marker.gameObject.AddComponent<ParticleSystem>();
            }

            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = .6f;
            main.startLifetime = .6f;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = color;
            main.gravityModifier = .2f;
            main.maxParticles = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, 32) });
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = .2f;
            particles.useAutoRandomSeed = false;
            particles.randomSeed = 19;
            particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            return particles;
        }
    }
}
