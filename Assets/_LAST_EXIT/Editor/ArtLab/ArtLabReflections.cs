using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LastExit.Editor.ArtLab
{
    public static class ArtLabReflections
    {
        public static void Bake()
        {
            Scene scene = SceneManager.GetActiveScene();

            if (scene.path != ArtLabSceneBuilder.ScenePath)
            {
                throw new InvalidOperationException("Open the refined Art Lab scene first.");
            }

            GameObject look = scene.GetRootGameObjects().Single(root => root.activeSelf &&
                root.name.StartsWith(ArtLabSceneBuilder.LookPrefix));
            ReflectionProbe probe = look.GetComponentInChildren<ReflectionProbe>();

            if (probe == null)
            {
                GameObject instance = new GameObject("Baked Station Reflection", typeof(ReflectionProbe));
                instance.transform.SetParent(look.transform, false);
                instance.transform.localPosition = new Vector3(0, 3.2f, 1);
                probe = instance.GetComponent<ReflectionProbe>();
            }

            probe.mode = ReflectionProbeMode.Baked;
            probe.size = new Vector3(24, 12, 28);
            probe.boxProjection = true;
            probe.resolution = 256;
            probe.hdr = true;
            probe.intensity = 1;
            probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            string path = "Assets/_LAST_EXIT/ArtLab/Rendering/StationReflection_Readability_" + look.name + ".exr";

            if (Lightmapping.BakeReflectionProbe(probe, path) == false)
            {
                throw new InvalidOperationException("Unity could not bake the station reflection probe.");
            }

            AssetDatabase.ImportAsset(path);
            probe.bakedTexture = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
