using System;
using System.Collections;
using LastExit.ArtLab.Preview;
using LastExit.Domain;
using LastExit.Platform;
using UnityEngine;
using UnityEngine.UI;

namespace LastExit.Presentation
{
    public class LevelSecurityPanel : MonoBehaviour, IInspectionVideo
    {
        private const string QueueIds = "ABCD";
        private readonly CctvInspection _inspection = new CctvInspection();
        private readonly Button[] _targets = new Button[4];
        private readonly string[] _heads = new string[4];
        private readonly bool[] _anomalies = new bool[4];
        [SerializeField] private Button _scan;
        [SerializeField] private Button[] _quarantine;
        // Retain authored references so existing scenes and authoring commands remain compatible.
        [SerializeField] private GameObject _checkPanel;
        [SerializeField] private RectTransform _checkCard;
        [SerializeField] private Text _checkText;
        [SerializeField] private Button _checkScan;
        [SerializeField] private Button _risk;
        [SerializeField, Min(1)] private float _minimumButtonPixels = 44;
        private Button _close;
        private Text _instruction;
        private RectTransform _targetRoot;
        private RectTransform _canvas;
        private CctvViewfinder _viewfinder;
        private Camera _camera;
        private ILevelInspectionBoard _board;
        private bool _canRequestScan;
        private bool _scanQueued;
        private SecurityTutorial _tutorial;
        private BoardSnapshot _snapshot;
        private bool _paused;
        private bool _focused = true;
        private bool _canUseSecurity;
        private int _generation = -1;
        private int _uses;
        private int _charges;
        private bool _constructed;
        private bool _skipTimerFrame;
        private float _layoutScale = 1;
        private bool _portraitLayout;
        private Coroutine _videoRoutine;

        public event Action ScanRequested;
        public event Action<string> QuarantineRequested;
        public event Action<bool> SecurityViewingChanged;
        public event Action ControlActivated;
        public event Action<bool> VideoChanged;

        public RectTransform PointerRegion => (RectTransform)_checkPanel.transform;
        public bool IsInspectionOpen => _inspection.IsOpen;
        public bool IsVideo => _inspection.IsVideo;
        public float VideoRemaining => IsVideo ? CctvInspection.VideoDuration - _inspection.Elapsed : 0;
        public string SelectedQueue => _inspection.SelectedQueue;
        public bool HasInstruction => _instruction != null && _instruction.gameObject.activeSelf;

        private void OnEnable()
        {
            ConstructInspectionControls();
            GameLocalization.Changed += RenderInspection;
            _scan.onClick.AddListener(OnScanRequested);
            _quarantine[0].onClick.AddListener(OnSelectionRequested);
            _close.onClick.AddListener(CloseInspection);
        }

        private void OnDisable()
        {
            GameLocalization.Changed -= RenderInspection;

            if (_constructed == false)
            {
                return;
            }

            _scan.onClick.RemoveListener(OnScanRequested);
            _quarantine[0].onClick.RemoveListener(OnSelectionRequested);
            _close.onClick.RemoveListener(CloseInspection);
            ResetInspection();
            _targetRoot.gameObject.SetActive(false);
            _instruction.gameObject.SetActive(false);
            _close.gameObject.SetActive(false);
            _risk.gameObject.SetActive(false);
            SecurityViewingChanged?.Invoke(false);
        }

        private void OnApplicationFocus(bool focused)
        {
            _focused = focused;
            _skipTimerFrame = true;
        }

        public void Initialize(Camera camera, RectTransform canvas, ILevelInspectionBoard board,
            SecurityTutorial tutorial = null)
        {
            ResetInspection();
            _camera = camera;
            _canvas = canvas;
            _board = board;
            _tutorial = tutorial;
            _generation = -1;
            _uses = 0;
            Array.Clear(_heads, 0, _heads.Length);
        }

