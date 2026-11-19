using System;
using LastExit.Platform;
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
        private IAudioSettings _preferences;
        private Text _heading;
        private Text _soundLabel;
        private Text _atmosphereLabel;
        private Text _effectsLabel;
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

        public void Initialize(IAudioSettings preferences)
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
            _heading = _panel.Find("Громкость")?.GetComponent<Text>();
            _soundLabel = _mute.GetComponentInChildren<Text>(true);
            _atmosphereLabel = _atmosphere.GetComponentInChildren<Text>(true);
            _effectsLabel = _effects.GetComponentInChildren<Text>(true);
            ApplyAppearance();
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
            bool compact = portrait == false && ((RectTransform)GetComponentInParent<Canvas>().transform).rect.height *
                safeScale < 560;
            RectTransform button = (RectTransform)_button.transform;
            Vector2 buttonAnchor = portrait ? Vector2.one : new Vector2(compact ? .25f : .5f, 0);
            button.anchorMin = buttonAnchor;
            button.anchorMax = buttonAnchor;
            button.pivot = new Vector2(0.5f, 0.5f);
            button.anchoredPosition = portrait
                ? new Vector2(-114, -28) / safeScale
                : new Vector2(0, compact ? 30 : 82) / safeScale;
            float panelWidth = ((RectTransform)button.parent).rect.width * safeScale;
            float buttonWidth = portrait ? PortraitButtonWidth : compact ?
                Mathf.Min(LandscapeButtonWidth, panelWidth * .5f - 4) : panelWidth - 32;
            button.sizeDelta = new Vector2(buttonWidth, ButtonHeight) / safeScale;
            _label.fontSize = Mathf.Max(1, Mathf.CeilToInt(ButtonFontSize / safeScale));
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
            GameLocalization.Changed += Synchronize;
            _subscribed = true;
        }

        private void ApplyAppearance()
        {
            TerminalUiTheme.StyleButton(_button);
            Image panelImage = _panel.GetComponent<Image>();

            if (panelImage != null)
            {
                panelImage.color = TerminalUiTheme.Panel;
                Outline border = _panel.GetComponent<Outline>();

                if (border == null)
                {
                    border = _panel.gameObject.AddComponent<Outline>();
                }

                border.effectColor = TerminalUiTheme.Border;
                border.effectDistance = new Vector2(1, -1);
            }

            foreach (Text label in _panel.GetComponentsInChildren<Text>(true))
            {
                TerminalUiTheme.StyleText(label);
            }

            if (_heading != null)
            {
                _heading.color = TerminalUiTheme.Accent;
                _heading.fontStyle = FontStyle.Normal;
            }

            if (_mute.targetGraphic != null)
            {
                _mute.targetGraphic.color = TerminalUiTheme.Background;
            }

            if (_mute.graphic != null)
            {
                _mute.graphic.color = TerminalUiTheme.Accent;
            }
            StyleSlider(_atmosphere);
            StyleSlider(_effects);
        }

        private static void StyleSlider(Slider slider)
        {
            foreach (Image image in slider.GetComponentsInChildren<Image>(true))
            {
                if (image.transform != slider.transform)
                {
                    image.color = image == slider.targetGraphic ? TerminalUiTheme.Accent : TerminalUiTheme.Border;
                }
            }

            ColorBlock colors = slider.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(.75f, .9f, .8f);
            slider.colors = colors;
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
            GameLocalization.Changed -= Synchronize;
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
            _label.text = GameLocalization.Get(_preferences.IsMuted ? "settings.muted" : "settings.sound");
            SetLabel(_heading, "settings.volume");
            SetLabel(_soundLabel, "settings.soundEnabled");
            SetLabel(_atmosphereLabel, "settings.atmosphere");
            SetLabel(_effectsLabel, "settings.effects");
        }

        private static void SetLabel(Text label, string key)
        {
            if (label != null)
            {
                label.text = GameLocalization.Get(key);
            }
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
