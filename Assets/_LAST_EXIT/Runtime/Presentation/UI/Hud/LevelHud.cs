using System;
using LastExit.ArtLab.Preview;
using LastExit.Domain;
using LastExit.Platform;
using UnityEngine;
using UnityEngine.UI;

namespace LastExit.Presentation
{
    [RequireComponent(typeof(Canvas))]
    public class LevelHud : MonoBehaviour, IControlActivation
    {
        [SerializeField] private RectTransform _canvas;
        [SerializeField] private PreviewCanvasScaler _scaler;
        [SerializeField] private PreviewHudLayout _layout;
        [SerializeField] private LevelCameraFraming _framing;
        [SerializeField] private RectTransform _header;
        [SerializeField] private RectTransform _footer;
        [SerializeField] private Text _counts;
        [SerializeField] private Text _title;
        [SerializeField] private Text _subtitle;
        [SerializeField] private Text _hint;
        [SerializeField] private Text _arrivals;
        [SerializeField] private Text _status;
        [SerializeField] private Button _restart;
        [SerializeField] private Button _continue;
        [SerializeField] private GameObject _victoryPanel;
        [SerializeField] private Button _repeat;
        [SerializeField] private Button _next;
        [SerializeField] private Text _resultTitle;
        [SerializeField] private Text _resultDetails;
        [SerializeField] private RectTransform _resultCard;
        [SerializeField] private LevelSecurityPanel _security;
        [SerializeField] private AudioOptionsView _audioOptions;
        [SerializeField] private GameObject _badgeTemplate;
        [SerializeField] private RectTransform _overlay;
        [SerializeField, Min(1)] private float _minimumButtonPixels = 44;
        [SerializeField, Min(0)] private float _badgeGapPixels = 4;
        [SerializeField, Min(.1f)] private float _rejectionHintSeconds = 1.2f;
        [SerializeField, Min(.1f)] private float _stabilityAccentSeconds = .7f;
        [SerializeField, Min(.3f)] private float _defeatRevealSeconds = .35f;
        private ILevelReadout _game;
        private IPreviewHudLayout _hudLayout;
        private IAudioOptionsLayout _audioOptionsLayout;
        private LevelDefinition _definition;
        private Camera _camera;
        private string _selected;
        private bool _initialized;
        private BoardSnapshot _snapshot;
        private bool _portrait;
        private BusStopHints _stopHints;
        private RectTransform _runtimeOverlay;
        private bool _securityEnabled;
        private int _generation;
        private float _layoutScale;
        private bool _controlsSubscribed;
        private LevelBoardOverlay _boardOverlay;
        private float _layoutWidth;
        private float _layoutHeight;
        private float _rejectionRemaining;
        private LevelResultView _resultView;
        private int _lastStability;
        private float _stabilityAccentRemaining;
        private Color _statusColor;
        private float _defeatRevealRemaining;
        private bool _lastDefeat;
        private TerminalPanelGraphic _headerPanel;
        private TerminalPanelGraphic _footerPanel;
        private ResourceSegmentsGraphic _stabilitySegments;
        private bool _compact;

        public event Action RestartRequested;
        public event Action ContinueRequested;
        public event Action NextRequested;
        public event Action ScanRequested;
        public event Action<string> QuarantineRequested;
        public event Action<bool> SecurityViewingChanged;
        public event Action ControlActivated;

        public Font MenuFallbackFont => _title.font;

        public RectTransform[] PointerRegions => new[]
        {
            _header, _footer, (RectTransform)_victoryPanel.transform, _security.PointerRegion,
            _audioOptionsLayout != null ? _audioOptionsLayout.PointerRegion : _header
        };

        private void OnEnable()
        {
            if (_initialized)
            {
                SubscribeControls();
                Refresh();
            }
        }