        public void Render(BoardSnapshot snapshot, bool canUseSecurity, bool paused, Func<string, bool> anomaly,
            bool? canRequestScan = null, bool scanQueued = false)
        {
            ConstructInspectionControls();
            _snapshot = snapshot;
            _canUseSecurity = canUseSecurity;
            _canRequestScan = canRequestScan ?? canUseSecurity;
            _scanQueued = scanQueued;

            if (_paused && paused == false)
            {
                _skipTimerFrame = true;
            }

            _paused = paused;
            _charges = snapshot.CctvCharges;

            if (_generation != snapshot.Generation)
            {
                ResetInspection();
                _generation = snapshot.Generation;
                _uses = snapshot.CctvUses;
                SecurityViewingChanged?.Invoke(false);
            }

            for (int index = 0; index < QueueIds.Length; index++)
            {
                string head = GetHead(snapshot, QueueIds[index].ToString());

                if (_heads[index] != head)
                {
                    ClearDistortion(index);
                    _heads[index] = head;
                }
            }

            if (snapshot.CctvUses > _uses)
            {
                _uses = snapshot.CctvUses;

                for (int index = 0; index < QueueIds.Length; index++)
                {
                    _anomalies[index] = anomaly(QueueIds[index].ToString());
                }

                _inspection.PlayVideo();
                VideoChanged?.Invoke(true);
                _skipTimerFrame = true;
                StopVideo();
                _videoRoutine = StartCoroutine(AdvanceVideo());
                SecurityViewingChanged?.Invoke(true);
            }

            RenderInspection();
        }

        public void RefreshLayout(float scale, bool portrait)
        {
            ConstructInspectionControls();
            _layoutScale = scale;
            _portraitLayout = portrait;
            bool compact = portrait == false && _canvas.rect.height * scale < 560;
            float minimum = Mathf.Max(_minimumButtonPixels, portrait ? 48 : compact ? 44 : 52) / scale;
            float gap = 8 / scale;
            RectTransform footer = (RectTransform)_scan.transform.parent;
            float width = footer.rect.width;
            float buttonWidth = portrait ? width * .5f - gap * 2 : width - gap * (compact ? 2 : 4);
            SetRect((RectTransform)_scan.transform, new Vector2(portrait ? .25f : .5f, 1),
                new Vector2(0, -(portrait ? 74 : compact ? 30 : 136) / scale),
                new Vector2(buttonWidth, portrait || compact ? minimum : 56 / scale));
            SetRect((RectTransform)_quarantine[0].transform, new Vector2(portrait ? .75f : .5f, 1),
                new Vector2(0, -(portrait ? 74 : compact ? 82 : 196) / scale),
                new Vector2(buttonWidth, minimum));
            // Context actions share the existing hint area; they never cover the playing field.
            float actionY = (portrait ? 122 : compact ? 134 : 252) / scale;
            SetRect((RectTransform)_close.transform, new Vector2(.5f, 1), new Vector2(0, -actionY),
                new Vector2(width - gap * 2, _minimumButtonPixels / scale));
            LayoutInstruction();

            foreach (Button button in new[] { _scan, _quarantine[0], _close, _risk })
            {
                button.GetComponentInChildren<Text>().fontSize = Mathf.CeilToInt(12 / scale);
            }

            ProjectTargets(scale);
        }

        public void ProjectTargets(float scale)
        {
            if (IsInspectionOpen == false || _camera == null || _board == null || _canvas == null)
            {
                return;
            }

            if (IsVideo)
            {
                _viewfinder.Project(_camera, _canvas, _board, _snapshot.Queues, scale);
                return;
            }

            for (int index = 0; index < _targets.Length; index++)
            {
                if (_heads[index] == null)
                {
                    continue;
                }

                Bounds bounds = _board.GetPassengerBounds(_heads[index]);
                Vector2 center = PreviewScreenProjection.Project(_camera, _canvas, bounds.center);
                float size = _minimumButtonPixels / scale;
                SetRect((RectTransform)_targets[index].transform, new Vector2(.5f, .5f), center,
                    new Vector2(size, size));
                _targets[index].GetComponentInChildren<Text>().fontSize = Mathf.CeilToInt(13 / scale);
            }
        }

        private static string GetHead(BoardSnapshot snapshot, string queueId)
        {
            foreach (QueueSnapshot queue in snapshot.Queues)
            {
                if (queue.Id == queueId && queue.RemainingPassengers.Count > 0)
                {
                    return queue.RemainingPassengers[0].Id;
                }
            }

            return null;
        }

