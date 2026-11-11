using System;
using LastExit.ArtLab.Preview;
using LastExit.Domain;
using UnityEngine;

namespace LastExit.Presentation
{
    // Maps observable activity to cues; no scene wiring, device sampling or passenger-kind queries.
    public class LevelAudioCueRouter
    {
        private readonly ILevelAudioPlayback _playback;
        private readonly IControlActivation _options;
        private readonly IPointerActivation _pointer;
        private readonly ILevelDragFeedback _drag;
        private readonly IControlActivation _hud;
        private readonly IInspectionVideo _security;
        private readonly IStepCompletion _sequence;
        private readonly IChamberPhases _chamber;
        private readonly IAudioMuteReadout _preferences;
        private readonly ILevelAudioReadout _game;
        private bool _subscribed;
        private bool _sessionEnabled = true;
        private bool _video;
        private bool _securityCheck;
        private bool _victory;
        private bool _defeat;
        private int _stability;
        private int _generation;

        public LevelAudioCueRouter(ILevelAudioPlayback playback, IControlActivation options,
            IPointerActivation pointer, ILevelDragFeedback drag, IControlActivation hud,
            IInspectionVideo security, IStepCompletion sequence, IChamberPhases chamber,
            IAudioMuteReadout preferences, ILevelAudioReadout game)
        {
            _playback = playback ?? throw new ArgumentNullException(nameof(playback));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _pointer = pointer ?? throw new ArgumentNullException(nameof(pointer));
            _drag = drag ?? throw new ArgumentNullException(nameof(drag));
            _hud = hud ?? throw new ArgumentNullException(nameof(hud));
            _security = security ?? throw new ArgumentNullException(nameof(security));
            _sequence = sequence ?? throw new ArgumentNullException(nameof(sequence));
            _chamber = chamber ?? throw new ArgumentNullException(nameof(chamber));
            _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
            _game = game ?? throw new ArgumentNullException(nameof(game));
            CaptureBaseline(game.Snapshot);
            _playback.ResetTransient();
        }

        public void Enable()
        {
            Subscribe();
            OnChanged();
        }

        public void Disable()
        {
            Unsubscribe();
        }

        public void SetSessionEnabled(bool enabled)
        {
            _sessionEnabled = enabled;

            if (enabled == false)
            {
                _playback.ResetTransient();
                _video = false;
            }

            _playback.SetPaused(enabled == false || _game.IsPaused);
            ReportState();
        }

        private void CaptureBaseline(BoardSnapshot snapshot)
        {
            _generation = snapshot.Generation;
            _stability = snapshot.Stability;
            _securityCheck = snapshot.HasSecurityCheck;
            _victory = snapshot.IsVictory;
            _defeat = snapshot.IsDefeat;
            _video = false;
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            _game.Changed += OnChanged;
            _game.StepRequested += OnStepRequested;
            _pointer.Pressed += OnPointerPressed;
            _drag.SelectionChanged += OnSelectionChanged;
            _drag.Moved += OnDragMoved;
            _drag.Released += OnDragReleased;
            _drag.Rejected += OnRejected;
            _hud.ControlActivated += OnControlActivated;
            _options.ControlActivated += OnControlActivated;
            _security.VideoChanged += OnVideoChanged;
            _sequence.Completed += OnStepCompleted;
            _chamber.PhaseChanged += OnChamberPhaseChanged;
            _playback.Played += OnSoundPlayed;
            _preferences.Changed += ReportState;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (_subscribed == false)
            {
                return;
            }

            _game.Changed -= OnChanged;
            _game.StepRequested -= OnStepRequested;
            _pointer.Pressed -= OnPointerPressed;
            _drag.SelectionChanged -= OnSelectionChanged;
            _drag.Moved -= OnDragMoved;
            _drag.Released -= OnDragReleased;
            _drag.Rejected -= OnRejected;
            _hud.ControlActivated -= OnControlActivated;
            _options.ControlActivated -= OnControlActivated;
            _security.VideoChanged -= OnVideoChanged;
            _sequence.Completed -= OnStepCompleted;
            _chamber.PhaseChanged -= OnChamberPhaseChanged;
            _playback.Played -= OnSoundPlayed;
            _preferences.Changed -= ReportState;
            _subscribed = false;
        }

