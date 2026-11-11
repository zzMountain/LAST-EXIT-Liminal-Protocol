using System;
using LastExit.ArtLab.Preview;
using LastExit.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace LastExit.Presentation
{
    [RequireComponent(typeof(Canvas))]
    public class LevelHud : MonoBehaviour, ILevelDiagnosticControls, IControlActivation
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

        public event Action RestartRequested;
        public event Action ContinueRequested;
        public event Action NextRequested;
        public event Action ScanRequested;
        public event Action<string> QuarantineRequested;
        public event Action RiskRequested;
        public event Action<bool> SecurityViewingChanged;
        public event Action ControlActivated;

        public string CctvPhase => _security.IsVideo ? "Video" : _security.IsInspectionOpen ? "Selection" : "Closed";
        public float CctvRemaining => _security.VideoRemaining;
        public string SelectedQueue => _security.SelectedQueue;

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
            }
        }

        private void LateUpdate()
        {
            // Projection follows the camera and moving actors; text comes from the last domain state.
            if (_initialized)
            {
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
            ILevelInspectionBoard inspectionBoard, LevelDefinition definition, ILevelStopPreview stopPreview)
        {
            Shutdown();
            _camera = camera;
            _game = game;
            _definition = definition;
            _hudLayout = _layout;
            _audioOptionsLayout = _audioOptions;
            _securityEnabled = definition.CctvCharges > 0;
            _framing.Configure(definition.Rows.Count, _securityEnabled);
            _security.gameObject.SetActive(_securityEnabled);
            _runtimeOverlay = (RectTransform)new GameObject("Attempt Overlay", typeof(RectTransform)).transform;
            _runtimeOverlay.SetParent(_overlay, false);
            _runtimeOverlay.anchorMin = Vector2.zero;
            _runtimeOverlay.anchorMax = Vector2.one;
            _runtimeOverlay.sizeDelta = Vector2.zero;
            _stopHints = new BusStopHints(_runtimeOverlay, Project, board, stopPreview);
            _boardOverlay = new LevelBoardOverlay(_runtimeOverlay, board, definition, _badgeTemplate, Project);
            _security.Initialize(_camera, _canvas, inspectionBoard);
            _layoutScale = 0;
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

        public Vector2 GetContinuePosition()
        {
            return GetControlPosition(_continue);
        }

        public Vector2 GetRepeatPosition()
        {
            return GetControlPosition(_repeat);
        }

        public Vector2 GetRestartPosition()
        {
            return GetControlPosition(_restart);
        }

        public Vector2 GetNextPosition()
        {
            return GetControlPosition(_next);
        }

        public Vector2 GetScanPosition()
        {
            return GetControlPosition(_security.GetScanControl());
        }

        public Vector2 GetRiskPosition()
        {
            return GetControlPosition(_security.GetRiskControl());
        }

        public Vector2 GetQueuePosition(string queueId)
        {
            return GetControlPosition(_security.GetQueueControl(queueId));
        }

        public Vector2 GetSelectionPosition()
        {
            return GetControlPosition(_security.GetSelectionControl());
        }

        public Vector2 GetClosePosition()
        {
            return GetControlPosition(_security.GetCloseControl());
        }

        public Vector2 GetQuarantinePosition()
        {
            return GetControlPosition(_security.GetQuarantineControl());
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
                }

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
                new Vector2(0, -y), new Vector2(-12, height));
            text.alignment = TextAnchor.MiddleCenter;
        }

        private static string GetIsolationResult(string result)
        {
            switch (result)
            {
                case "EchoRemoved": return "ECHO устранён";
                case "HumanLost": return "Ошибка: обычный пассажир. −1 стабильность";
                case "RiskAccepted": return "Риск принят: −1 стабильность";
                default: return string.Empty;
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
            _security.RiskRequested += OnRiskRequested;
            _security.SecurityViewingChanged += OnSecurityViewingChanged;
            _security.ControlActivated += OnControlActivated;
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
            _security.RiskRequested -= OnRiskRequested;
            _security.SecurityViewingChanged -= OnSecurityViewingChanged;
            _security.ControlActivated -= OnControlActivated;
            _controlsSubscribed = false;
        }

        private Vector2 GetControlPosition(Button button)
        {
            RectTransform rect = (RectTransform)button.transform;
            Vector3 world = rect.TransformPoint(rect.rect.center);
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(_camera, world);
            return new Vector2(screen.x, _camera.pixelHeight - screen.y);
        }

        private void RefreshLayout()
        {
            _framing.RefreshFraming();
            _scaler.RefreshScale();
            Canvas canvas = _canvas.GetComponent<Canvas>();
            bool portrait = _camera.aspect < 1;
            float cssScale = canvas.scaleFactor / WebViewportMetrics.RenderDensity;
            _hudLayout.Apply(portrait, cssScale);
            EnsureButtonHeight(_continue, cssScale);
            EnsureButtonHeight(_repeat, cssScale);
            EnsureButtonHeight(_next, cssScale);

            if (_portrait != portrait || Mathf.Approximately(_layoutScale, cssScale) == false ||
                _layoutWidth != _canvas.rect.width || _layoutHeight != _canvas.rect.height)
            {
                _portrait = portrait;
                _layoutScale = cssScale;
                _layoutWidth = _canvas.rect.width;
                _layoutHeight = _canvas.rect.height;
                ApplyCampaignLayout(cssScale);
                RenderText(_snapshot);
            }

            float doorCalloutOffset = ((RectTransform)_badgeTemplate.transform).rect.height * .5f +
                8 / cssScale;
            _boardOverlay.Project(cssScale, _badgeGapPixels, _selected, _portrait,
                string.IsNullOrEmpty(_stopHints.Message));
            _stopHints.Project(cssScale, doorCalloutOffset);
            _security.ProjectTargets(cssScale);
            _audioOptionsLayout?.RefreshLayout(cssScale, portrait);
        }

        private void ApplyCampaignLayout(float scale)
        {
            _boardOverlay.ApplyScale(scale);

            float header = 82 / scale;
            float footer = (_securityEnabled ? 212 : 90) / scale;
            float left = 104 / scale;
            float right = 148 / scale;
            float margin = 8 / scale;

            if (_portrait)
            {
                SetHudRect(_header, new Vector2(0, 1), Vector2.one, new Vector2(0, -header * .5f),
                    new Vector2(0, header));
                SetHudRect(_footer, Vector2.zero, new Vector2(1, 0), new Vector2(0, footer * .5f),
                    new Vector2(0, footer));
                _framing.SetAvailableRegion(new Rect(margin / _canvas.rect.width,
                    (footer + margin) / _canvas.rect.height, 1 - margin * 2 / _canvas.rect.width,
                    1 - (header + footer + margin * 2) / _canvas.rect.height), _boardOverlay.BoardBounds);
            }
            else
            {
                SetHudRect(_header, Vector2.zero, new Vector2(0, 1), new Vector2(left * .5f, 0),
                    new Vector2(left, 0));
                SetHudRect(_footer, new Vector2(1, 0), Vector2.one, new Vector2(-right * .5f, 0),
                    new Vector2(right, 0));
                _framing.SetAvailableRegion(new Rect((left + margin) / _canvas.rect.width,
                    margin / _canvas.rect.height, 1 - (left + right + margin * 2) / _canvas.rect.width,
                    1 - margin * 2 / _canvas.rect.height), _boardOverlay.BoardBounds);
            }

            SetHeaderText(_title, 17 / scale, 24 / scale);
            SetHeaderText(_subtitle, 42 / scale, 20 / scale);
            SetHeaderText(_counts, (_portrait ? 65 : 94) / scale, (_portrait ? 22 : 66) / scale);
            _title.fontSize = Mathf.CeilToInt(15 / scale);
            _subtitle.fontSize = Mathf.CeilToInt(12 / scale);
            _counts.fontSize = Mathf.CeilToInt(12 / scale);

            if (_portrait)
            {
                float offset = _audioOptionsLayout != null ? 70 : 44;
                float width = _audioOptionsLayout != null ? 144 : 92;
                _title.rectTransform.anchoredPosition += new Vector2(-offset / scale, 0);
                _title.rectTransform.sizeDelta += new Vector2(-width / scale, 0);
                _subtitle.rectTransform.anchoredPosition += new Vector2(-offset / scale, 0);
                _subtitle.rectTransform.sizeDelta += new Vector2(-width / scale, 0);
            }

            RectTransform restart = (RectTransform)_restart.transform;
            SetHudRect(restart, _portrait ? new Vector2(1, 1) : new Vector2(.5f, 0),
                _portrait ? new Vector2(1, 1) : new Vector2(.5f, 0),
                _portrait ? new Vector2(-46 / scale, -28 / scale) : new Vector2(0, 30 / scale),
                new Vector2(84 / scale, 44 / scale));
            _restart.GetComponentInChildren<Text>().fontSize = Mathf.CeilToInt(13 / scale);
            SetHudRect((RectTransform)_continue.transform, new Vector2(.5f, 0), new Vector2(.5f, 0),
                new Vector2(0, 30 / scale),
                new Vector2((_portrait ? 176 : 132) / scale, _minimumButtonPixels / scale));
            _continue.GetComponentInChildren<Text>().fontSize = Mathf.CeilToInt(14 / scale);
            SetFooterText(_hint, (_portrait ? _securityEnabled ? 125 : 26 : _securityEnabled ? 145 : 50) / scale,
                (_portrait ? 36 : 70) / scale);
            SetFooterText(_arrivals, (_portrait ? _securityEnabled ? 157 : 57 : _securityEnabled ? 209 : 130) / scale,
                (_portrait ? 22 : 46) / scale);
            SetFooterText(_status, (_portrait ? _securityEnabled ? 190 : 135 : 266) / scale,
                (_portrait ? 40 : 64) / scale);
            _hint.fontSize = Mathf.CeilToInt(12 / scale);
            _arrivals.fontSize = Mathf.CeilToInt(11 / scale);
            _status.fontSize = Mathf.CeilToInt(11 / scale);
            float height = _minimumButtonPixels / scale;
            float cardHeight = Mathf.Min(_canvas.rect.height - 16 / scale, 350 / scale);
            _resultCard.sizeDelta = new Vector2(Mathf.Min(_canvas.rect.width - 16 / scale, 500 / scale), cardHeight);
            SetResultButton(_repeat, height * .5f + 8 / scale, height);
            SetResultButton(_next, height * 1.5f + 16 / scale, height);
            SetHeaderText(_resultTitle, 28 / scale, 38 / scale);
            SetHeaderText(_resultDetails, 80 / scale, 58 / scale);
            _resultTitle.fontSize = Mathf.CeilToInt(22 / scale);
            _resultDetails.fontSize = Mathf.CeilToInt(15 / scale);
            _security.RefreshLayout(scale, _portrait);
        }

        private void SetResultButton(Button button, float y, float height)
        {
            RectTransform rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(.5f, 0);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(0, y);
            rect.sizeDelta = new Vector2(_resultCard.sizeDelta.x - 40, height);
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
            _title.text = _portrait ? "L-07 / LAST EXIT" : "L-07\nLAST EXIT";
            _subtitle.text = _game.LevelId;
            _counts.text = "В очередях " + passengers + "   /   Автобусов " + remainingBuses;
            string instruction = _game.Tutorial.Text;

            if (snapshot.HasSecurityCheck)
            {
                instruction = "Требуется проверка: CCTV, ручной выбор или риск.";
            }

            _hint.text = string.IsNullOrEmpty(_stopHints.Message) ? instruction : _stopHints.Message;

            if (_rejectionRemaining > 0)
            {
                _hint.text = _game.Tutorial.IsComplete ?
                    "Автобус занят или путь перекрыт.\nПопробуйте другой автобус." : _game.Tutorial.Text;
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
                        _arrivals.text = "Далее · " + bus.Route + " · " + bus.Class + " · " + bus.Capacity + " мест";
                    }
                }
            }

            _arrivals.gameObject.SetActive(snapshot.NextBusIds.Count > 0);
            string isolationResult = GetIsolationResult(snapshot.LastIsolationResult);
            _status.text = _game.IsPaused ? "Пауза" : _securityEnabled ?
                "Стабильность " + snapshot.Stability + " · Потери " + lostSeats +
                    (string.IsNullOrEmpty(isolationResult) ? "" : "\n" + isolationResult) : "";
            bool explanation = _game.Tutorial.Step == TutorialStep.ArrivalExplanation;
            _continue.gameObject.SetActive(explanation);
            _victoryPanel.SetActive(snapshot.IsVictory || snapshot.IsDefeat);
            _resultTitle.text = snapshot.IsDefeat ? "Эвакуация прервана" : "Уровень пройден";
            string evacuation = snapshot.LostSeats > 0 ? "Потеряно мест: " + snapshot.LostSeats :
                "Все пассажиры эвакуированы";
            _resultDetails.text = snapshot.IsDefeat ? "Стабильность исчерпана. Повторите попытку." :
                _game.HasNextLevel ? _game.LevelId + " · " + evacuation :
                "LEVEL-01–15 пройдены\n" + evacuation;

            if (snapshot.IsVictory)
            {
                _resultDetails.text += "\nЗвёзды: " + _game.ResultStars + "/3";
            }

            if (snapshot.IsDefeat && string.IsNullOrEmpty(isolationResult) == false)
            {
                _resultDetails.text = isolationResult + "\nСтабильность исчерпана.";
            }
            _next.gameObject.SetActive(snapshot.IsVictory && _game.HasNextLevel);
            _next.interactable = _game.IsPaused == false;
            _restart.interactable = _game.IsPaused == false;
            _repeat.interactable = _game.IsPaused == false;
            _continue.interactable = _game.IsPaused == false;

            if (_securityEnabled)
            {
                _security.Render(snapshot, _game.CanUseSecurity, _game.IsPaused, _game.IsCctvAnomaly);
                bool inspecting = _security.IsInspectionOpen || _security.IsVideo || snapshot.HasSecurityCheck;
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

        private void OnRiskRequested()
        {
            RiskRequested?.Invoke();
        }

        private void OnControlActivated()
        {
            ControlActivated?.Invoke();
        }
    }
}
