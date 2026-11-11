using System;
using UnityEngine;
using UnityEngine.UI;

namespace LastExit.Presentation
{
    public class AudioOptionsView : MonoBehaviour, IControlActivation, IAudioOptionsLayout
    {
        private const float MinimumScale = 0.001f;
        private const float ButtonHeight = 44;
        private const float PortraitButtonWidth = 44;
        private const float LandscapeButtonWidth = 84;
        private const float PanelWidth = 250;
        private const float PanelHeight = 180;
        private const int ButtonFontSize = 10;

        [SerializeField] private Button _button;
        [SerializeField] private RectTransform _panel;
        [SerializeField] private Toggle _mute;
        [SerializeField] private Slider _atmosphere;
        [SerializeField] private Slider _effects;
        [SerializeField] private Text _label;
        private AudioPreferences _preferences;
        private bool _subscribed;

        public event Action Interacted;

        event Action IControlActivation.ControlActivated
        {
            add => Interacted += value;
            remove => Interacted -= value;
        }

        public RectTransform PointerRegion => _panel;
        public bool IsOpen => _panel != null && _panel.gameObject.activeSelf;

        private void OnEnable()
        {
            Subscribe();
            Synchronize();
        }

        private void OnDisable()
        {
            Unsubscribe();

            if (_panel != null)
            {
                _panel.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        public void Initialize(AudioPreferences preferences)
        {
            if (preferences == null)
            {
                throw new ArgumentNullException(nameof(preferences));
            }

            if (_button == null || _panel == null || _mute == null || _atmosphere == null
                || _effects == null || _label == null)
            {
                throw new InvalidOperationException("Audio options require all authored UI references.");
            }

            Unsubscribe();
            _preferences = preferences;
            _panel.gameObject.SetActive(false);
            _atmosphere.minValue = 0;
            _atmosphere.maxValue = 1;
            _atmosphere.wholeNumbers = false;
            _effects.minValue = 0;
            _effects.maxValue = 1;
            _effects.wholeNumbers = false;

            if (isActiveAndEnabled)
            {
                Subscribe();
            }

            Synchronize();
        }

        public void Shutdown()
        {
            Unsubscribe();
            _preferences = null;

            if (_panel != null)
            {
                _panel.gameObject.SetActive(false);
            }
        }

        public void RefreshLayout(float scale, bool portrait)
        {
            float safeScale = Mathf.Max(MinimumScale, scale);
            RectTransform button = (RectTransform)_button.transform;
            Vector2 buttonAnchor = portrait ? Vector2.one : new Vector2(0.5f, 0);
            button.anchorMin = buttonAnchor;
            button.anchorMax = buttonAnchor;
            button.pivot = new Vector2(0.5f, 0.5f);
            button.anchoredPosition = portrait
                ? new Vector2(-114, -28) / safeScale
                : new Vector2(0, 82) / safeScale;
            float buttonWidth = portrait ? PortraitButtonWidth : LandscapeButtonWidth;
            button.sizeDelta = new Vector2(buttonWidth, ButtonHeight) / safeScale;
            _label.fontSize = Mathf.Max(1, Mathf.RoundToInt(ButtonFontSize / safeScale));
            _panel.anchorMin = Vector2.one;
            _panel.anchorMax = Vector2.one;
            _panel.pivot = Vector2.one;
            _panel.anchoredPosition = portrait
                ? new Vector2(-8, -86) / safeScale
                : new Vector2(-156, -8) / safeScale;
            _panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            _panel.localScale = Vector3.one / safeScale;
        }

        private void Subscribe()
        {
            if (_subscribed || _preferences == null)
            {
                return;
            }

            _button.onClick.AddListener(OnPanelRequested);
            _mute.onValueChanged.AddListener(OnSoundEnabledChanged);
            _atmosphere.onValueChanged.AddListener(OnAtmosphereVolumeChanged);
            _effects.onValueChanged.AddListener(OnEffectsVolumeChanged);
            _preferences.Changed += Synchronize;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (_subscribed == false)
            {
                return;
            }

            _button.onClick.RemoveListener(OnPanelRequested);
            _mute.onValueChanged.RemoveListener(OnSoundEnabledChanged);
            _atmosphere.onValueChanged.RemoveListener(OnAtmosphereVolumeChanged);
            _effects.onValueChanged.RemoveListener(OnEffectsVolumeChanged);
            _preferences.Changed -= Synchronize;
            _subscribed = false;
        }

        private void Synchronize()
        {
            if (_preferences == null)
            {
                return;
            }

            _mute.SetIsOnWithoutNotify(_preferences.IsMuted == false);
            _atmosphere.SetValueWithoutNotify(_preferences.AtmosphereVolume);
            _effects.SetValueWithoutNotify(_preferences.EffectsVolume);
            _label.text = _preferences.IsMuted ? "Тихо" : "Звук";
        }

        private void OnPanelRequested()
        {
            bool open = IsOpen == false;

            if (open)
            {
                _panel.SetAsLastSibling();
            }

            _panel.gameObject.SetActive(open);
            Interacted?.Invoke();
        }

        private void OnSoundEnabledChanged(bool enabled)
        {
            _preferences.SetMuted(enabled == false);
            Interacted?.Invoke();
        }

        private void OnAtmosphereVolumeChanged(float volume)
        {
            _preferences.SetAtmosphereVolume(volume);
            Interacted?.Invoke();
        }

        private void OnEffectsVolumeChanged(float volume)
        {
            _preferences.SetEffectsVolume(volume);
            Interacted?.Invoke();
        }
    }
}