        private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private void ConstructInspectionControls()
        {
            if (_constructed)
            {
                return;
            }

            Canvas canvas = GetComponentInParent<Canvas>();

            if (canvas == null)
            {
                throw new InvalidOperationException("Inspection controls require a parent Canvas.");
            }

            _constructed = true;
            _checkPanel.SetActive(false);
            // The old modal stays inactive. Runtime controls belong to the existing board/footer.
            _targetRoot = (RectTransform)new GameObject("On-field quarantine targets", typeof(RectTransform)).transform;
            _targetRoot.SetParent(canvas.transform, false);
            _targetRoot.anchorMin = Vector2.zero;
            _targetRoot.anchorMax = Vector2.one;
            _targetRoot.sizeDelta = Vector2.zero;
            _viewfinder = new CctvViewfinder((RectTransform)canvas.transform);
            RectTransform footer = (RectTransform)_scan.transform.parent;
            _close = CloneButton(GameLocalization.Get("action.cancel"), footer);
            _risk.transform.SetParent(footer, false);
            _instruction = Instantiate(_checkText, footer);
            _instruction.name = "On-field inspection hint";
            _instruction.raycastTarget = false;
            _instruction.alignment = TextAnchor.MiddleCenter;
            TerminalUiTheme.StyleButton(_scan, true);
            TerminalUiTheme.StyleButton(_quarantine[0]);
            TerminalUiTheme.StyleButton(_close);
            TerminalUiTheme.StyleBodyText(_instruction);
            _instruction.gameObject.SetActive(false);

            for (int index = 0; index < QueueIds.Length; index++)
            {
                string queueId = QueueIds[index].ToString();
                Button target = CloneButton(queueId, _targetRoot);
                target.name = "Queue head " + queueId;
                TerminalUiTheme.StyleText(target.GetComponentInChildren<Text>());
                target.onClick.AddListener(() => SelectTarget(queueId));
                _targets[index] = target;
                target.gameObject.AddComponent<NearestHeadRaycastFilter>().Initialize(target, _targets);
            }

            for (int index = 1; index < _quarantine.Length; index++)
            {
                _quarantine[index].gameObject.SetActive(false);
            }

            _targetRoot.gameObject.SetActive(false);
            _close.gameObject.SetActive(false);
            _risk.gameObject.SetActive(false);
        }

        private Button CloneButton(string label, RectTransform parent)
        {
            Button button = Instantiate(_checkScan, parent);
            button.onClick = new Button.ButtonClickedEvent();
            button.GetComponentInChildren<Text>().text = label;
            button.gameObject.SetActive(true);
            return button;
        }

        private void RenderInspection()
        {
            bool choosing = _inspection.IsOpen && IsVideo == false;
            _viewfinder.Show(VideoRemaining);

            if (_snapshot != null && _paused == false)
            {
                _tutorial?.Observe(_snapshot, IsVideo, choosing);
            }

            bool enabled = _canUseSecurity && _paused == false && IsVideo == false;
            _checkPanel.SetActive(false);
            bool showTargets = choosing && isActiveAndEnabled;
            bool openingTargets = showTargets && _targetRoot.gameObject.activeSelf == false;
            _targetRoot.gameObject.SetActive(showTargets);
            _scan.interactable = _canRequestScan && _paused == false && IsVideo == false && _charges > 0;
            _scan.GetComponentInChildren<Text>().text = IsVideo
                ? GameLocalization.Get("security.timer", Mathf.CeilToInt(VideoRemaining))
                : _scanQueued ? GameLocalization.Get("security.queued") :
                    GameLocalization.Get("security.charges", _charges);
            _quarantine[0].interactable = enabled && (choosing == false || SelectedQueue != null);
            _quarantine[0].GetComponentInChildren<Text>().text = choosing
                ? SelectedQueue != null ? GameLocalization.Get("security.quarantine", SelectedQueue) :
                    GameLocalization.Get("security.touch")
                : GameLocalization.Get("security.select");
            _close.GetComponentInChildren<Text>().text = GameLocalization.Get("action.cancel");
            _close.gameObject.SetActive(choosing);
            _close.interactable = _paused == false;
            _risk.gameObject.SetActive(false);
            _instruction.text = GetInstruction(choosing);
            _instruction.gameObject.SetActive(string.IsNullOrEmpty(_instruction.text) == false);
            LayoutInstruction();

            for (int index = 0; index < _targets.Length; index++)
            {
                _targets[index].gameObject.SetActive(_heads[index] != null);
                _targets[index].interactable = enabled && _heads[index] != null;
                _targets[index].GetComponent<Image>().color = SelectedQueue == QueueIds[index].ToString()
                    ? new Color(.8f, .88f, .87f, .65f) : new Color(.12f, .23f, .2f, .2f);

                if (_heads[index] != null)
                {
                    _board?.SetPassengerScan(_heads[index], IsVideo && _anomalies[index], _inspection.Elapsed);
                }
            }

            if (openingTargets)
            {
                ProjectTargets(_layoutScale);
            }
        }

