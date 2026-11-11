using System;
using System.Collections;
using LastExit.ArtLab.Preview;
using LastExit.Domain;
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
        private Camera _camera;
        private ILevelInspectionBoard _board;
        private bool _checking;
        private bool _paused;
        private bool _focused = true;
        private bool _canUseSecurity;
        private int _generation = -1;
        private int _uses;
        private int _charges;
        private bool _constructed;
        private bool _skipTimerFrame;
        private Coroutine _videoRoutine;

        public event Action ScanRequested;
        public event Action<string> QuarantineRequested;
        public event Action RiskRequested;
        public event Action<bool> SecurityViewingChanged;
        public event Action ControlActivated;
        public event Action<bool> VideoChanged;

        public RectTransform PointerRegion => (RectTransform)_checkPanel.transform;
        public bool IsInspectionOpen => _inspection.IsOpen;
        public bool IsVideo => _inspection.IsVideo;
        public float VideoRemaining => IsVideo ? CctvInspection.VideoDuration - _inspection.Elapsed : 0;
        public string SelectedQueue => _inspection.SelectedQueue;

        private void OnEnable()
        {
            ConstructInspectionControls();
            _scan.onClick.AddListener(OnScanRequested);
            _quarantine[0].onClick.AddListener(OnSelectionRequested);
            _risk.onClick.AddListener(OnRiskRequested);
            _close.onClick.AddListener(CloseInspection);
        }

        private void OnDisable()
        {
            _scan.onClick.RemoveListener(OnScanRequested);
            _quarantine[0].onClick.RemoveListener(OnSelectionRequested);
            _risk.onClick.RemoveListener(OnRiskRequested);
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

        public void Initialize(Camera camera, RectTransform canvas, ILevelInspectionBoard board)
        {
            ResetInspection();
            _camera = camera;
            _canvas = canvas;
            _board = board;
            _generation = -1;
            _uses = 0;
            Array.Clear(_heads, 0, _heads.Length);
        }

        public void Render(BoardSnapshot snapshot, bool canUseSecurity, bool paused, Func<string, bool> anomaly)
        {
            ConstructInspectionControls();
            _canUseSecurity = canUseSecurity;

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

            _checking = snapshot.HasSecurityCheck;
            RenderInspection();
        }

        public void RefreshLayout(float scale, bool portrait)
        {
            ConstructInspectionControls();
            float minimum = _minimumButtonPixels / scale;
            float gap = 8 / scale;
            RectTransform footer = (RectTransform)_scan.transform.parent;
            float width = footer.rect.width;
            float buttonWidth = portrait ? width * .5f - gap * 2 : width - gap * 2;
            SetRect((RectTransform)_scan.transform, new Vector2(portrait ? .25f : .5f, 1),
                new Vector2(0, -minimum * .5f - gap), new Vector2(buttonWidth, minimum));
            SetRect((RectTransform)_quarantine[0].transform, new Vector2(portrait ? .75f : .5f, 1),
                new Vector2(0, -minimum * (portrait ? .5f : 1.5f) - gap * (portrait ? 1 : 2)),
                new Vector2(buttonWidth, minimum));
            // Context actions share the existing hint area; they never cover the playing field.
            float actionY = portrait ? minimum * 1.5f + gap * 2 : minimum * 2.5f + gap * 3;
            float actionWidth = width * .5f - gap * 1.5f;
            SetRect((RectTransform)_close.transform, new Vector2(.25f, 1), new Vector2(0, -actionY),
                new Vector2(actionWidth, minimum));
            SetRect((RectTransform)_risk.transform, new Vector2(.75f, 1), new Vector2(0, -actionY),
                new Vector2(actionWidth, minimum));
            SetRect(_instruction.rectTransform, new Vector2(.5f, 1),
                new Vector2(0, -(portrait ? 130 : 185) / scale), new Vector2(width - gap * 2, 48 / scale));
            _instruction.fontSize = Mathf.CeilToInt(12 / scale);

            foreach (Button button in new[] { _scan, _quarantine[0], _close, _risk })
            {
                button.GetComponentInChildren<Text>().fontSize = Mathf.CeilToInt(12 / scale);
            }

            ProjectTargets(scale);
        }

        public void ProjectTargets(float scale)
        {
            if (_camera == null || _board == null || _canvas == null)
            {
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

        public Button GetScanControl() => _scan;
        public Button GetRiskControl() => _risk;
        public Button GetQuarantineControl() => _quarantine[0];
        public Button GetSelectionControl() => _quarantine[0];
        public Button GetCloseControl() => _close;

        public Button GetQueueControl(string queueId)
        {
            ConstructInspectionControls();
            int index = QueueIds.IndexOf(queueId, StringComparison.Ordinal);

            if (index < 0 || queueId.Length != 1)
            {
                throw new ArgumentException("Expected queue A, B, C or D.", nameof(queueId));
            }

            return _targets[index];
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

            _constructed = true;
            _checkPanel.SetActive(false);
            // The old modal stays inactive. Runtime controls belong to the existing board/footer.
            Canvas canvas = GetComponentInParent<Canvas>();
            _targetRoot = (RectTransform)new GameObject("On-field quarantine targets", typeof(RectTransform)).transform;
            _targetRoot.SetParent(canvas.transform, false);
            _targetRoot.anchorMin = Vector2.zero;
            _targetRoot.anchorMax = Vector2.one;
            _targetRoot.sizeDelta = Vector2.zero;
            RectTransform footer = (RectTransform)_scan.transform.parent;
            _close = CloneButton("Отмена", footer);
            _risk.transform.SetParent(footer, false);
            _instruction = Instantiate(_checkText, footer);
            _instruction.name = "On-field inspection hint";
            _instruction.raycastTarget = false;
            _instruction.alignment = TextAnchor.MiddleCenter;
            _instruction.gameObject.SetActive(false);

            for (int index = 0; index < QueueIds.Length; index++)
            {
                string queueId = QueueIds[index].ToString();
                Button target = CloneButton(queueId, _targetRoot);
                target.name = "Queue head " + queueId;
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
            bool enabled = _canUseSecurity && _paused == false && IsVideo == false;
            _checkPanel.SetActive(false);
            _targetRoot.gameObject.SetActive(choosing && isActiveAndEnabled);
            _scan.interactable = enabled && _charges > 0;
            _scan.GetComponentInChildren<Text>().text = IsVideo
                ? "CCTV · " + Mathf.CeilToInt(VideoRemaining) + " с" : "CCTV · " + _charges;
            _quarantine[0].interactable = enabled && (choosing == false || SelectedQueue != null);
            _quarantine[0].GetComponentInChildren<Text>().text = choosing
                ? SelectedQueue != null ? "В карантин · " + SelectedQueue : "Коснитесь головы"
                : "Выбрать пассажира";
            _close.gameObject.SetActive(choosing);
            _close.interactable = _paused == false;
            _risk.gameObject.SetActive(_checking && IsVideo == false);
            _risk.interactable = enabled;
            _risk.GetComponentInChildren<Text>().text = "Риск −1";
            _instruction.gameObject.SetActive(IsVideo || choosing || _checking);
            _instruction.text = GetInstruction(choosing);
            _instruction.color = _checking ? new Color(.96f, .77f, .36f) : Color.white;

            for (int index = 0; index < _targets.Length; index++)
            {
                _targets[index].gameObject.SetActive(_heads[index] != null);
                _targets[index].interactable = enabled && _heads[index] != null;
                _targets[index].GetComponent<Image>().color = SelectedQueue == QueueIds[index].ToString()
                    ? new Color(.24f, .65f, .65f, .75f) : new Color(.12f, .23f, .25f, .25f);

                if (_heads[index] != null)
                {
                    _board?.SetPassengerScan(_heads[index], IsVideo && _anomalies[index], _inspection.Elapsed);
                }
            }
        }

        private string GetInstruction(bool choosing)
        {
            if (IsVideo)
            {
                return "Идёт сканирование.\nСледите за головами.";
            }

            if (choosing)
            {
                return SelectedQueue == null
                    ? "Выберите голову\nпассажира на поле.\nЗатем «В карантин»."
                    : "Цель: " + SelectedQueue + ".\nНажмите\n«В карантин».";
            }

            return _charges > 0
                ? "Посадка на паузе.\nCCTV, выбор цели\nили риск −1."
                : "Посадка на паузе.\nВыберите пассажира\nили риск −1.";
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
            SecurityViewingChanged?.Invoke(false);
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

        private void OnRiskRequested()
        {
            CloseInspection();
            RiskRequested?.Invoke();
        }
    }
}
