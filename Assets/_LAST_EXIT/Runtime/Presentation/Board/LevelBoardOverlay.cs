using System;
using System.Collections.Generic;
using LastExit.Domain;
using LastExit.Platform;
using UnityEngine;
using UnityEngine.UI;

namespace LastExit.Presentation
{
    // Owns only the projected board hierarchy; session rules and screen chrome stay outside it.
    public class LevelBoardOverlay
    {
        private readonly Dictionary<string, GameObject> _badges = new Dictionary<string, GameObject>();
        private readonly List<Image> _guides = new List<Image>();
        private readonly List<Image> _blockerEnds = new List<Image>();
        private readonly Dictionary<string, ResourceSegmentsGraphic> _seats =
            new Dictionary<string, ResourceSegmentsGraphic>();
        private readonly List<Text> _queueLabels = new List<Text>();
        private readonly List<Image> _doorHints = new List<Image>();
        private readonly List<Text> _blockerLabels = new List<Text>();
        private readonly List<LaneAccess> _laneAccess = new List<LaneAccess>();
        private readonly RectTransform _root;
        private readonly ILevelHudBoard _board;
        private readonly LevelDefinition _definition;
        private readonly GameObject _badgeTemplate;
        private readonly Font _font;
        private readonly Func<Vector3, Vector2> _project;
        private readonly Text _isolationLabel;
        private readonly Image _quarantineBeacon;
        private BoardSnapshot _snapshot;
        private string _tutorialBus;
        private string _selected;
        private float _layoutScale;
        private float _badgeGapPixels;
        private bool _showIdleReadouts;
        private bool _boundsCached;

        public LevelBoardOverlay(RectTransform root, ILevelHudBoard board, LevelDefinition definition,
            GameObject badgeTemplate, Func<Vector3, Vector2> project)
        {
            ValidateBadge(badgeTemplate);
            _root = root;
            _board = board;
            _definition = definition;
            _badgeTemplate = badgeTemplate;
            _font = badgeTemplate.transform.Find("Occupancy").GetComponent<Text>().font;
            _project = project;

            for (int index = 0; index < definition.Rows.Count; index++)
            {
                Image door = CreateImage("Door R" + (index + 1), new Color(.92f, .96f, .91f));
                door.rectTransform.sizeDelta = new Vector2(24, 5);
                _doorHints.Add(door);
            }

            foreach (KeyValuePair<string, int> lane in definition.Corridors)
            {
                _guides.Add(CreateImage("Corridor " + lane.Key, new Color(.64f, .75f, .78f, .5f)));
                Text label = CreateLabel("Queue " + lane.Key);
                label.rectTransform.sizeDelta = new Vector2(90, 34);
                _queueLabels.Add(label);
                _blockerEnds.Add(CreateImage("Blocked endpoint " + lane.Key, new Color(1, .78f, .34f)));
                Text blocker = CreateLabel("Blocker " + lane.Key);
                blocker.color = new Color(1, .8f, .4f);
                _blockerLabels.Add(blocker);
            }

            _quarantineBeacon = CreateImage("Quarantine selection beacon", new Color(.92f, .96f, .91f));
            _quarantineBeacon.gameObject.SetActive(false);
            _isolationLabel = CreateLabel("Isolation Area");
            TerminalUiTheme.StyleText(_isolationLabel);
            _isolationLabel.text = GameLocalization.Get("board.quarantine");
            _isolationLabel.rectTransform.sizeDelta = new Vector2(140, 30);
        }

        public Bounds BoardBounds { get; private set; }

        public void Refresh(BoardSnapshot snapshot, TutorialStep tutorialStep, string firstBusId,
            Func<string, LaneAccess> getLaneAccess)
        {
            _isolationLabel.text = GameLocalization.Get("board.quarantine");
            _snapshot = snapshot;
            _isolationLabel.text = GameLocalization.Get("board.quarantine");
            _tutorialBus = tutorialStep == TutorialStep.AlignFirstBus ? firstBusId : null;

            if (_boundsCached == false)
            {
                CacheBoardBounds();
                _boundsCached = true;
            }

            UpdateBadges(snapshot);
            RefreshLaneReadouts(snapshot, tutorialStep, getLaneAccess);
        }

