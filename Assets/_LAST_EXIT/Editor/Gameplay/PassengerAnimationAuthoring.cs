using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LastExit.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastExit.Editor.Gameplay
{
    // A gameplay-only controller reuses the existing Generic clips without changing source art or Polish.
    public static class PassengerAnimationAuthoring
    {
        public const string ControllerPath = "Assets/_LAST_EXIT/Art/BlenderKit/Animations/PassengerGameplay.controller";
        private const string ApplyPaceCommand = "LAST EXIT/Gameplay/Apply Faster Passenger Pace";
        private const string ApplyBoardingCommand = "LAST EXIT/Gameplay/Apply Boarding Jump And Faster Pace";
        private const string ApplyCommand = "LAST EXIT/Gameplay/Apply Passenger Walk Controller";
        private const string ModelPath = "Assets/_LAST_EXIT/Art/BlenderKit/Models/PASSENGER_BASE_v01.fbx";
        private const string IdleClipName = "LE_Passenger_Idle_PROTOTYPE";
        private const string WalkClipName = "LE_Passenger_Walk_PROTOTYPE";
        private const string RateParameter = "WalkRate";
        private const string PulseMaterialPath = "Assets/_LAST_EXIT/Art/BlenderKit/Animations/BoardingPulse.mat";

        [MenuItem(ApplyCommand)]
        public static void Apply()
        {
            EnsureSavedScenes();
            string sourceHash = GetHash(LevelSceneAuthoring.SourcePath);
            AnimationClip[] clips = LoadClips();
            AnimationClip idle = clips.Single(clip => clip.name == IdleClipName);
            AnimationClip walk = clips.Single(clip => clip.name == WalkClipName);

            if (walk.isLooping == false)
            {
                throw new InvalidOperationException("The imported Walk clip must loop; no model importer was changed.");
            }

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            AnimatorControllerParameter parameter = controller.parameters.SingleOrDefault(item =>
                item.name == RateParameter);

            if (parameter == null)
            {
                parameter = new AnimatorControllerParameter
                {
                    name = RateParameter,
                    type = AnimatorControllerParameterType.Float,
                    defaultFloat = 1
                };
                controller.AddParameter(parameter);
            }
            else if (parameter.type != AnimatorControllerParameterType.Float)
            {
                throw new InvalidOperationException("PassengerGameplay WalkRate must be a Float parameter.");
            }

            AnimatorControllerParameter[] parameters = controller.parameters;

            foreach (AnimatorControllerParameter item in parameters)
            {
                if (item.name == RateParameter)
                {
                    item.defaultFloat = 1;
                }
            }

            controller.parameters = parameters;
            AnimatorControllerLayer[] layers = controller.layers;
            AnimatorStateMachine machine = layers[0].stateMachine;
            AnimatorState idleState = GetOrCreateState(machine, "Idle", new Vector3(260, 80, 0));
            AnimatorState walkState = GetOrCreateState(machine, "Walk", new Vector3(260, 180, 0));
            idleState.motion = idle;
            idleState.speed = 1;
            idleState.speedParameterActive = false;
            walkState.motion = walk;
            walkState.speed = 1;
            walkState.speedParameter = RateParameter;
            walkState.speedParameterActive = true;
            machine.defaultState = idleState;
            layers[0].defaultWeight = 1;
            controller.layers = layers;
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(idleState);
            EditorUtility.SetDirty(walkState);

            Scene scene = SceneManager.GetSceneByPath(LevelSceneAuthoring.ScenePath);

            if (scene.isLoaded == false)
            {
                scene = EditorSceneManager.OpenScene(LevelSceneAuthoring.ScenePath, OpenSceneMode.Single);
            }

            LevelBoardView board = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LevelBoardView>(true)).Single();
            SerializedObject serialized = new SerializedObject(board);
            SerializedProperty reference = serialized.FindProperty("_passengerController");

            if (reference == null)
            {
                throw new InvalidOperationException("Compile LevelBoardView._passengerController before applying Walk.");
            }

            reference.objectReferenceValue = controller;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            GameObject template = (GameObject)serialized.FindProperty("_passengerTemplate").objectReferenceValue;

            if (template.GetComponent<PassengerLocomotion>() == null)
            {
                template.AddComponent<PassengerLocomotion>();
            }

            EditorSceneManager.MarkSceneDirty(scene);

            if (EditorSceneManager.SaveScene(scene) == false)
            {
                throw new InvalidOperationException("Could not save the gameplay passenger controller reference.");
            }

            AssetDatabase.SaveAssets();

            if (GetHash(LevelSceneAuthoring.SourcePath) != sourceHash)
            {
                throw new InvalidOperationException("Saved Polish changed during gameplay Walk authoring.");
            }

            Debug.Log("PassengerGameplay ready: Idle/Walk; WalkRate=1; source clips and saved Polish unchanged.\n" +
                Inspect());
        }

        [MenuItem(ApplyPaceCommand)]
        public static void ApplyPace()
        {
            EnsureSavedScenes();
            string sourceHash = GetHash(LevelSceneAuthoring.SourcePath);
            Scene scene = SceneManager.GetSceneByPath(LevelSceneAuthoring.ScenePath);

            if (scene.isLoaded == false)
            {
                scene = EditorSceneManager.OpenScene(LevelSceneAuthoring.ScenePath, OpenSceneMode.Single);
            }

            LevelBoardView board = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LevelBoardView>(true)).Single();
            SerializedObject boardSettings = new SerializedObject(board);
            GameObject template = (GameObject)boardSettings.FindProperty("_passengerTemplate").objectReferenceValue;

            if (template.TryGetComponent(out PassengerLocomotion locomotion) == false)
            {
                throw new InvalidOperationException("The gameplay passenger template needs PassengerLocomotion.");
            }

            SerializedObject settings = new SerializedObject(locomotion);
            LevelSequencePlayer sequence = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LevelSequencePlayer>(true)).Single();
            SerializedObject sequenceSettings = new SerializedObject(sequence);
            settings.FindProperty("_walkingSpeed").floatValue = 6.5f;
            settings.FindProperty("_acceleration").floatValue = 32;
            sequenceSettings.FindProperty("_departureSeconds").floatValue = .65f;
            sequenceSettings.FindProperty("_arrivalSeconds").floatValue = .5f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            sequenceSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);

            if (EditorSceneManager.SaveScene(scene) == false)
            {
                throw new InvalidOperationException("Could not save the gameplay passenger pace.");
            }

            if (GetHash(LevelSceneAuthoring.SourcePath) != sourceHash)
            {
                throw new InvalidOperationException("Saved Polish changed during passenger pace authoring.");
            }

            Debug.Log("Pace saved: walking speed 6.5, acceleration 32; departure 0.65s, arrival 0.5s.");
        }

        [MenuItem(ApplyBoardingCommand)]
        public static void ApplyBoarding()
        {
            ApplyPace();
            string sourceHash = GetHash(LevelSceneAuthoring.SourcePath);
            Scene scene = SceneManager.GetSceneByPath(LevelSceneAuthoring.ScenePath);
            LevelBoardView board = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LevelBoardView>(true)).Single();
            SerializedObject boardData = new SerializedObject(board);
            GameObject template = (GameObject)boardData.FindProperty("_passengerTemplate").objectReferenceValue;

            if (template.TryGetComponent(out PassengerBoardingFeedback feedback) == false)
            {
                feedback = template.AddComponent<PassengerBoardingFeedback>();
            }

            Transform pulseRoot = template.transform.Find("Boarding Pulse");

            if (pulseRoot == null)
            {
                pulseRoot = new GameObject("Boarding Pulse", typeof(LineRenderer)).transform;
                pulseRoot.SetParent(template.transform, false);
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(PulseMaterialPath);

            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(material, PulseMaterialPath);
            }

            material.SetFloat("_Surface", 1);
            material.SetFloat("_Blend", 0);
            material.SetFloat("_ZWrite", 0);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetColor("_BaseColor", Color.white);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            LineRenderer pulse = pulseRoot.GetComponent<LineRenderer>();
            pulse.sharedMaterial = material;
            pulse.useWorldSpace = true;
            pulse.loop = true;
            pulse.positionCount = 24;
            pulse.widthMultiplier = 1;
            pulse.startWidth = .065f;
            pulse.endWidth = .065f;
            pulse.numCornerVertices = 2;
            pulse.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pulse.receiveShadows = false;
            pulse.enabled = false;
            SerializedObject settings = new SerializedObject(feedback);
            settings.FindProperty("_pulse").objectReferenceValue = pulse;
            settings.FindProperty("_seconds").floatValue = .24f;
            settings.FindProperty("_height").floatValue = .4f;
            settings.FindProperty("_radius").floatValue = .7f;
            settings.FindProperty("_pulseHeight").floatValue = 1.25f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);

            if (EditorSceneManager.SaveScene(scene) == false)
            {
                throw new InvalidOperationException("Could not save passenger boarding feedback.");
            }

            AssetDatabase.SaveAssets();

            if (GetHash(LevelSceneAuthoring.SourcePath) != sourceHash)
            {
                throw new InvalidOperationException("Saved Polish changed during boarding feedback authoring.");
            }

            string report = "BoardingJumpSeconds=0.24; Height=0.4; RingRadius=0.7; PulseHeight=1.25; " +
                "RingSegments=24; Material=" + PulseMaterialPath + "; PolishUnchanged=True\n" + Inspect();
            Directory.CreateDirectory("TestResults");
            File.WriteAllText("TestResults/Boarding-Authoring.txt", report);
            Debug.Log(report);
        }

        public static string Inspect()
        {
            StringBuilder report = new StringBuilder();
            ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            report.AppendLine("PassengerModel=" + ModelPath + "; Rig=" + importer?.animationType);

            foreach (AnimationClip clip in LoadClips())
            {
                EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
                report.AppendLine("Clip=" + clip.name + "; Seconds=" + clip.length.ToString("F6") +
                    "; FPS=" + clip.frameRate + "; Loop=" + clip.isLooping + "; Curves=" + bindings.Length);

                if (clip.name == WalkClipName)
                {
                    foreach (string path in bindings.Select(binding => binding.path).Distinct())
                    {
                        report.AppendLine("WalkBinding=" + path);
                    }
                }
            }

            Scene scene = SceneManager.GetSceneByPath(LevelSceneAuthoring.ScenePath);

            if (scene.isLoaded)
            {
                Transform template = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Single(item => item.name == "Shared Passenger Template");
                PassengerLocomotion locomotion = template.GetComponent<PassengerLocomotion>();
                SerializedObject pace = new SerializedObject(locomotion);
                report.AppendLine("WalkingSpeed=" + pace.FindProperty("_walkingSpeed").floatValue +
                    "; Acceleration=" + pace.FindProperty("_acceleration").floatValue);
                Animator animator = template.GetComponentInChildren<Animator>(true);
                SkinnedMeshRenderer mesh = template.GetComponentInChildren<SkinnedMeshRenderer>(true);
                report.AppendLine("TemplateScale=" + template.lossyScale.ToString("F4"));
                report.AppendLine("AnimatorPath=" + AnimationUtility.CalculateTransformPath(animator.transform, template));
                report.AppendLine("MeshPath=" + AnimationUtility.CalculateTransformPath(mesh.transform, template));
                report.AppendLine("MeshWorldRotation=" + mesh.transform.rotation.eulerAngles.ToString("F4"));
                report.AppendLine("VisualForwardFromSourceMinusY=" +
                    mesh.transform.TransformDirection(Vector3.down).normalized.ToString("F4"));
                report.AppendLine("BoundsSize=" + mesh.bounds.size.ToString("F4"));
                report.AppendLine("RootMotion=" + animator.applyRootMotion);
            }

            return report.ToString();
        }

        private static AnimatorState GetOrCreateState(AnimatorStateMachine machine, string name, Vector3 position)
        {
            AnimatorState state = machine.states.Select(child => child.state).SingleOrDefault(item => item.name == name);
            return state != null ? state : machine.AddState(name, position);
        }

        private static AnimationClip[] LoadClips()
        {
            return AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
                .Where(clip => clip.name.StartsWith("__preview__", StringComparison.Ordinal) == false).ToArray();
        }

        private static void EnsureSavedScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Exit Play mode before authoring the passenger controller.");
            }

            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                if (SceneManager.GetSceneAt(index).isDirty)
                {
                    throw new InvalidOperationException("Save modified scenes before authoring the passenger controller.");
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
