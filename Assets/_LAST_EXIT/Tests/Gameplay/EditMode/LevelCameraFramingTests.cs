using LastExit.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LastExit.Tests.Gameplay
{
    public class LevelCameraFramingTests
    {
        [TestCase(360, 640)]
        [TestCase(844, 300)]
        [TestCase(1600, 900)]
        public void FitsAllBoardCornersInsideAreaBetweenHud(int width, int height)
        {
            GameObject cameraObject = new GameObject("Framing test", typeof(Camera), typeof(LevelCameraFraming));

            try
            {
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                camera.aspect = (float)width / height;
                camera.transform.rotation = Quaternion.Euler(65, 0, 0);
                LevelCameraFraming framing = cameraObject.GetComponent<LevelCameraFraming>();
                SerializedObject settings = new SerializedObject(framing);
                settings.FindProperty("_camera").objectReferenceValue = camera;
                settings.FindProperty("_basePosition").vector3Value = new Vector3(0, 25, -15);
                settings.ApplyModifiedPropertiesWithoutUndo();
                Rect available = width < height ? new Rect(.02f, .25f, .96f, .62f) :
                    new Rect(112f / width, 8f / height, 1 - 268f / width, 1 - 16f / height);
                Bounds board = new Bounds(new Vector3(0, 1, 4), new Vector3(18, 3, 22));
                framing.SetAvailableRegion(available, board);

                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = board.center + new Vector3(
                        (corner & 1) == 0 ? -board.extents.x : board.extents.x,
                        (corner & 2) == 0 ? -board.extents.y : board.extents.y,
                        (corner & 4) == 0 ? -board.extents.z : board.extents.z);
                    Vector3 viewport = camera.WorldToViewportPoint(point);
                    Assert.That(viewport.x, Is.InRange(available.xMin, available.xMax));
                    Assert.That(viewport.y, Is.InRange(available.yMin, available.yMax));
                }
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
