using System.Collections.Generic;
using LastExit.ArtLab.Preview;
using LastExit.Content;
using LastExit.Domain;
using LastExit.Platform;
using UnityEngine;

namespace LastExit.Presentation
{
    public class LevelComposition : MonoBehaviour
    {
        [SerializeField] private TextAsset _level;
        [SerializeField] private TextAsset[] _levels;
        [SerializeField] private bool _randomizeArrivals = true;
        [SerializeField] private Camera _camera;
        [SerializeField] private PreviewPointerInput _input;
        [SerializeField] private LevelBoardView _board;
        [SerializeField] private LevelHud _hud;
        [SerializeField] private LevelBusDrag _drag;
        [SerializeField] private LevelSequencePlayer _sequence;
        [SerializeField] private LevelAudioFeedback _audio;
        [SerializeField] private YandexPlatform _platform;
        [SerializeField] private bool _openMenuOnStart = true;
        [SerializeField, Min(0)] private float _boardingBeatSeconds = 1.5f;
        [SerializeField] private StationConditionFeedback _stationCondition;
        private ICampaignPlatform _campaignPlatform;
        private ILevelSelectionReadout _menuProgress;
        private CampaignMenuView _menu;
        private bool _focused = true;
        private bool _applicationPaused;
        private bool _initialized;
        private bool _progressReady;
        private bool _subscribed;
        private bool _restartOnEnable;
        private int _levelIndex;
        private bool _transitioning;

        public LevelGameSession Game { get; private set; }

        private void Awake()
        {
            _campaignPlatform = _platform;
            _menuProgress = _platform;

            if (_campaignPlatform != null)
            {
                _campaignPlatform.ProgressReady += OnProgressReady;
                _campaignPlatform.PauseChanged += ApplyPause;
                _campaignPlatform.Initialize(_levels != null && _levels.Length > 0 ? _levels.Length : 1);
                return;
            }

            if (isActiveAndEnabled)
            {
                InitializeAttempt();
            }
        }

        private void OnEnable()
        {
            if (_initialized == false && (_campaignPlatform == null || _progressReady))
            {
                InitializeAttempt();
            }

            if (_initialized)
            {
                _audio?.SetSessionEnabled(true);
                Subscribe();
                ApplyPause();

                if (_restartOnEnable && _transitioning == false)
                {
                    _restartOnEnable = false;
                    Restart();
                }
                else
                {
                    Game.Publish();
                }

                _campaignPlatform?.MarkSceneReady();
            }
        }