        private void ReportState()
        {
            LevelAudioDiagnostics.ReportState(_playback.IsActivated, _playback.IsPaused,
                _preferences.IsMuted, _playback.ActiveVoiceCount);
        }

        private void OnChanged()
        {
            BoardSnapshot snapshot = _game.Snapshot;
            _playback.SetPaused(_sessionEnabled == false || _game.IsPaused);

            if (snapshot.Generation != _generation)
            {
                _playback.ResetTransient();
                CaptureBaseline(snapshot);
                return;
            }

            if (snapshot.Stability < _stability)
            {
                _playback.Play(LevelSound.StabilityLoss);
            }

            if (snapshot.HasSecurityCheck && _securityCheck == false)
            {
                _playback.Play(LevelSound.SecurityAlert);
            }

            if (snapshot.IsVictory && _victory == false)
            {
                _playback.Play(LevelSound.LevelWin);
            }

            if (snapshot.IsDefeat && _defeat == false)
            {
                _playback.Play(LevelSound.LevelLose);
            }

            _stability = snapshot.Stability;
            _securityCheck = snapshot.HasSecurityCheck;
            _victory = snapshot.IsVictory;
            _defeat = snapshot.IsDefeat;
            ReportState();
        }

        private void OnPointerPressed(Vector2 position)
        {
            _playback.Activate();
            ReportState();
        }

        private void OnControlActivated()
        {
            _playback.Activate();
            _playback.Play(LevelSound.UiClick);
            ReportState();
        }

        private void OnSelectionChanged(string busId)
        {
            if (busId == null)
            {
                _playback.SetLoop(LevelSound.BusDrag, false);
                return;
            }

            _playback.Activate();
            _playback.Play(LevelSound.BusSelect);
        }

        private void OnDragMoved()
        {
            _playback.NotifyDragMotion();
        }

        private void OnDragReleased(bool accepted)
        {
            _playback.SetLoop(LevelSound.BusDrag, false);
            _playback.Play(accepted ? LevelSound.BusSnap : LevelSound.ActionRejected);
        }

        private void OnRejected()
        {
            _playback.Activate();
            _playback.SetLoop(LevelSound.BusDrag, false);
            _playback.Play(LevelSound.ActionRejected);
        }

        private void OnVideoChanged(bool video)
        {
            bool ended = _video && video == false;
            _video = video;
            _playback.SetLoop(LevelSound.ScanLoop, video);

            if (video || ended)
            {
                _playback.Play(video ? LevelSound.ScanStart : LevelSound.ScanEnd);
            }
        }

        private void OnStepRequested(BoardStep step)
        {
            if (step.Kind == BoardStepKind.Arrive)
            {
                _playback.Play(LevelSound.BusArrive);
            }
            else if (step.Kind == BoardStepKind.Depart)
            {
                _playback.Play(LevelSound.BusDepart);
            }
        }

        private void OnStepCompleted(BoardStep step)
        {
            if (step.Kind == BoardStepKind.Board)
            {
                _playback.Play(LevelSound.PassengerBoard);
            }
            else if (step.Kind == BoardStepKind.Arrive)
            {
                _playback.Play(LevelSound.BusStop);
            }
        }

        private void OnChamberPhaseChanged(string phase)
        {
            switch (phase)
            {
                case "Opening": _playback.Play(LevelSound.QuarantineOpen); break;
                case "Incinerating": _playback.Play(LevelSound.QuarantineDischarge); break;
                case "Closing": _playback.Play(LevelSound.QuarantineClose); break;
            }
        }

        private void OnSoundPlayed(LevelSound sound)
        {
            LevelAudioDiagnostics.ReportCue(sound);
            ReportState();
        }
    }
}
