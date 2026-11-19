using System;
using LastExit.ArtLab.Preview;
using UnityEngine;

namespace LastExit.Presentation
{
    // Composition and lifetime adapter. Cue routing holds only consumer-specific interfaces.
    public class LevelAudioFeedback : MonoBehaviour
    {
        [SerializeField] private LevelAudioPlayback _playback;
        [SerializeField] private AudioOptionsView _options;
        [SerializeField] private PreviewPointerInput _pointer;
        [SerializeField] private LevelBusDrag _drag;
        [SerializeField] private LevelHud _hud;
        [SerializeField] private LevelSecurityPanel _security;
        [SerializeField] private LevelSequencePlayer _sequence;
        [SerializeField] private QuarantineChamber _chamber;
        private AudioPreferences _preferences;
        private LevelAudioCueRouter _router;
        private bool _sessionEnabled = true;

        public IAudioSettings Settings => _preferences;

        private void OnEnable()
        {
            if (_router != null)
            {
                InitializePlayback();
                _router.Enable();
            }
        }

        private void OnDisable()
        {
            _router?.Disable();
            _playback.ResetTransient();
            _options.Shutdown();
        }

        public void Bind(ILevelAudioReadout game)
        {
            if (game == null)
            {
                throw new ArgumentNullException(nameof(game));
            }

            _router?.Disable();
            _preferences = _preferences ?? new AudioPreferences();
            bool activated = _playback.IsActivated;
            InitializePlayback();

            if (activated)
            {
                _playback.Activate();
            }

            _router = new LevelAudioCueRouter(_playback, _options, _pointer, _drag, _hud,
                _security, _sequence, _chamber, game);
            _router.SetSessionEnabled(_sessionEnabled);

            if (isActiveAndEnabled)
            {
                _router.Enable();
            }
        }

        public void SetSessionEnabled(bool enabled)
        {
            _sessionEnabled = enabled;
            _router?.SetSessionEnabled(enabled);
        }

        public void ActivateFromControl()
        {
            _playback.Activate();
        }

        private void InitializePlayback()
        {
            _playback.Initialize(_preferences);
            _options.Initialize(_preferences);
        }
    }
}