        private void OnDisable()
        {
            _campaignPlatform?.CancelSceneReadiness();

            if (_initialized)
            {
                _audio?.SetSessionEnabled(false);
                // Scene teardown may already have destroyed the camera: detach view callbacks before pausing.
                Unsubscribe();
                ApplyPause();
                _sequence.Cancel();
                _campaignPlatform?.SetGameplayActive(false);
                _restartOnEnable = true;
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();

            if (_menu != null)
            {
                _menu.ResumeRequested -= ResumeFromMenu;
                _menu.LevelRequested -= SelectLevel;
                _menu.VisibilityChanged -= OnMenuVisibilityChanged;
            }

            if (_campaignPlatform != null)
            {
                _campaignPlatform.ProgressReady -= OnProgressReady;
                _campaignPlatform.PauseChanged -= ApplyPause;
                _campaignPlatform.CancelSceneReadiness();
                _campaignPlatform.SetGameplayActive(false);
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            _focused = focused;
            ApplyPause();
        }

        private void OnApplicationPause(bool paused)
        {
            _applicationPaused = paused;
            ApplyPause();
        }

        private void LateUpdate()
        {
            // Run after EventSystem input so a scan clicked on the deadline frame wins over dispatch.
            if (_initialized && Game.IsAwaitingBoardingBeat)
            {
                Game.AdvanceBoardingBeat(Time.unscaledDeltaTime);
            }
        }

        public void Restart()
        {
            _drag.Cancel();
            _sequence.Cancel();
            _board.Clear();
            Game.Restart();
        }

        public void OpenMenu()
        {
            if (_initialized && _transitioning == false)
            {
                _menu.ShowHome();
            }
        }

        public void ResumeFromMenu()
        {
            _audio?.ActivateFromControl();

            if (_initialized && _transitioning == false)
            {
                _menu.Hide();
            }
        }

        public void SelectLevel(int levelIndex)
        {
            int count = _levels != null && _levels.Length > 0 ? _levels.Length : 1;

            if (_initialized == false || _transitioning || levelIndex < 0 || levelIndex >= count ||
                (_menuProgress != null && _menuProgress.IsLevelUnlocked(levelIndex) == false) ||
                (_menuProgress == null && levelIndex != 0))
            {
                return;
            }

            _transitioning = true;
            ApplyPause();

            if (_campaignPlatform != null)
            {
                _campaignPlatform.ShowTransitionAd(() => CompleteLevelTransition(levelIndex));
            }
            else
            {
                CompleteLevelTransition(levelIndex);
            }
        }

        public void NextLevel()
        {
            if (_initialized == false || _transitioning || Game.IsPaused || Game.PendingStep != null || Game.Snapshot.IsVictory == false ||
                Game.HasNextLevel == false)
            {
                return;
            }

            _transitioning = true;
            ApplyPause();

            if (_campaignPlatform != null)
            {
                _campaignPlatform.ShowTransitionAd(CompleteNextLevel);
            }
            else
            {
                CompleteNextLevel();
            }
        }

        private void InitializeAttempt()
        {
            LoadAttempt();
            _initialized = true;

            if (isActiveAndEnabled)
            {
                Subscribe();
            }

            if (_openMenuOnStart)
            {
                _menu.ShowHome();
            }

            ApplyPause();

            if (isActiveAndEnabled)
            {
                _campaignPlatform?.MarkSceneReady();
            }
        }

        private void CompleteNextLevel()
        {
            CompleteLevelTransition(_levelIndex + 1);
        }

        private void CompleteLevelTransition(int levelIndex)
        {
            if (this == null)
            {
                return;
            }

            _drag.Cancel();
            _sequence.Cancel();
            Unsubscribe();
            _hud.Shutdown();
            _board.Clear();
            _levelIndex = levelIndex;
            _campaignPlatform?.SetCurrentLevel(_levelIndex);
            _transitioning = false;
            _restartOnEnable = false;
            LoadAttempt();

            if (isActiveAndEnabled)
            {
                Subscribe();
            }

            _menu.Hide();
            ApplyPause();
            Game.Publish();
        }

        private void LoadAttempt()
        {
            TextAsset content = _levels != null && _levels.Length > 0 ? _levels[_levelIndex] : _level;
            LevelContentSelection selection = LevelContentLoader.Load(content, _randomizeArrivals);
            LevelDefinition definition = selection.Definition;
            Game = new LevelGameSession(definition, _levels != null && _levelIndex + 1 < _levels.Length,
                selection.VariantId, _boardingBeatSeconds);
            // Views bind before the startup/menu pause is resolved. Do not consume first-time cues underneath it.
            Game.SetPaused(true);
            _audio?.Bind(Game);
            _board.Synchronize(Game.Snapshot);
            _hud.Initialize(_camera, Game, _board, _board, definition, Game,
                new SecurityTutorial(definition.Id, _platform));
            InitializeMenu();
            _menu.SetCurrentLevel(_levelIndex);
            List<RectTransform> pointerRegions = new List<RectTransform>(_hud.PointerRegions)
            {
                _menu.PointerRegion, _menu.OpenButtonRegion
            };
            _input.Initialize(_camera, pointerRegions.ToArray());
            _drag.Initialize(_camera, _input, Game, _board, definition);
            _sequence.Initialize(_board, definition.Width);
            InitializeStationCondition(definition.Width);
        }

        private void InitializeMenu()
        {
            if (_menu != null)
            {
                return;
            }

            _menu = gameObject.AddComponent<CampaignMenuView>();
            int count = _levels != null && _levels.Length > 0 ? _levels.Length : 1;
            _menu.Initialize(_hud.MenuFallbackFont, _audio != null ? _audio.Settings : null, _menuProgress, count);
            _menu.ResumeRequested += ResumeFromMenu;
            _menu.LevelRequested += SelectLevel;
            _menu.VisibilityChanged += OnMenuVisibilityChanged;
        }

        private void InitializeStationCondition(int width)
        {
            if (_stationCondition == null)
            {
                return;
            }

            Vector3 center = new Vector3(_board.GetWorldX(width * .5f), 0,
                (_board.QueueHeadZ + _board.RowEndZ) * .5f);
            Vector3 size = new Vector3(width * _board.CellSize, .1f,
                _board.QueueHeadZ - _board.RowEndZ + 3);
            _stationCondition.Initialize(_camera, new Bounds(center, size));
            _stationCondition.SetPaused(true);
            _stationCondition.Synchronize(Game.Snapshot);
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            Game.Changed += OnChanged;
            Game.StepRequested += OnStepRequested;
            _sequence.Completed += Game.Complete;
            _hud.RestartRequested += Restart;
            _hud.ContinueRequested += Game.ContinueTutorial;
            _hud.NextRequested += NextLevel;
            _hud.ScanRequested += Game.Scan;
            _hud.QuarantineRequested += Game.Quarantine;
            _hud.SecurityViewingChanged += OnSecurityViewingChanged;
            _drag.SelectionChanged += _hud.Select;
            _drag.PreviewChanged += _hud.Preview;
            _drag.Rejected += _hud.ShowRejectedMove;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (_subscribed == false)
            {
                return;
            }

            Game.Changed -= OnChanged;
            Game.StepRequested -= OnStepRequested;
            _sequence.Completed -= Game.Complete;
            _hud.RestartRequested -= Restart;
            _hud.ContinueRequested -= Game.ContinueTutorial;
            _hud.NextRequested -= NextLevel;
            _hud.ScanRequested -= Game.Scan;
            _hud.QuarantineRequested -= Game.Quarantine;
            _hud.SecurityViewingChanged -= OnSecurityViewingChanged;
            _drag.SelectionChanged -= _hud.Select;
            _drag.PreviewChanged -= _hud.Preview;
            _drag.Rejected -= _hud.ShowRejectedMove;
            _subscribed = false;
        }

        private void ApplyPause()
        {
            if (_initialized == false)
            {
                return;
            }

            bool paused = isActiveAndEnabled == false || _focused == false || _applicationPaused || _transitioning ||
                (_menu != null && _menu.IsOpen) ||
                (_campaignPlatform != null && _campaignPlatform.IsPaused);

            if (paused)
            {
                _drag.Cancel();
            }

            _sequence.SetPaused(paused);
            _board.SetPaused(paused);
            _stationCondition?.SetPaused(paused);
            Game.SetPaused(paused);
        }

        private void OnProgressReady()
        {
            _campaignPlatform.ProgressReady -= OnProgressReady;
            _progressReady = true;
            _levelIndex = _campaignPlatform.ResumeLevelIndex;

            if (isActiveAndEnabled)
            {
                InitializeAttempt();
            }
        }

        private void OnChanged()
        {
            if (Game.Snapshot.IsVictory)
            {
                BoardSnapshot result = Game.Snapshot;
                _campaignPlatform?.RecordCompletedLevel(new CampaignResult(_levelIndex, Game.ResultStars,
                    result.CctvUses, result.EmergencyResets, result.LostSeats));
            }

            _campaignPlatform?.SetGameplayActive(isActiveAndEnabled && Game.IsPaused == false &&
                Game.IsSecurityViewing == false && Game.Snapshot.IsVictory == false &&
                Game.Snapshot.IsDefeat == false);
            bool motionPaused = Game.IsPaused || Game.IsSecurityViewing;
            _sequence.SetPaused(motionPaused);
            _board.SetPaused(motionPaused);
            _board.Synchronize(Game.Snapshot);
            _stationCondition?.Synchronize(Game.Snapshot);
            _stationCondition?.SetPaused(motionPaused);
            _hud.Refresh();
        }

        private void OnSecurityViewingChanged(bool viewing)
        {
            _drag.Cancel();
            Game.SetSecurityViewing(viewing);
        }

        private void OnMenuVisibilityChanged(bool visible)
        {
            ApplyPause();
        }

        private void OnStepRequested(BoardStep step)
        {
            _sequence.Play(step, Game.Snapshot);
        }
    }
}
