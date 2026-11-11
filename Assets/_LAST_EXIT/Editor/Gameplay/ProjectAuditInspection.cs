using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastExit.Editor.Gameplay
{
    // Read-only inspection: additive scenes are closed without saving and the user's setup is restored.
    public static class ProjectAuditInspection
    {
        private const string InspectCommand = "LAST EXIT/Gameplay/Inspect Full Project Bindings";
        private const string ReportPath = "TestResults/FullAudit-SerializedState.txt";
        private const string ProjectAssets = "Assets/_LAST_EXIT/";

        [MenuItem(InspectCommand)]
        public static void Inspect()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Exit Play mode before inspecting serialized project state.");
            }

            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                if (SceneManager.GetSceneAt(index).isDirty)
                {
                    throw new InvalidOperationException("Save modified scenes before inspecting project bindings.");
                }
            }

            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            StringBuilder report = new StringBuilder();
            report.AppendLine("Unity=" + Application.unityVersion);
            report.AppendLine("Target=" + EditorUserBuildSettings.activeBuildTarget);
            report.AppendLine("Template=" + PlayerSettings.WebGL.template);
            report.AppendLine("BuildScenes=" + string.Join(",", EditorBuildSettings.scenes
                .Where(scene => scene.enabled).Select(scene => scene.path)));
            int issues = 0;

            try
            {
                foreach (string path in AssetDatabase.GetAllAssetPaths().Where(path =>
                    path.StartsWith(ProjectAssets, StringComparison.Ordinal) &&
                    (path.EndsWith(".unity", StringComparison.Ordinal) ||
                        path.EndsWith(".prefab", StringComparison.Ordinal))).OrderBy(path => path))
                {
                    if (path.EndsWith(".unity", StringComparison.Ordinal))
                    {
                        Scene scene = SceneManager.GetSceneByPath(path);
                        bool opened = scene.IsValid() == false || scene.isLoaded == false;

                        if (opened)
                        {
                            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                        }

                        try
                        {
                            issues += InspectObjects(path, scene.GetRootGameObjects(), report);
                        }
                        finally
                        {
                            if (opened)
                            {
                                EditorSceneManager.CloseScene(scene, true);
                            }
                        }
                    }
                    else
                    {
                        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        issues += InspectObjects(path, new[] { prefab }, report);
                    }
                }
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }

            report.AppendLine("MissingScriptsOrReferences=" + issues);
            Directory.CreateDirectory("TestResults");
            File.WriteAllText(ReportPath, report.ToString());

            if (issues > 0)
            {
                throw new InvalidOperationException("Serialized project inspection found issues; see " + ReportPath);
            }
        }

        private static int InspectObjects(string path, IEnumerable<GameObject> roots, StringBuilder report)
        {
            int issues = 0;
            Dictionary<string, int> componentCounts = new Dictionary<string, int>();

            foreach (Transform transform in roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true)))
            {
                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                issues += missing;

                if (missing > 0)
                {
                    report.AppendLine("MISSING_SCRIPT=" + path + ":" + transform.name + ":" + missing);
                }

                foreach (MonoBehaviour behaviour in transform.GetComponents<MonoBehaviour>())
                {
                    if (behaviour == null)
                    {
                        continue;
                    }

                    string name = behaviour.GetType().FullName;
                    componentCounts.TryGetValue(name, out int count);
                    componentCounts[name] = count + 1;
                    SerializedObject serialized = new SerializedObject(behaviour);
                    SerializedProperty property = serialized.GetIterator();

                    while (property.Next(true))
                    {
                        if (property.propertyType == SerializedPropertyType.ObjectReference &&
                            property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                        {
                            issues++;
                            report.AppendLine("MISSING_REFERENCE=" + path + ":" + transform.name + ":" +
                                property.propertyPath);
                        }
                    }
                }
            }

            report.AppendLine("Asset=" + path);

            foreach (KeyValuePair<string, int> entry in componentCounts.OrderBy(entry => entry.Key))
            {
                report.AppendLine("  " + entry.Key + "=" + entry.Value);
            }

            return issues;
        }
    }
}
