using UnityEngine;

namespace LastExit.ArtLab.Preview
{
    public static class PreviewScreenProjection
    {
        public static Vector2 Project(Camera camera, RectTransform canvas, Vector3 position)
        {
            Vector3 viewport = camera.WorldToViewportPoint(position);
            Rect rect = canvas.rect;
            return new Vector2((viewport.x - canvas.pivot.x) * rect.width,
                (viewport.y - canvas.pivot.y) * rect.height);
        }
    }
}