        public void ApplyScale(float scale)
        {
            _isolationLabel.fontSize = Mathf.CeilToInt(10 / scale);
            _isolationLabel.rectTransform.sizeDelta = new Vector2(96, 24) / scale;

            foreach (Text blocker in _blockerLabels)
            {
                blocker.fontSize = Mathf.Max(18, Mathf.CeilToInt(12 / scale));
                blocker.rectTransform.sizeDelta = new Vector2(Mathf.Max(160, 80 / scale), Mathf.Max(64, 32 / scale));
            }
        }

        public void Project(float scale, float badgeGapPixels, string selected, bool portrait, bool showIdleReadouts, bool quarantineSelected = false)
        {
            _layoutScale = scale;
            _badgeGapPixels = badgeGapPixels;
            _selected = selected;
            _showIdleReadouts = showIdleReadouts;
            float doorCalloutOffset = ((RectTransform)_badgeTemplate.transform).rect.height * .5f + 8 / scale;
            ProjectBadges(_snapshot, doorCalloutOffset);
            ProjectGuides(_snapshot);
            Vector2 isolationPosition = _project(_board.GetIsolationPosition()) + new Vector2(0, 32);
            float leftLimit = -_root.rect.width * .5f + _isolationLabel.rectTransform.rect.width * .5f + 12 / scale;
            RectTransform firstQueueLabel = _queueLabels[0].rectTransform;
            float rightLimit = firstQueueLabel.anchoredPosition.x - firstQueueLabel.rect.width * .5f -
                _isolationLabel.rectTransform.rect.width * .5f - 8 / scale;
            _isolationLabel.gameObject.SetActive(_definition.CctvCharges > 0 && portrait && rightLimit >= leftLimit);
            isolationPosition.x = rightLimit >= leftLimit ? Mathf.Clamp(isolationPosition.x, leftLimit, rightLimit) : leftLimit;
            _isolationLabel.rectTransform.anchoredPosition = isolationPosition;
            _quarantineBeacon.gameObject.SetActive(quarantineSelected);
            _quarantineBeacon.rectTransform.anchoredPosition = _project(_board.GetIsolationPosition());
            _quarantineBeacon.rectTransform.sizeDelta = new Vector2(24, 5) / scale;
        }

        private static void ValidateBadge(GameObject badge)
        {
            Transform occupancy = badge.transform.Find("Occupancy");

            if (badge.TryGetComponent<Image>(out _) == false ||
                occupancy == null || occupancy.TryGetComponent<Text>(out _) == false)
            {
                throw new InvalidOperationException(
                    "Bus badge needs its background and Occupancy text.");
            }
        }

        private void CacheBoardBounds()
        {
            Bounds bounds = new Bounds(new Vector3(_board.GetWorldX(_definition.Width * .5f), 1,
                (_board.QueueHeadZ + _board.RowEndZ) * .5f),
                new Vector3(_board.GetWorldX(_definition.Width) - _board.GetWorldX(0) + 1, 2,
                    _board.QueueHeadZ - _board.RowEndZ + 4));

            foreach (QueueSnapshot queue in _snapshot.Queues)
            {
                foreach (PassengerDefinition passenger in queue.RemainingPassengers)
                {
                    bounds.Encapsulate(_board.GetPassengerBounds(passenger.Id));
                }
            }

            bounds.Encapsulate(new Bounds(_board.GetIsolationPosition() + Vector3.up,
                new Vector3(3.5f, 3, 3)));
            BoardBounds = bounds;
        }

