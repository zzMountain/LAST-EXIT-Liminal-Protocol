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
        private ICampaignPlatform _campaignPlatform;
        private bool _focused = true;
        private bool _applicationPaused;
        private bool _initialized;
        private bool _progressReady;
        private bool _subscribed;
        private bool _restartOnEnable;
        private int _levelIndex;
        private bool _transitioning;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private int _diagnosticWidth;
        private int _diagnosticHeight;
        private int _diagnosticFrames;
#endif

        public LevelGameSession Game { get; private set; }

        private void Awake()
        {
            _campaignPlatform = _platform;

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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void LateUpdate()
        {
            // Two frames cover camera/HUD LateUpdate order after an actual viewport change.
            if (_diagnosticWidth != _camera.pixelWidth || _diagnosticHeight != _camera.pixelHeight)
            {
                _diagnosticWidth = _camera.pixelWidth;
                _diagnosticHeight = _camera.pixelHeight;
                _diagnosticFrames = 2;
            }

            if (_initialized && (Game.IsSecurityViewing || Game.BoardingSteps.Count > 0 || _diagnosticFrames > 0))
            {
                LevelDiagnostics.Report(Game, _camera, _board, _hud);
                _diagnosticFrames = Mathf.Max(0, _diagnosticFrames - 1);
            }
        }
#endif

        public void Restart()
        {
            _drag.Cancel();
            _sequence.Cancel();
            _board.Clear();
            Game.Restart();
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

            ApplyPause();

            if (isActiveAndEnabled)
            {
                _campaignPlatform?.MarkSceneReady();
            }
        }

        private void CompleteNextLevel()
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
            _levelIndex++;
            _campaignPlatform?.SetCurrentLevel(_levelIndex);
            _transitioning = false;
            _restartOnEnable = false;
            LoadAttempt();

            if (isActiveAndEnabled)
            {
                Subscribe();
            }

            ApplyPause();
            Game.Publish();
        }

        private void LoadAttempt()
        {
            TextAsset content = _levels != null && _levels.Length > 0 ? _levels[_levelIndex] : _level;
            LevelContentSelection selection = LevelContentLoader.Load(content, _randomizeArrivals);
            LevelDefinition definition = selection.Definition;
            Game = new LevelGameSession(definition, _levels != null && _levelIndex + 1 < _levels.Length,
                selection.VariantId);
            _audio?.Bind(Game);
            _board.Synchronize(Game.Snapshot);
            _hud.Initialize(_camera, Game, _board, _board, definition, Game);
            _input.Initialize(_camera, _hud.PointerRegions);
            _drag.Initialize(_camera, _input, Game, _board, definition);
            _sequence.Initialize(_board, definition.Width);
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
            _hud.RiskRequested += Game.AcceptRisk;
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
            _hud.RiskRequested -= Game.AcceptRisk;
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
                (_campaignPlatform != null && _campaignPlatform.IsPaused);

            if (paused)
            {
                _drag.Cancel();
            }

            _sequence.SetPaused(paused);
            _board.SetPaused(paused);
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
            _hud.Refresh();
            LevelDiagnostics.Report(Game, _camera, _board, _hud);
        }

        private void OnSecurityViewingChanged(bool viewing)
        {
            _drag.Cancel();
            Game.SetSecurityViewing(viewing);
        }

        private void OnStepRequested(BoardStep step)
        {
            _sequence.Play(step, Game.Snapshot);
        }
    }
}