        private void LateUpdate()
        {
            // Projection follows the camera and moving actors; text comes from the last domain state.
            if (_initialized)
            {
                _resultView.Tick(_game.IsPaused ? 0 : Time.unscaledDeltaTime);

                if (_stabilityAccentRemaining > 0 && _game.IsPaused == false)
                {
                    _stabilityAccentRemaining = Mathf.Max(0,
                        _stabilityAccentRemaining - Time.unscaledDeltaTime);
                    _status.color = Color.Lerp(_statusColor, new Color(1, .32f, .24f),
                        _stabilityAccentRemaining / _stabilityAccentSeconds);
                }

                if (_defeatRevealRemaining > 0 && _game.IsPaused == false)
                {
                    _defeatRevealRemaining = Mathf.Max(0, _defeatRevealRemaining - Time.unscaledDeltaTime);

                    if (_defeatRevealRemaining == 0)
                    {
                        RenderText(_snapshot);
                    }
                }

                if (_rejectionRemaining > 0 && _game.IsPaused == false)
                {
                    _rejectionRemaining = Mathf.Max(0, _rejectionRemaining - Time.unscaledDeltaTime);

                    if (_rejectionRemaining == 0)
                    {
                        RenderText(_snapshot);
                    }
                }

                RefreshLayout();
            }
        }

        private void OnDisable()
        {
            if (_initialized)
            {
                UnsubscribeControls();
            }
        }

        private void OnDestroy()
        {
            UnsubscribeControls();
        }

        public void Initialize(Camera camera, ILevelReadout game, ILevelHudBoard board,
            ILevelInspectionBoard inspectionBoard, LevelDefinition definition, ILevelStopPreview stopPreview,
            SecurityTutorial securityTutorial = null)
        {
            Shutdown();
            _camera = camera;
            ApplyTerminalTheme();
            _game = game;
            _definition = definition;
            _statusColor = _status.color;
            if (_resultView == null)
            {
                _resultView = new LevelResultView(_resultCard, _resultTitle, _resultDetails, _repeat, _next);
            }

            _resultView.SetResult(-1);
            _hudLayout = _layout;
            _audioOptionsLayout = _audioOptions;
            _securityEnabled = definition.SupportsInspection;
            _framing.Configure(definition.Rows.Count, _securityEnabled);
            _security.gameObject.SetActive(_securityEnabled);
            _runtimeOverlay = (RectTransform)new GameObject("Attempt Overlay", typeof(RectTransform)).transform;
            _runtimeOverlay.SetParent(_overlay, false);
            _runtimeOverlay.anchorMin = Vector2.zero;
            _runtimeOverlay.anchorMax = Vector2.one;
            _runtimeOverlay.sizeDelta = Vector2.zero;
            _stopHints = new BusStopHints(_runtimeOverlay, Project, board, stopPreview, board.QueueHeadZ);
            _boardOverlay = new LevelBoardOverlay(_runtimeOverlay, board, definition, _badgeTemplate, Project);
            _security.Initialize(_camera, _canvas, inspectionBoard, securityTutorial);
            _layoutScale = 1;
            _layoutWidth = 0;
            _generation = -1;

            if (isActiveAndEnabled)
            {
                SubscribeControls();
            }

            _initialized = true;
            Refresh();
        }

        public void Shutdown()
        {
            if (_initialized == false)
            {
                return;
            }

            UnsubscribeControls();
            _initialized = false;
            _runtimeOverlay.gameObject.SetActive(false);
            Destroy(_runtimeOverlay.gameObject);
            _boardOverlay = null;
            _selected = null;
            _snapshot = null;
            _stopHints = null;
            _rejectionRemaining = 0;
            _victoryPanel.SetActive(false);
            _resultView.SetResult(-1);
            _security.gameObject.SetActive(false);
        }

        public void Select(string busId)
        {
            _selected = busId;

            if (busId != null)
            {
                _rejectionRemaining = 0;
            }
        }

        public void ShowRejectedMove()
        {
            _rejectionRemaining = _rejectionHintSeconds;
            RenderText(_snapshot);
        }

        public void Preview(string busId, int leftX)
        {
            _stopHints.Hide();

            foreach (RowSnapshot row in _snapshot.Rows)
            {
                if (row.Bus != null && row.Bus.Id == busId)
                {
                    _stopHints.Show(row.Bus, leftX, _definition.Width, _definition.Corridors);
                }
            }

            RenderText(_snapshot);
        }

