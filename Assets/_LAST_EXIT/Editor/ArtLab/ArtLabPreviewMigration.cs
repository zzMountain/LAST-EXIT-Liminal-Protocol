using System;
using System.Linq;
using LastExit.ArtLab.Preview;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastExit.Editor.ArtLab
{
    public static class ArtLabPreviewMigration
    {
        public static void Run()
        {
            Scene scene = SceneManager.GetActiveScene();

            if (scene.path != ArtLabUxScene.ScenePath || scene.isDirty)
            {
                throw new InvalidOperationException("Open the saved canonical UX scene before migration.");
            }

            ArtLabPreviewHud hud = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<ArtLabPreviewHud>()).Single();

            if (hud.TryGetComponent<PreviewComposition>(out _))
            {
                throw new InvalidOperationException("Preview composition already exists; migration is one-time.");
            }

            string snapshot = AssetDatabase.GenerateUniqueAssetPath(
                "Assets/_LAST_EXIT/Scenes/ArtLab/StationL07_BeforeReviewFixes.unity");

            if (AssetDatabase.CopyAsset(scene.path, snapshot) == false)
            {
                throw new InvalidOperationException("Could not preserve the pre-migration scene.");
            }

            PreviewBus[] buses = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<PreviewBus>())
                .OrderByDescending(bus => bus.transform.position.z).ToArray();
            PreviewBusBadge[] badges = hud.GetComponentsInChildren<PreviewBusBadge>(true);
            PreviewLaneGuide[] lanes = hud.GetComponentsInChildren<PreviewLaneGuide>(true);

            if (buses.Length != 5 || badges.Length != 5 || lanes.Length != 4)
            {
                throw new InvalidOperationException("Expected five authored buses/badges and four lanes.");
            }

            Camera camera = hud.GetComponent<Canvas>().worldCamera;
            PreviewComposition composition = Undo.AddComponent<PreviewComposition>(hud.gameObject);
            composition.Initialize(camera, hud.GetComponent<PreviewPointerInput>(),
                new[] { (RectTransform)hud.transform.Find("Header"), (RectTransform)hud.transform.Find("Footer") },
                hud.GetComponent<PreviewBusDrag>(), buses, badges, lanes, hud,
                hud.GetComponent<PreviewHudLayout>(), camera.GetComponent<PreviewCameraFraming>());
            EditorUtility.SetDirty(composition);
            hud.RefreshLayout();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