        private void UpdateBadges(BoardSnapshot snapshot)
        {
            HashSet<string> remaining = new HashSet<string>();

            foreach (RowSnapshot row in snapshot.Rows)
            {
                BusSnapshot bus = row.Bus;

                if (bus == null)
                {
                    continue;
                }

                remaining.Add(bus.Id);

                if (_badges.ContainsKey(bus.Id) == false)
                {
                    GameObject badge = UnityEngine.Object.Instantiate(_badgeTemplate, _root);
                    ValidateBadge(badge);
                    badge.name = "Bus " + bus.Id;
                    badge.SetActive(true);
                    _badges.Add(bus.Id, badge);
                    RectTransform segmentsRect = (RectTransform)new GameObject("Seat segments",
                        typeof(RectTransform), typeof(CanvasRenderer), typeof(ResourceSegmentsGraphic)).transform;
                    segmentsRect.SetParent(badge.transform, false);
                    segmentsRect.anchorMin = new Vector2(.5f, 0);
                    segmentsRect.anchorMax = segmentsRect.anchorMin;
                    segmentsRect.anchoredPosition = new Vector2(0, 5);
                    segmentsRect.sizeDelta = new Vector2(64, 4);
                    ResourceSegmentsGraphic segments = segmentsRect.GetComponent<ResourceSegmentsGraphic>();
                    segments.raycastTarget = false;
                    _seats.Add(bus.Id, segments);
                    Text occupancy = badge.transform.Find("Occupancy").GetComponent<Text>();
                    RectTransform badgeRect = (RectTransform)badge.transform;
                    badgeRect.sizeDelta += new Vector2(0, 12);
                    occupancy.rectTransform.anchoredPosition = new Vector2(0, 4);
                }

                _seats[bus.Id].SetCounts(bus.Occupied, bus.LostSeats, bus.Capacity);
                _badges[bus.Id].transform.Find("Occupancy").GetComponent<Text>().text =
                    bus.Occupied + "/" + bus.Capacity + (bus.LostSeats > 0 ? " · −" + bus.LostSeats : "");
                Text count = _badges[bus.Id].transform.Find("Occupancy").GetComponent<Text>();
                float countWidth = Mathf.Max(76, count.preferredWidth + 4);
                count.rectTransform.sizeDelta = new Vector2(countWidth, 32);
                RectTransform readout = (RectTransform)_badges[bus.Id].transform;
                readout.sizeDelta = new Vector2(countWidth + 4, 42);
            }

            List<string> removed = new List<string>();

            foreach (KeyValuePair<string, GameObject> badge in _badges)
            {
                if (remaining.Contains(badge.Key) == false)
                {
                    badge.Value.SetActive(false);
                    UnityEngine.Object.Destroy(badge.Value);
                    removed.Add(badge.Key);
                }
            }

            foreach (string id in removed)
            {
                _badges.Remove(id);
                _seats.Remove(id);
            }
        }

        private void ProjectBadges(BoardSnapshot snapshot, float doorCalloutOffset)
        {
            for (int index = 0; index < snapshot.Rows.Count; index++)
            {
                BusSnapshot bus = snapshot.Rows[index].Bus;
                _doorHints[index].gameObject.SetActive(bus != null);

                if (bus == null)
                {
                    continue;
                }

                Bounds bounds = _board.GetBusBounds(bus.Id);
                Vector3 position = new Vector3(bounds.center.x, bounds.max.y, bounds.max.z);
                RectTransform badge = (RectTransform)_badges[bus.Id].transform;
                badge.anchoredPosition = _project(position) + Vector2.up *
                    (badge.rect.height * .5f + _badgeGapPixels / _layoutScale);
                _seats[bus.Id].rectTransform.sizeDelta = new Vector2(
                    Mathf.Min(badge.rect.width - 12, 64 / _layoutScale), 4 / _layoutScale);
                _seats[bus.Id].rectTransform.anchoredPosition = new Vector2(0, 5 / _layoutScale);
                bool selected = bus.Id == _selected ||
                    (bus.Id == _tutorialBus);
                badge.GetComponent<Image>().color = selected ? new Color(.24f, .34f, .38f, .96f) :
                    new Color(.09f, .15f, .18f, .86f);
                Vector3 door = _board.GetDoor(bus.Id);
                door.y = .08f;
                door.z = bounds.max.z + .3f;
                _doorHints[index].rectTransform.anchoredPosition = _project(door) + Vector2.up * doorCalloutOffset;
                _doorHints[index].rectTransform.sizeDelta = new Vector2(selected ? 32 : 26, 5) / _layoutScale;
            }
        }