        public void Refresh()
        {
            if (_initialized)
            {
                _snapshot = _game.Snapshot;

                if (_generation != _snapshot.Generation)
                {
                    _generation = _snapshot.Generation;
                    _selected = null;
                    _rejectionRemaining = 0;
                    _stopHints.Hide();
                    _resultView.SetResult(-1);
                    _lastStability = _snapshot.Stability;
                    _stabilityAccentRemaining = 0;
                    _status.color = _statusColor;
                    _lastDefeat = false;
                    _defeatRevealRemaining = 0;
                }

                if (_snapshot.Stability < _lastStability)
                {
                    _stabilityAccentRemaining = _stabilityAccentSeconds;
                    _status.color = new Color(1, .32f, .24f);
                }

                _lastStability = _snapshot.Stability;

                if (_snapshot.IsDefeat && _lastDefeat == false)
                {
                    _defeatRevealRemaining = _defeatRevealSeconds;
                }

                _lastDefeat = _snapshot.IsDefeat;

                _boardOverlay.Refresh(_snapshot, _game.Tutorial.Step, _game.Tutorial.FirstBusId,
                    _game.GetLaneAccess);
                RenderText(_snapshot);
                RefreshLayout();
            }
        }

        private static void SetHudRect(RectTransform rect, Vector2 min, Vector2 max,
            Vector2 position, Vector2 size)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetHeaderText(Text text, float y, float height)
        {
            SetHudRect(text.rectTransform, new Vector2(0, 1), Vector2.one,
                new Vector2(0, -y), new Vector2(-32, height));
            text.alignment = TextAnchor.MiddleCenter;
        }

        private static string GetIsolationResult(string result)
        {
            switch (result)
            {
                case "EchoRemoved": return GameLocalization.Get("result.echoRemoved");
                case "HumanLost": return GameLocalization.Get("result.humanLost");
                case "RiskAccepted": return GameLocalization.Get("result.riskAccepted");
                default: return string.Empty;
            }
        }

