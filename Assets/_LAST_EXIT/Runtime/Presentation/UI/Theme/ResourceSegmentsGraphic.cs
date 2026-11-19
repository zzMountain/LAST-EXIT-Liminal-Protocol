using UnityEngine;
using UnityEngine.UI;

namespace LastExit.Presentation
{
    // Visual counts only. Lost seats are distinct from occupied seats and retain the numeric readout.
    [RequireComponent(typeof(CanvasRenderer))]
    public class ResourceSegmentsGraphic : MaskableGraphic
    {
        private const float GapFraction = .18f;
        private int _occupied;
        private int _lost;
        private int _capacity;

        public void SetCounts(int occupied, int lost, int capacity)
        {
            if (_occupied == occupied && _lost == lost && _capacity == capacity)
            {
                return;
            }

            _occupied = occupied;
            _lost = lost;
            _capacity = capacity;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();

            if (_capacity <= 0)
            {
                return;
            }

            Rect rect = GetPixelAdjustedRect();
            float step = rect.width / _capacity;

            for (int index = 0; index < _capacity; index++)
            {
                float left = rect.xMin + index * step;
                float right = left + step * (1 - GapFraction);
                bool lost = index >= _occupied && index < _occupied + _lost;
                Color segmentColor = index < _occupied ? new Color(.9f, .95f, .94f) :
                    lost ? new Color(1, .78f, .34f) : new Color(.26f, .4f, .39f);
                AddQuad(helper, new Vector2(left, rect.yMin), new Vector2(right, rect.yMax), segmentColor);

                if (lost)
                {
                    // A dark gap marks lost seats even without color perception.
                    float middle = (left + right) * .5f;
                    float halfWidth = (right - left) * .12f;
                    AddQuad(helper, new Vector2(middle - halfWidth, rect.yMin),
                        new Vector2(middle + halfWidth, rect.yMax), TerminalUiTheme.Background);
                }
            }
        }

        private static void AddQuad(VertexHelper helper, Vector2 minimum, Vector2 maximum, Color tint)
        {
            int first = helper.currentVertCount;
            helper.AddVert(new Vector3(minimum.x, minimum.y), tint, Vector2.zero);
            helper.AddVert(new Vector3(minimum.x, maximum.y), tint, Vector2.zero);
            helper.AddVert(new Vector3(maximum.x, maximum.y), tint, Vector2.zero);
            helper.AddVert(new Vector3(maximum.x, minimum.y), tint, Vector2.zero);
            helper.AddTriangle(first, first + 1, first + 2);
            helper.AddTriangle(first, first + 2, first + 3);
        }
    }
}
