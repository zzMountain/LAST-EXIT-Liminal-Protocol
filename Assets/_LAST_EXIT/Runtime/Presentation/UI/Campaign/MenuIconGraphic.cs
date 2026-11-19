using UnityEngine;
using UnityEngine.UI;

namespace LastExit.Presentation
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class MenuIconGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            Rect rect = GetPixelAdjustedRect();

            for (int line = 0; line < 3; line++)
            {
                float y = rect.center.y + (line - 1) * rect.height * .23f;
                float halfWidth = rect.width * .28f;
                float halfHeight = rect.height * .025f;
                int start = vertices.currentVertCount;
                vertices.AddVert(new Vector2(rect.center.x - halfWidth, y - halfHeight), color, Vector2.zero);
                vertices.AddVert(new Vector2(rect.center.x - halfWidth, y + halfHeight), color, Vector2.zero);
                vertices.AddVert(new Vector2(rect.center.x + halfWidth, y + halfHeight), color, Vector2.zero);
                vertices.AddVert(new Vector2(rect.center.x + halfWidth, y - halfHeight), color, Vector2.zero);
                vertices.AddTriangle(start, start + 1, start + 2);
                vertices.AddTriangle(start, start + 2, start + 3);
            }
        }
    }
}
