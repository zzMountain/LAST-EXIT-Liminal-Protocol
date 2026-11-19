using System.Collections.Generic;
using LastExit.ArtLab.Preview;
using LastExit.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace LastExit.Presentation
{
    // One camera frames the whole inspection line, even when some queues are empty.
    internal sealed class CctvViewfinder
    {
        private const int TickCount = 4;
        private const float LanePadding = .65f;
        private readonly RectTransform _frame;
        private readonly RectTransform _meter;
        private readonly RectTransform[] _edges = new RectTransform[4];
        private readonly Image[] _ticks = new Image[TickCount];

        public CctvViewfinder(RectTransform canvas)
        {
            _frame = (RectTransform)new GameObject("CCTV viewfinder", typeof(RectTransform)).transform;
            _frame.SetParent(canvas, false);
            _frame.gameObject.SetActive(false);

            for (int index = 0; index < _edges.Length; index++)
            {
                _edges[index] = CreateImage("Border", _frame, TerminalUiTheme.Accent).rectTransform;
            }

            _meter = CreateImage("CCTV countdown", _frame, TerminalUiTheme.Background).rectTransform;
            _meter.anchorMin = _meter.anchorMax = _meter.pivot = new Vector2(1, .5f);

            for (int index = 0; index < _ticks.Length; index++)
            {
                _ticks[index] = CreateImage("Second " + (index + 1), _meter, TerminalUiTheme.Text);
            }
        }

        public void Show(float remaining)
        {
            _frame.gameObject.SetActive(remaining > 0);

            if (remaining <= 0)
            {
                return;
            }

            int seconds = Mathf.CeilToInt(remaining);

            for (int index = 0; index < _ticks.Length; index++)
            {
                _ticks[index].color = index < seconds ? TerminalUiTheme.Text : TerminalUiTheme.Border;
            }
        }

        public void Project(Camera camera, RectTransform canvas, ILevelInspectionBoard board,
            IReadOnlyList<QueueSnapshot> queues, float scale)
        {
            if (_frame.gameObject.activeSelf == false || queues.Count == 0)
            {
                return;
            }

            int first = queues[0].X;
            int last = first;

            for (int index = 1; index < queues.Count; index++)
            {
                first = Mathf.Min(first, queues[index].X);
                last = Mathf.Max(last, queues[index].X);
            }

            Vector2 low = PreviewScreenProjection.Project(camera, canvas,
                new Vector3(board.GetWorldX(first - LanePadding), 0, board.QueueHeadZ - .35f));
            Vector2 high = PreviewScreenProjection.Project(camera, canvas,
                new Vector3(board.GetWorldX(last + LanePadding), 1.8f, board.QueueHeadZ + .35f));
            Vector2 size = high - low;
            _frame.anchoredPosition = (low + high) * .5f;
            _frame.sizeDelta = size;
            float line = 1.5f / scale;
            Vector2 horizontal = new Vector2(size.x, line);
            Vector2 vertical = new Vector2(line, size.y);
            SetRect(_edges[0], new Vector2(0, size.y * .5f), horizontal);
            SetRect(_edges[1], new Vector2(0, -size.y * .5f), horizontal);
            SetRect(_edges[2], new Vector2(-size.x * .5f, 0), vertical);
            SetRect(_edges[3], new Vector2(size.x * .5f, 0), vertical);

            float meterWidth = 37 / scale;
            float availableRight = canvas.rect.xMax - high.x - 3 / scale;
            SetRect(_meter, new Vector2(Mathf.Clamp(availableRight, 0, meterWidth), 0),
                new Vector2(meterWidth, 11 / scale));

            for (int index = 0; index < TickCount; index++)
            {
                SetRect(_ticks[index].rectTransform, new Vector2((index - 1.5f) * 8 / scale, 0),
                    new Vector2(6, 4) / scale);
            }
        }

        private static Image CreateImage(string name, RectTransform parent, Color color)
        {
            Image image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(parent, false);
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
