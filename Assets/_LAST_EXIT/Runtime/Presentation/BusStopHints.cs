using System;
using System.Collections.Generic;
using LastExit.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace LastExit.Presentation
{
    // Screen-space affordances only: stop positions and feedback come from the same preview as a real drop.
    public class BusStopHints
    {
        private readonly RectTransform _root;
        private readonly Func<Vector3, Vector2> _project;
        private readonly ILevelBusGeometry _board;
        private readonly ILevelStopPreview _preview;
        private readonly List<Image> _stops = new List<Image>();
        private readonly List<bool> _corridorStops = new List<bool>();
        private readonly Image[] _edges = new Image[4];
        private readonly Image _door;
        private BusStopPreview _state;
        private BusSnapshot _bus;

        public BusStopHints(RectTransform overlay, Func<Vector3, Vector2> project,
            ILevelBusGeometry board, ILevelStopPreview preview)
        {
            _root = (RectTransform)new GameObject("Bus Stop Preview", typeof(RectTransform)).transform;
            _root.SetParent(overlay, false);
            _root.anchorMin = new Vector2(.5f, .5f);
            _root.anchorMax = _root.anchorMin;
            _root.SetAsFirstSibling();
            _project = project;
            _board = board;
            _preview = preview;

            for (int index = 0; index < _edges.Length; index++)
            {
                _edges[index] = CreateImage("Stop Outline " + index);
            }

            _door = CreateImage("Stop Door");
            Hide();
        }

        public string Message { get; private set; }

        public void Show(BusSnapshot bus, int leftX, int width, IReadOnlyDictionary<string, int> corridors)
        {
            _bus = bus;
            _state = _preview.GetStopPreview(bus.Id, leftX);
            _root.gameObject.SetActive(_state != null);

            if (_state == null)
            {
                Message = string.Empty;
                return;
            }

            int count = width - bus.FootprintCells + 1;

            while (_stops.Count < count)
            {
                _stops.Add(CreateImage("Door Stop " + _stops.Count));
            }

            for (int index = 0; index < _stops.Count; index++)
            {
                _stops[index].gameObject.SetActive(index < count);
            }

            _corridorStops.Clear();

            for (int index = 0; index < count; index++)
            {
                bool corridorStop = false;

                foreach (KeyValuePair<string, int> lane in corridors)
                {
                    corridorStop |= lane.Value == index + 1;
                }

                _corridorStops.Add(corridorStop);
            }

            Message = GetMessage(_state);
        }

        public void Hide()
        {
            _state = null;
            Message = string.Empty;
            _root.gameObject.SetActive(false);
        }

        public void Project(float canvasScale, float doorCalloutOffset)
        {
            if (_state == null)
            {
                return;
            }

            Bounds bounds = _board.GetBusBounds(_bus.Id);
            Vector3 actualDoor = _board.GetDoor(_bus.Id);
            Vector3 stopDoor = new Vector3(_board.GetWorldX(_state.LeftX + 1), .08f, bounds.max.z + .3f);
            Color color = _state.CanBoard ? new Color(.44f, 1, .65f) :
                _state.IsAllowed ? new Color(1, .78f, .34f) : new Color(1, .34f, .32f);

            for (int index = 0; index < _stops.Count; index++)
            {
                Vector3 position = stopDoor;
                position.x = _board.GetWorldX(index + 1);
                Image stop = _stops[index];
                stop.rectTransform.anchoredPosition = _project(position) + Vector2.up * doorCalloutOffset;
                bool selected = index == _state.LeftX;
                bool corridorStop = index < _corridorStops.Count && _corridorStops[index];
                float size = selected ? 12 : corridorStop ? 8 : 3;
                stop.rectTransform.sizeDelta = Vector2.one * size / canvasScale;
                stop.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
                stop.color = selected ? color : corridorStop ? new Color(.95f, .85f, .57f, .8f) :
                    new Color(.7f, .82f, .85f, .3f);
            }

            float deltaX = stopDoor.x - actualDoor.x;
            Vector2 minimum = _project(new Vector3(bounds.min.x + deltaX, .08f, bounds.min.z));
            Vector2 maximum = _project(new Vector3(bounds.max.x + deltaX, .08f, bounds.max.z));
            float thickness = 1.5f / canvasScale;
            SetEdge(0, new Vector2(minimum.x, minimum.y), new Vector2(maximum.x, minimum.y), thickness, color);
            SetEdge(1, new Vector2(maximum.x, minimum.y), new Vector2(maximum.x, maximum.y), thickness, color);
            SetEdge(2, new Vector2(maximum.x, maximum.y), new Vector2(minimum.x, maximum.y), thickness, color);
            SetEdge(3, new Vector2(minimum.x, maximum.y), new Vector2(minimum.x, minimum.y), thickness, color);
            _door.rectTransform.anchoredPosition = _project(stopDoor) + Vector2.up * doorCalloutOffset;
            _door.rectTransform.sizeDelta = new Vector2(28, 5) / canvasScale;
            _door.color = color;
        }

        private static string GetMessage(BusStopPreview state)
        {
            if (state.IsAllowed == false)
            {
                return "Эта остановка недоступна";
            }

            if (state.CanBoard)
            {
                return "Отпустите: дверь совпала";
            }

            LaneAccess access = state.DoorAccess;

            if (access == null)
            {
                return "Сместите дверь к линии очереди";
            }

            if (access.BusId != null && access.BusId != state.BusId)
            {
                return "Путь перекрыт сверху";
            }

            switch (access.Reason)
            {
                case LaneAccessReason.RouteMismatch:
                    return "У этой очереди другой маршрут";
                case LaneAccessReason.EmptyQueue:
                    return "Эта очередь пуста";
                case LaneAccessReason.Full:
                    return "В автобусе нет свободных мест";
                default:
                    return "Сместите дверь к линии очереди";
            }
        }

        private void SetEdge(int index, Vector2 from, Vector2 to, float width, Color color)
        {
            Vector2 delta = to - from;
            RectTransform rect = _edges[index].rectTransform;
            rect.anchoredPosition = (from + to) * .5f;
            rect.sizeDelta = new Vector2(delta.magnitude, width);
            rect.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            _edges[index].color = new Color(color.r, color.g, color.b, .75f);
        }

        private Image CreateImage(string name)
        {
            RectTransform rect = CreateRect(name);
            Image image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private RectTransform CreateRect(string name)
        {
            RectTransform rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(_root, false);
            rect.anchorMin = new Vector2(.5f, .5f);
            rect.anchorMax = rect.anchorMin;
            return rect;
        }
    }
}