        private static TerminalPanelGraphic CreatePanel(RectTransform parent)
        {
            RectTransform rect = (RectTransform)new GameObject("Terminal HUD groups",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(TerminalPanelGraphic)).transform;
            rect.SetParent(parent, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            TerminalPanelGraphic panel = rect.GetComponent<TerminalPanelGraphic>();
            panel.color = TerminalUiTheme.Border;
            panel.raycastTarget = false;
            return panel;
        }

        private static string GetRouteLabel(string route)
        {
            switch (route)
            {
                case "R": return "■ R";
                case "T": return "● T";
                case "B": return "▲ B";
                case "G": return "◆ G";
                default: return route;
            }
        }

        private void SubscribeControls()
        {
            if (_controlsSubscribed)
            {
                return;
            }

            _restart.onClick.AddListener(OnRestartRequested);
            _repeat.onClick.AddListener(OnRestartRequested);
            _continue.onClick.AddListener(OnContinueRequested);
            _next.onClick.AddListener(OnNextRequested);
            _security.ScanRequested += OnScanRequested;
            _security.QuarantineRequested += OnQuarantineRequested;
            _security.SecurityViewingChanged += OnSecurityViewingChanged;
            _security.ControlActivated += OnControlActivated;
            GameLocalization.Changed += Refresh;
            _controlsSubscribed = true;
        }

        private void UnsubscribeControls()
        {
            if (_controlsSubscribed == false)
            {
                return;
            }

            _restart.onClick.RemoveListener(OnRestartRequested);
            _repeat.onClick.RemoveListener(OnRestartRequested);
            _continue.onClick.RemoveListener(OnContinueRequested);
            _next.onClick.RemoveListener(OnNextRequested);
            _security.ScanRequested -= OnScanRequested;
            _security.QuarantineRequested -= OnQuarantineRequested;
            _security.SecurityViewingChanged -= OnSecurityViewingChanged;
            _security.ControlActivated -= OnControlActivated;
            GameLocalization.Changed -= Refresh;
            _controlsSubscribed = false;
        }

        private void RefreshLayout()
        {
            if (WebViewportMetrics.HasDrawableCamera(_camera) == false)
            {
                _layoutWidth = 0;
                return;
            }

            _scaler.RefreshScale();
            Canvas canvas = _canvas.GetComponent<Canvas>();
            float cssScale = canvas.scaleFactor / WebViewportMetrics.RenderDensity;

            if (WebViewportMetrics.HasArea(_canvas.rect) == false || cssScale <= 0 || float.IsFinite(cssScale) == false)
            {
                _layoutWidth = 0;
                return;
            }

            _framing.RefreshFraming();
            bool portrait = TerminalUiTheme.UsesStackedHud(_canvas.rect.width * cssScale,
                _canvas.rect.height * cssScale);
            _hudLayout.Apply(portrait, cssScale);
            EnsureButtonHeight(_continue, cssScale);

            if (_portrait != portrait || Mathf.Approximately(_layoutScale, cssScale) == false ||
                _layoutWidth != _canvas.rect.width || _layoutHeight != _canvas.rect.height)
            {
                _portrait = portrait;
                _layoutScale = cssScale;
                if (TryApplyCampaignLayout(cssScale) == false)
                {
                    _layoutWidth = 0;
                    return;
                }

                _layoutWidth = _canvas.rect.width;
                _layoutHeight = _canvas.rect.height;
                RenderText(_snapshot);
            }

            float doorCalloutOffset = ((RectTransform)_badgeTemplate.transform).rect.height * .5f +
                8 / cssScale;
            _boardOverlay.Project(cssScale, _badgeGapPixels, _selected, _portrait,
                string.IsNullOrEmpty(_stopHints.Message), _security.IsInspectionOpen && _security.SelectedQueue != null);
            _stopHints.Project(cssScale, doorCalloutOffset);
            _security.ProjectTargets(cssScale);
            _audioOptionsLayout?.RefreshLayout(cssScale, portrait);
            RefreshPanelSections(cssScale);
        }

        private bool TryApplyCampaignLayout(float scale)
        {
            float screenWidth = _canvas.rect.width * scale;
            float screenHeight = _canvas.rect.height * scale;
            _compact = _portrait == false && screenHeight < 560;
            float header = 90 / scale;
            float footer = (_securityEnabled ? 206 : 112) / scale;
            float left = TerminalUiTheme.GetLeftPanelWidth(screenWidth) / scale;
            float right = left;
            float margin = (_portrait ? 8 : TerminalUiTheme.GetSideMargin(screenWidth)) / scale;

            Vector2 canvasSize = _canvas.rect.size;
            Rect available = _portrait
                ? new Rect(margin / canvasSize.x, (footer + margin) / canvasSize.y,
                    1 - margin * 2 / canvasSize.x, 1 - (header + footer + margin * 2) / canvasSize.y)
                : new Rect((left + margin * 2) / canvasSize.x, margin / canvasSize.y,
                    1 - (left + right + margin * 4) / canvasSize.x, 1 - margin * 2 / canvasSize.y);

            if (_framing.TrySetAvailableRegion(available, _boardOverlay.BoardBounds) == false)
            {
                return false;
            }

            _boardOverlay.ApplyScale(scale);

            if (_portrait)
            {
                SetHudRect(_header, new Vector2(0, 1), Vector2.one, new Vector2(0, -header * .5f),
                    new Vector2(0, header));
                SetHudRect(_footer, Vector2.zero, new Vector2(1, 0), new Vector2(0, footer * .5f),
                    new Vector2(0, footer));
            }
            else
            {
                SetHudRect(_header, Vector2.zero, new Vector2(0, 1),
                    new Vector2(left * .5f + margin, 0), new Vector2(left, -margin * 2));
                SetHudRect(_footer, new Vector2(1, 0), Vector2.one,
                    new Vector2(-right * .5f - margin, 0), new Vector2(right, -margin * 2));
            }

            SetHeaderText(_title, (_portrait ? 22 : _compact ? 22 : 30) / scale,
                (_portrait ? 28 : _compact ? 30 : 36) / scale);
            SetHeaderText(_subtitle, (_portrait ? 51 : _compact ? 52 : 70) / scale, 24 / scale);
            SetHeaderText(_counts, (_portrait ? 77 : _compact ? 88 : 174) / scale,
                (_portrait ? 22 : _compact ? 34 : 128) / scale);
            _title.fontSize = Mathf.CeilToInt((_portrait ? 16 : _compact ? 18 : 22) / scale);
            _subtitle.fontSize = Mathf.CeilToInt((_portrait ? 12 : _compact ? 16 : 12) / scale);
            _counts.fontSize = Mathf.CeilToInt((_portrait || _compact ? 11 : 12) / scale);
            _title.alignment = TextAnchor.MiddleLeft;
            _subtitle.alignment = TextAnchor.MiddleLeft;
            _counts.alignment = _portrait ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;

            if (_portrait)
            {
                foreach (Text label in new[] { _title, _subtitle })
                {
                    SetHudRect(label.rectTransform, new Vector2(0, 1), new Vector2(0, 1),
                        new Vector2(75 / scale, -(label == _title ? 22 : 51) / scale),
                        new Vector2(126, label == _title ? 28 : 24) / scale);
                }
            }

            RectTransform restart = (RectTransform)_restart.transform;
            SetHudRect(restart, _portrait ? new Vector2(1, 1) : new Vector2(_compact ? .75f : .5f, 0),
                _portrait ? new Vector2(1, 1) : new Vector2(_compact ? .75f : .5f, 0),
                _portrait ? new Vector2(-46 / scale, -28 / scale) : new Vector2(0, 30 / scale),
                new Vector2((_portrait ? 84 : _compact ? Mathf.Min(84, left * scale * .5f - 4) :
                    left * scale - 32) / scale, 44 / scale));
            _restart.GetComponentInChildren<Text>().fontSize = Mathf.CeilToInt(12 / scale);
            SetHudRect((RectTransform)_continue.transform, new Vector2(.5f, 0), new Vector2(.5f, 0),
                new Vector2(0, 30 / scale),
                new Vector2((_portrait ? 176 : 132) / scale, _minimumButtonPixels / scale));
            _continue.GetComponentInChildren<Text>().fontSize = Mathf.CeilToInt(14 / scale);
            float footerHeight = _footer.rect.height * scale;
            SetFooterText(_hint, (_portrait ? _securityEnabled ? 173 : 34 :
                _compact ? 190 : _securityEnabled ? 310 : 72) / scale,
                (_portrait ? 52 : _compact ? 60 : 114) / scale);
            _arrivals.transform.SetParent(_compact ? _header : _footer, false);
            SetFooterText(_arrivals, (_portrait ? _securityEnabled ? 120 : 77 :
                _compact ? 126 : footerHeight - 62) / scale, (_portrait || _compact ? 30 : 58) / scale);
            SetFooterText(_status, (_portrait ? 22 : _compact ? footerHeight - 23 : 52) / scale,
                (_portrait ? 42 : _compact ? 42 : 80) / scale);
            _hint.fontSize = Mathf.CeilToInt((_compact ? 11 : 12) / scale);
            _arrivals.fontSize = Mathf.CeilToInt(11 / scale);
            _status.fontSize = Mathf.CeilToInt((_compact ? 10 : 12) / scale);
            _hint.alignment = _portrait ? TextAnchor.MiddleCenter : TextAnchor.UpperLeft;
            _arrivals.alignment = _portrait ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
            _status.alignment = _portrait ? TextAnchor.MiddleCenter : TextAnchor.UpperLeft;
            _stabilitySegments.rectTransform.sizeDelta = new Vector2(66, 5) / scale;
            _stabilitySegments.rectTransform.anchoredPosition = new Vector2(0, 3 / scale);
            _resultView.ApplyLayout(scale, _canvas.rect.size);
            _security.RefreshLayout(scale, _portrait);
            return true;
        }

        private void ApplyTerminalTheme()
        {
            _camera.backgroundColor = TerminalUiTheme.Background;

            foreach (RectTransform panel in new[] { _header, _footer })
            {
                Image image = panel.GetComponent<Image>();

                if (image != null)
                {
                    image.enabled = false;
                    Outline outline = panel.GetComponent<Outline>();

                    if (outline != null)
                    {
                        outline.enabled = false;
                    }
                }
            }

            if (_headerPanel == null)
            {
                _headerPanel = CreatePanel(_header);
                _footerPanel = CreatePanel(_footer);
            }

            foreach (Text label in new[] { _title, _subtitle, _counts, _hint, _arrivals, _status })
            {
                TerminalUiTheme.StyleText(label, label == _arrivals);
                label.supportRichText = true;
            }

            foreach (Text text in new[] { _counts, _hint, _arrivals, _status })
            {
                TerminalUiTheme.StyleBodyText(text);
            }

            foreach (Button button in new[] { _restart, _continue, _repeat, _next })
            {
                TerminalUiTheme.StyleButton(button, button == _continue || button == _next);
            }

            if (_stabilitySegments == null)
            {
                RectTransform rect = (RectTransform)new GameObject("Stability segments",
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(ResourceSegmentsGraphic)).transform;
                rect.SetParent(_status.transform, false);
                rect.anchorMin = new Vector2(.5f, 0);
                rect.anchorMax = rect.anchorMin;
                _stabilitySegments = rect.GetComponent<ResourceSegmentsGraphic>();
                _stabilitySegments.raycastTarget = false;
            }
        }

        private void RefreshPanelSections(float scale)
        {
            float headerHeight = _header.rect.height * scale;
            float footerHeight = _footer.rect.height * scale;
            _headerPanel.SetSections(scale, _portrait ? headerHeight : _compact ? 148 : 324,
                _portrait ? 0 : _compact ? 112 : 164);
            bool instructions = _hint.gameObject.activeSelf || _security.HasInstruction ||
                _security.IsInspectionOpen || _security.IsVideo;
            float top = _securityEnabled ? instructions ? 406 : 236 :
                _hint.gameObject.activeSelf || _game.IsPaused ? 148 : 0;
            _footerPanel.SetSections(scale, _portrait || _compact ? footerHeight : top,
                _portrait || _compact || _arrivals.gameObject.activeSelf == false ? 0 : 112);
        }

        private void SetFooterText(Text text, float y, float height)
        {
            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(0, -y);
            rect.sizeDelta = new Vector2(-24, height);
            text.alignment = TextAnchor.MiddleCenter;
        }

        private void EnsureButtonHeight(Button button, float scale)
        {
            RectTransform rect = (RectTransform)button.transform;
            float height = _minimumButtonPixels / scale;

            if (Mathf.Approximately(rect.sizeDelta.y, height) == false)
            {
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
            }
        }

        private void RenderText(BoardSnapshot snapshot)
        {
            int passengers = 0;
            int remainingBuses = 0;
            int lostSeats = snapshot.LostSeats;

            foreach (QueueSnapshot queue in snapshot.Queues)
            {
                passengers += queue.RemainingPassengers.Count;
            }

            foreach (RowSnapshot row in snapshot.Rows)
            {
                remainingBuses += row.Bus == null ? 0 : 1;
            }

            remainingBuses += snapshot.NextBusIds.Count;
            _title.text = "LAST EXIT";
            _subtitle.text = GameLocalization.FormatLevel(_game.LevelId);
            string countsKey = _portrait ? "hud.counts" : _compact ? "hud.compactCounts" : "terminal.counts";
            _counts.text = GameLocalization.Get(countsKey, passengers, remainingBuses, Mathf.CeilToInt(40 / _layoutScale));
            _restart.GetComponentInChildren<Text>().text = GameLocalization.Get("action.restart");
            _repeat.GetComponentInChildren<Text>().text = GameLocalization.Get("action.repeat");
            _next.GetComponentInChildren<Text>().text = GameLocalization.Get("action.next");
            _continue.GetComponentInChildren<Text>().text = GameLocalization.Get("action.continue");
            string instruction = _game.Tutorial.Text;

            _hint.text = string.IsNullOrEmpty(_stopHints.Message) ? instruction : _stopHints.Message;

            if (_rejectionRemaining > 0)
            {
                _hint.text = _game.Tutorial.IsComplete ?
                    GameLocalization.Get("hud.reject") : _game.Tutorial.Text;
            }

            _hint.gameObject.SetActive(string.IsNullOrEmpty(_hint.text) == false);
            _arrivals.text = string.Empty;

            if (snapshot.NextBusIds.Count > 0)
            {
                string nextId = snapshot.NextBusIds[0];

                foreach (BusDefinition bus in _definition.Buses)
                {
                    if (bus.Id == nextId)
                    {
                        _arrivals.text = GameLocalization.Get("hud.nextBus", GetRouteLabel(bus.Route),
                            GameLocalization.Get("bus.class." + bus.Class), bus.Capacity);
                    }
                }
            }

            _arrivals.gameObject.SetActive(snapshot.NextBusIds.Count > 0);
            string isolationResult = GetIsolationResult(snapshot.LastIsolationResult);
            string statusResult = _compact && snapshot.LastIsolationResult == "HumanLost"
                ? GameLocalization.Get("result.compactHumanLost") : isolationResult;
            _status.text = _game.IsPaused ? GameLocalization.Get("hud.paused") : _securityEnabled ?
                GameLocalization.Get(_compact ? "hud.compactStability" : "hud.stability", snapshot.Stability, lostSeats) +
                    (string.IsNullOrEmpty(statusResult) ? "" : "\n" + statusResult) : "";
            _stabilitySegments.SetCounts(snapshot.Stability, 0, _definition.InitialStability);
            _stabilitySegments.gameObject.SetActive(_securityEnabled);
            bool explanation = _game.Tutorial.Step == TutorialStep.ArrivalExplanation;
            _continue.gameObject.SetActive(explanation);
            _victoryPanel.SetActive(snapshot.IsVictory || (snapshot.IsDefeat && _defeatRevealRemaining <= 0));
            _resultTitle.text = GameLocalization.Get(snapshot.IsDefeat ? "result.defeat" : "result.victory");
            string evacuation = snapshot.LostSeats > 0 ? GameLocalization.Get("result.lostSeats", snapshot.LostSeats) :
                GameLocalization.Get("result.evacuated");
            _resultDetails.text = snapshot.IsDefeat ? GameLocalization.Get("result.retryReason") :
                _game.HasNextLevel ? GameLocalization.FormatLevel(_game.LevelId) + " · " + evacuation :
                GameLocalization.Get("result.campaignDone", evacuation);

            if (snapshot.IsDefeat && string.IsNullOrEmpty(isolationResult) == false)
            {
                _resultDetails.text = isolationResult + "\n" + GameLocalization.Get("result.stabilityEmpty");
            }
            _next.gameObject.SetActive(snapshot.IsVictory && _game.HasNextLevel);
            _resultView.SetResult(snapshot.IsVictory ? _game.ResultStars : snapshot.IsDefeat ? 0 : -1);
            _next.interactable = _game.IsPaused == false;
            _restart.interactable = _game.IsPaused == false;
            _repeat.interactable = _game.IsPaused == false;
            _continue.interactable = _game.IsPaused == false;

            if (_securityEnabled)
            {
                _security.Render(snapshot, _game.CanUseSecurity, _game.IsPaused, _game.IsCctvAnomaly,
                    _game.CanRequestScan, _game.IsScanQueued);
                bool inspecting = _security.IsInspectionOpen || _security.IsVideo || _security.HasInstruction;
                _hint.gameObject.SetActive(inspecting == false && string.IsNullOrEmpty(_hint.text) == false);
                _arrivals.gameObject.SetActive(inspecting == false && snapshot.NextBusIds.Count > 0);
            }

            // Keep the inherited footer readable while the explanation button is present.
            if (explanation)
            {
                _arrivals.text = string.Empty;
            }
        }

        private Vector2 Project(Vector3 position)
        {
            return PreviewScreenProjection.Project(_camera, _canvas, position);
        }

        private void OnRestartRequested()
        {
            RestartRequested?.Invoke();
            ControlActivated?.Invoke();
        }

        private void OnContinueRequested()
        {
            ContinueRequested?.Invoke();
            ControlActivated?.Invoke();
        }

        private void OnNextRequested()
        {
            NextRequested?.Invoke();
            ControlActivated?.Invoke();
        }

        private void OnScanRequested()
        {
            ScanRequested?.Invoke();
        }

        private void OnQuarantineRequested(string queueId)
        {
            QuarantineRequested?.Invoke(queueId);
        }

        private void OnSecurityViewingChanged(bool viewing)
        {
            SecurityViewingChanged?.Invoke(viewing);
        }

        private void OnControlActivated()
        {
            ControlActivated?.Invoke();
        }
    }
}
