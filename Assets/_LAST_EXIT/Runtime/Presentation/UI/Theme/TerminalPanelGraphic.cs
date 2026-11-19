using UnityEngine;
using UnityEngine.UI;

namespace LastExit.Presentation
{
    // Draw only the occupied HUD groups, leaving the space between them open to the station.
    public class TerminalPanelGraphic : MaskableGraphic
    {
        private float _pixel = 1;
        private float _topHeight;
        private float _bottomHeight;

        public void SetSections(float scale, float topHeight, float bottomHeight)
        {
            float pixel = 1 / Mathf.Max(.001f, scale);

            if (Mathf.Approximately(_pixel, pixel) && Mathf.Approximately(_topHeight, topHeight) &&
                Mathf.Approximately(_bottomHeight, bottomHeight))
            {
                return;
            }

            _pixel = pixel;
            _topHeight = topHeight;
            _bottomHeight = bottomHeight;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            Rect bounds = rectTransform.rect;
            float top = Mathf.Min(bounds.height, _topHeight * _pixel);
            float bottom = Mathf.Min(bounds.height - top, _bottomHeight * _pixel);

            if (top > 0)
            {
                AddPanel(helper, new Rect(bounds.xMin, bounds.yMax - top, bounds.width, top));
            }

            if (bottom > 0)
            {
                AddPanel(helper, new Rect(bounds.xMin, bounds.yMin, bounds.width, bottom));
            }
        }

        private void AddPanel(VertexHelper helper, Rect bounds)
        {
            AddRectangle(helper, bounds, TerminalUiTheme.Panel);
            AddRectangle(helper, new Rect(bounds.xMin, bounds.yMin, bounds.width, _pixel), color);
            AddRectangle(helper, new Rect(bounds.xMin, bounds.yMax - _pixel, bounds.width, _pixel), color);
            AddRectangle(helper, new Rect(bounds.xMin, bounds.yMin, _pixel, bounds.height), color);
            AddRectangle(helper, new Rect(bounds.xMax - _pixel, bounds.yMin, _pixel, bounds.height), color);
        }

        private static void AddRectangle(VertexHelper helper, Rect rect, Color color)
        {
            int first = helper.currentVertCount;
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = new Vector3(rect.xMin, rect.yMin);
            helper.AddVert(vertex);
            vertex.position = new Vector3(rect.xMin, rect.yMax);
            helper.AddVert(vertex);
            vertex.position = new Vector3(rect.xMax, rect.yMax);
            helper.AddVert(vertex);
            vertex.position = new Vector3(rect.xMax, rect.yMin);
            helper.AddVert(vertex);
            helper.AddTriangle(first, first + 1, first + 2);
            helper.AddTriangle(first, first + 2, first + 3);
        }
    }
}