        private void RefreshLaneReadouts(BoardSnapshot snapshot, TutorialStep tutorialStep,
            Func<string, LaneAccess> getLaneAccess)
        {
            _laneAccess.Clear();
            int index = 0;

            foreach (KeyValuePair<string, int> lane in _definition.Corridors)
            {
                QueueSnapshot queue = null;

                foreach (QueueSnapshot candidate in snapshot.Queues)
                {
                    if (candidate.Id == lane.Key)
                    {
                        queue = candidate;
                    }
                }

                LaneAccess access = getLaneAccess(lane.Key);
                _laneAccess.Add(access);
                bool tutorialTarget = lane.Key == "A" && tutorialStep == TutorialStep.AlignFirstBus;
                bool blockedByBody = access.BusId != null &&
                    (access.Reason == LaneAccessReason.DoorMisaligned ||
                        access.Reason == LaneAccessReason.RouteMismatch);
                Color blocked = tutorialTarget || blockedByBody ? new Color(.95f, .75f, .35f, .85f) :
                    new Color(.64f, .75f, .78f, .4f);
                _guides[index].color = access.CanBoard ? new Color(.9f, .95f, .94f, .8f) : blocked;
                int count = queue == null ? 0 : queue.RemainingPassengers.Count;
                // Corridor letters already appear above the queues; keep counts legible on five-row phones.
                _queueLabels[index].text = count.ToString();
                _queueLabels[index].color = tutorialTarget ? new Color(.9f, .34f, .33f) :
                    new Color(.9f, .95f, .96f);
                _blockerLabels[index].gameObject.SetActive(blockedByBody && count > 0);
                _blockerEnds[index].gameObject.SetActive(blockedByBody && count > 0);
                _blockerLabels[index].text = GameLocalization.Get(
                    access.Reason == LaneAccessReason.RouteMismatch ? "board.blockedRoute" : "board.blockedBody",
                    access.FirstRowId);
                index++;
            }
        }

