using System;
using System.Collections.Generic;
using LastExit.Domain;
using LastExit.Platform;
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
        private readonly Image _axis;
        private readonly Image _path;
        private readonly float _queueHeadZ;
        private readonly Text _stateSymbol;
        private int _firstAllowedStop;
        private int _lastAllowedStop;
        private BusStopPreview _state;
        private BusSnapshot _bus;

        public BusStopHints(RectTransform overlay, Func<Vector3, Vector2> project,
            ILevelBusGeometry board, ILevelStopPreview preview, float queueHeadZ)
        {
            _root = (RectTransform)new GameObject("Bus Stop Preview", typeof(RectTransform)).transform;
            _root.SetParent(overlay, false);
            _root.anchorMin = new Vector2(.5f, .5f);
            _root.anchorMax = _root.anchorMin;
            _root.SetAsFirstSibling();
            _project = project;
            _board = board;
            _preview = preview;
            _queueHeadZ = queueHeadZ;

            for (int index = 0; index < _edges.Length; index++)
            {
                _edges[index] = CreateImage("Stop Outline " + index);
            }

            _door = CreateImage("Stop Door");
            _axis = CreateImage("Horizontal movement axis");
            _path = CreateImage("Preview corridor path");
            _path.transform.SetAsFirstSibling();
            _axis.transform.SetAsFirstSibling();
            _stateSymbol = CreateRect("Door alignment status").gameObject.AddComponent<Text>();
            _stateSymbol.font = TerminalUiTheme.LoadFont(null);
            _stateSymbol.alignment = TextAnchor.MiddleCenter;
            _stateSymbol.raycastTarget = false;
            Hide();
        }

        public string Message => _state == null ? string.Empty : GetMessage(_state);

        public void Show(BusSnapshot bus, int leftX, int width, IReadOnlyDictionary<string, int> corridors)
        {
            _bus = bus;
            _state = _preview.GetStopPreview(bus.Id, leftX);
            _root.gameObject.SetActive(_state != null);

            if (_state == null)
            {
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

            _firstAllowedStop = -1;
            _lastAllowedStop = -1;

            for (int index = 0; index < count; index++)
            {
                BusStopPreview candidate = _preview.GetStopPreview(bus.Id, index);

                if (candidate != null && candidate.IsAllowed)
                {
                    if (_firstAllowedStop < 0)
                    {
                        _firstAllowedStop = index;
                    }

                    _lastAllowedStop = index;
                }
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
        }

        public void Hide()
        {
            _state = null;
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
            Color neutral = new Color(.9f, .95f, .94f);
            Color color = _state.CanBoard ? neutral : new Color(1, .78f, .34f);
            _stateSymbol.text = _state.CanBoard ? "=" : "!";
            _stateSymbol.color = color;
            _stateSymbol.fontSize = Mathf.CeilToInt(14 / canvasScale);
            _stateSymbol.rectTransform.sizeDelta = new Vector2(22, 20) / canvasScale;
            _stateSymbol.rectTransform.anchoredPosition = _project(stopDoor) +
                Vector2.up * doorCalloutOffset + Vector2.right * (24 / canvasScale);

            Vector2 axisFrom = _project(new Vector3(_board.GetWorldX(_firstAllowedStop + 1), stopDoor.y, stopDoor.z));
            Vector2 axisTo = _project(new Vector3(_board.GetWorldX(_lastAllowedStop + 1), stopDoor.y, stopDoor.z));
            _axis.rectTransform.anchoredPosition = (axisFrom + axisTo) * .5f + Vector2.up * doorCalloutOffset;
            _axis.rectTransform.sizeDelta = new Vector2(Mathf.Max(24 / canvasScale,
                Vector2.Distance(axisFrom, axisTo)), 2 / canvasScale);
            _axis.enabled = _firstAllowedStop >= 0;
            _axis.color = new Color(neutral.r, neutral.g, neutral.b, .25f);
            LaneAccess access = _state.DoorAccess;
            _path.enabled = access != null && access.BusId != null && access.Reason != LaneAccessReason.EmptyQueue;

            if (_path.enabled)
            {
                Bounds blocker = _board.GetBusBounds(access.BusId);
                Vector2 from = _project(new Vector3(stopDoor.x, .1f, _queueHeadZ - .6f));
                Vector2 to = _project(new Vector3(stopDoor.x, blocker.max.y, blocker.max.z + .15f));
                Vector2 delta = to - from;
                _path.rectTransform.anchoredPosition = (from + to) * .5f;
                _path.rectTransform.sizeDelta = new Vector2(delta.magnitude, 2 / canvasScale);
                _path.rectTransform.localRotation = Quaternion.Euler(0, 0,
                    Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                _path.color = color;
            }

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
            SetEdge(0, new Vector2(minimum.x, minimum.y), new Vector2(maximum.x, minimum.y), thickness, neutral);
            SetEdge(1, new Vector2(maximum.x, minimum.y), new Vector2(maximum.x, maximum.y), thickness, neutral);
            SetEdge(2, new Vector2(maximum.x, maximum.y), new Vector2(minimum.x, maximum.y), thickness, neutral);
            SetEdge(3, new Vector2(minimum.x, maximum.y), new Vector2(minimum.x, minimum.y), thickness, neutral);
            _door.rectTransform.anchoredPosition = _project(stopDoor) + Vector2.up * doorCalloutOffset;
            _door.rectTransform.sizeDelta = new Vector2(28, 5) / canvasScale;
            _door.color = color;
        }

        private static string GetMessage(BusStopPreview state)
        {
            if (state.IsAllowed == false)
            {
                return GameLocalization.Get("stop.unavailable");
            }

            if (state.CanBoard)
            {
                return GameLocalization.Get("stop.aligned");
            }

            LaneAccess access = state.DoorAccess;

            if (access == null)
            {
                return GameLocalization.Get("stop.align");
            }

            if (access.BusId != null && access.BusId != state.BusId)
            {
                return GameLocalization.Get("stop.blocked");
            }

            switch (access.Reason)
            {
                case LaneAccessReason.RouteMismatch:
                    return GameLocalization.Get("stop.route");
                case LaneAccessReason.EmptyQueue:
                    return GameLocalization.Get("stop.empty");
                case LaneAccessReason.Full:
                    return GameLocalization.Get("stop.full");
                default:
                    return GameLocalization.Get("stop.align");
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
