using UnityEngine;
using UnityEngine.UI;

namespace LastExit.Presentation
{
    // Vector geometry keeps the award crisp at both canvas scales without a font glyph or texture.
    [RequireComponent(typeof(CanvasRenderer))]
    public class ResultStarGraphic : MaskableGraphic
    {
        private const int PointCount = 10;
        private const float InnerRadius = .46f;
        private float _reveal;
        private float _flash;

        public void SetAppearance(float reveal, float flash)
        {
            _reveal = Mathf.Clamp01(reveal);
            _flash = Mathf.Clamp01(flash);
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            Rect rect = GetPixelAdjustedRect();
            float radius = Mathf.Min(rect.width, rect.height) * .42f;
            Color glow = new Color(1, .72f, .25f, _reveal * (.045f + _flash * .12f));
            AddStar(vertices, rect.center, radius * 1.18f, glow, glow);
            AddStar(vertices, rect.center, radius * 1.09f, glow, glow);
            Color rim = Color.Lerp(new Color(.27f, .38f, .4f), new Color(1, .88f, .56f), _reveal);
            AddStar(vertices, rect.center + Vector2.down * radius * .055f, radius, rim * .65f, rim * .65f);
            AddStar(vertices, rect.center, radius, rim, rim);
            Color top = Color.Lerp(new Color(.14f, .22f, .25f), new Color(1, .83f, .38f), _reveal);
            Color bottom = Color.Lerp(new Color(.08f, .14f, .17f), new Color(.85f, .42f, .09f), _reveal);
            AddStar(vertices, rect.center, radius * .89f, top, bottom);
        }

        private static Vector2 GetPoint(int index, float radius)
        {
            float angle = Mathf.PI * .5f + index * Mathf.PI * 2 / PointCount;
            float distance = index % 2 == 0 ? radius : radius * InnerRadius;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
        }

        private static void AddStar(VertexHelper vertices, Vector2 center, float radius, Color top, Color bottom)
        {
            int start = vertices.currentVertCount;
            vertices.AddVert(center, Color.Lerp(bottom, top, .6f), Vector2.zero);

            for (int index = 0; index < PointCount; index++)
            {
                Vector2 point = GetPoint(index, radius);
                Color shade = Color.Lerp(bottom, top, (point.y / radius + 1) * .5f);
                vertices.AddVert(center + point, shade, Vector2.zero);
            }

            for (int index = 0; index < PointCount; index++)
            {
                vertices.AddTriangle(start, start + 1 + (index + 1) % PointCount, start + 1 + index);
            }
        }
    }
}