        private void LayoutInstruction()
        {
            if (_canvas == null)
            {
                return;
            }

            bool anomaly = _tutorial?.ShowsAnomalyHint == true && IsVideo == false && IsInspectionOpen == false;
            bool compact = _portraitLayout == false && _canvas.rect.height * _layoutScale < 560;
            RectTransform footer = (RectTransform)_scan.transform.parent;
            float center = _portraitLayout ? 175 : compact ? 190 : 338;
            float height = _portraitLayout ? 54 : compact ? 60 : 100;

            if (anomaly)
            {
                center = _portraitLayout ? 132 : compact ? 137 : 268;
                height = _portraitLayout || compact ? 48 : 68;
            }

            SetRect(_instruction.rectTransform, new Vector2(.5f, 1),
                new Vector2(0, -center / _layoutScale),
                new Vector2(footer.rect.width - 16 / _layoutScale, height / _layoutScale));
            _instruction.fontSize = Mathf.CeilToInt((compact ? 11 : anomaly ? 14 : 12) / _layoutScale);
            _instruction.fontStyle = anomaly ? FontStyle.Bold : FontStyle.Normal;
            _instruction.alignment = _portraitLayout ? TextAnchor.MiddleCenter : TextAnchor.UpperLeft;
            _instruction.color = anomaly ? new Color(1, .82f, .5f) : TerminalUiTheme.Text;
        }

        private string GetInstruction(bool choosing)
        {
            if (IsVideo)
            {
                return GameLocalization.Get("security.scanning");
            }

            if (choosing)
            {
                if (SelectedQueue == null && _tutorial?.ShowsAnomalyHint == false &&
                    string.IsNullOrEmpty(_tutorial.Text) == false)
                {
                    return _tutorial.Text;
                }

                return SelectedQueue == null
                    ? GameLocalization.Get("security.choose")
                    : GameLocalization.Get("security.confirm", SelectedQueue);
            }

            return _tutorial?.Text ?? string.Empty;
        }

        private void ClearDistortion(int index)
        {
            if (_heads[index] != null)
            {
                _board?.SetPassengerScan(_heads[index], false, 0);
            }
        }

        private void CloseInspection()
        {
            ControlActivated?.Invoke();
            ResetInspection();
            RenderInspection();
            SecurityViewingChanged?.Invoke(false);
        }

        private void SelectTarget(string queueId)
        {
            _inspection.Select(queueId);
            ControlActivated?.Invoke();
            RenderInspection();
        }

        private IEnumerator AdvanceVideo()
        {
            while (IsVideo)
            {
                yield return null;
                _inspection.Advance(Time.unscaledDeltaTime, _paused || _focused == false || _skipTimerFrame);
                _skipTimerFrame = false;
                RenderInspection();
            }

            _videoRoutine = null;
            VideoChanged?.Invoke(false);
            // The scan ends in manual selection; only an explicit close releases the pause.
            SecurityViewingChanged?.Invoke(_inspection.IsOpen);
        }

        private void StopVideo()
        {
            if (_videoRoutine != null)
            {
                StopCoroutine(_videoRoutine);
                _videoRoutine = null;
            }
        }

        private void ResetInspection()
        {
            bool wasVideo = IsVideo;
            StopVideo();
            _inspection.Reset();
            _viewfinder?.Show(0);

            if (wasVideo)
            {
                VideoChanged?.Invoke(false);
            }

            for (int index = 0; index < _heads.Length; index++)
            {
                ClearDistortion(index);
            }
        }

        private void OnScanRequested()
        {
            ControlActivated?.Invoke();
            ScanRequested?.Invoke();
        }

        private void OnSelectionRequested()
        {
            if (IsVideo || _canUseSecurity == false || _paused)
            {
                return;
            }

            if (_inspection.IsOpen && SelectedQueue != null)
            {
                string queueId = SelectedQueue;
                CloseInspection();
                QuarantineRequested?.Invoke(queueId);
                return;
            }

            ControlActivated?.Invoke();
            _inspection.OpenSelection();
            RenderInspection();
            SecurityViewingChanged?.Invoke(true);
        }

    }
}