        private void ProjectGuides(BoardSnapshot snapshot)
        {
            int index = 0;
            bool showIdleReadouts = _showIdleReadouts;

            foreach (KeyValuePair<string, int> lane in _definition.Corridors)
            {
                LaneAccess access = _laneAccess[index];
                float endZ = access.BusId == null ? _board.RowEndZ - 1 :
                    _board.GetBusBounds(access.BusId).max.z + .15f;
                float x = _board.GetWorldX(lane.Value);
                Vector2 start = _project(new Vector3(x, .1f, _board.QueueHeadZ - .6f));
                float endY = access.BusId == null ? .1f : _board.GetBusBounds(access.BusId).max.y;
                Vector2 end = _project(new Vector3(x, endY, endZ));
                Vector2 delta = end - start;
                RectTransform line = _guides[index].rectTransform;
                line.anchoredPosition = (start + end) * .5f;
                line.sizeDelta = new Vector2(2, delta.magnitude);
                line.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 90);
                _guides[index].enabled = showIdleReadouts;
                Image endpoint = _blockerEnds[index];
                endpoint.rectTransform.anchoredPosition = end;
                endpoint.rectTransform.sizeDelta = new Vector2(18, 3) / _layoutScale;
                endpoint.enabled = showIdleReadouts;
                _queueLabels[index].rectTransform.anchoredPosition = start + new Vector2(0, 18);
                // While dragging, the stop preview replaces the last confirmed state's blocker caption.
                _blockerLabels[index].enabled = showIdleReadouts;

                if (_blockerLabels[index].gameObject.activeSelf)
                {
                    RectTransform badge = (RectTransform)_badges[access.BusId].transform;
                    float upperEdge = badge.anchoredPosition.y + badge.rect.height * .5f;

                    for (int rowIndex = 0; rowIndex < snapshot.Rows.Count; rowIndex++)
                    {
                        if (snapshot.Rows[rowIndex].Bus != null &&
                            snapshot.Rows[rowIndex].Bus.Id == access.BusId)
                        {
                            RectTransform door = _doorHints[rowIndex].rectTransform;
                            upperEdge = Mathf.Max(upperEdge, door.anchoredPosition.y + door.rect.height * .5f);
                        }
                    }

                    float labelY = upperEdge + 8 / _layoutScale +
                        _blockerLabels[index].rectTransform.rect.height * .5f;
                    RectTransform label = _blockerLabels[index].rectTransform;
                    RectTransform queueLabel = _queueLabels[index].rectTransform;
                    float topLimit = queueLabel.anchoredPosition.y - queueLabel.rect.height * .5f -
                        _badgeGapPixels / _layoutScale;
                    float bottomLimit = upperEdge + _badgeGapPixels / _layoutScale;
                    bool fits = topLimit - bottomLimit >= label.rect.height;
                    _blockerLabels[index].enabled = showIdleReadouts && fits;

                    if (fits)
                    {
                        labelY = Mathf.Clamp(labelY, bottomLimit + label.rect.height * .5f,
                            topLimit - label.rect.height * .5f);
                    }

                    label.anchoredPosition = new Vector2(end.x, labelY);
                    Rect caption = new Rect(label.anchoredPosition - label.rect.size * .5f, label.rect.size);

                    foreach (RowSnapshot row in snapshot.Rows)
                    {
                        if (row.Bus == null)
                        {
                            continue;
                        }

                        RectTransform otherBadge = (RectTransform)_badges[row.Bus.Id].transform;
                        Rect badgeArea = new Rect(otherBadge.anchoredPosition - otherBadge.rect.size * .5f,
                            otherBadge.rect.size);

                        if (caption.Overlaps(GetProjectedBodyBounds(_board.GetBusBounds(row.Bus.Id))) ||
                            caption.Overlaps(badgeArea))
                        {
                            _blockerLabels[index].enabled = false;
                        }
                    }

                    for (int previous = 0; previous < index; previous++)
                    {
                        Text other = _blockerLabels[previous];
                        Rect otherArea = new Rect(other.rectTransform.anchoredPosition -
                            other.rectTransform.rect.size * .5f, other.rectTransform.rect.size);

                        if (other.isActiveAndEnabled && caption.Overlaps(otherArea))
                        {
                            _blockerLabels[index].enabled = false;
                        }
                    }
                }

                index++;
            }
        }

        private Rect GetProjectedBodyBounds(Bounds bounds)
        {
            Vector2 minimum = Vector2.positiveInfinity;
            Vector2 maximum = Vector2.negativeInfinity;

            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int z = 0; z < 2; z++)
                    {
                        Vector2 corner = _project(new Vector3(x == 0 ? bounds.min.x : bounds.max.x,
                            y == 0 ? bounds.min.y : bounds.max.y, z == 0 ? bounds.min.z : bounds.max.z));
                        minimum = Vector2.Min(minimum, corner);
                        maximum = Vector2.Max(maximum, corner);
                    }
                }
            }

            return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
        }

        private Image CreateImage(string name, Color color)
        {
            RectTransform rect = CreateRect(name);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Text CreateLabel(string name)
        {
            RectTransform rect = CreateRect(name);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = _font;
            text.fontSize = 22;
            text.color = new Color(.9f, .95f, .96f);
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
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
