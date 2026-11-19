using System;
using LastExit.Platform;
using UnityEngine;
using UnityEngine.UI;

namespace LastExit.Presentation
{
    [RequireComponent(typeof(Canvas))]
    public class CampaignMenuView : MonoBehaviour
    {
        private RectTransform _canvas;
        private RectTransform _root;
        private RectTransform _card;
        private RectTransform _home;
        private RectTransform _levels;
        private RectTransform _settings;
        private RectTransform _accent;
        private Button _opener;
        private Button _play;
        private Button _levelSelect;
        private Button _settingsSelect;
        private Button _back;
        private Button _russian;
        private Button _english;
        private Button _mute;
        private Text _station;
        private Text _title;
        private Text _subtitle;
        private Text _atmosphereLabel;
        private Text _effectsLabel;
        private Slider _atmosphere;
        private Slider _effects;
        private Button[] _levelButtons;
        private ResultStarGraphic[][] _levelStars;
        private Font _font;
        private IAudioSettings _audio;
        private ILevelSelectionReadout _progress;
        private int _levelCount;
        private int _currentLevelIndex;
        private int _page;
        private float _scale;
        private Vector2 _canvasSize;
        private bool _initialized;
        private bool _subscribed;
        private int _fontWarmupFrames;

        public event Action ResumeRequested;
        public event Action<int> LevelRequested;
        public event Action<bool> VisibilityChanged;

        public bool IsOpen => _initialized && _root.gameObject.activeSelf;
        public RectTransform PointerRegion => _root;
        public RectTransform OpenButtonRegion => (RectTransform)_opener.transform;

        private void OnEnable()
        {
            if (_initialized)
            {
                Subscribe();
                RefreshTexts();
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void LateUpdate()
        {
            if (_initialized)
            {
                RefreshLayout();

                if (IsOpen && _fontWarmupFrames > 0)
                {
                    WarmVisibleText();
                    _fontWarmupFrames = Mathf.Max(0, _fontWarmupFrames - 1);
                }
            }
        }

        public void Initialize(Font font, IAudioSettings audio, ILevelSelectionReadout progress, int levelCount)
        {
            _font = TerminalUiTheme.LoadFont(font);
            _audio = audio;
            _progress = progress;
            _levelCount = levelCount;
            _canvas = (RectTransform)transform;
            _root = CreateRect("Campaign Menu", _canvas);
            Stretch(_root);
            Image backdrop = _root.gameObject.AddComponent<Image>();
            Color backdropColor = TerminalUiTheme.Background;
            backdropColor.a = .96f;
            backdrop.color = backdropColor;
            _card = CreateRect("Menu Card", _root);
            Image cardImage = _card.gameObject.AddComponent<Image>();
            cardImage.color = TerminalUiTheme.Panel;
            Outline border = _card.gameObject.AddComponent<Outline>();
            border.effectColor = TerminalUiTheme.Border;
            border.effectDistance = new Vector2(1, -1);
            _accent = CreateRect("Menu Accent", _card);
            _accent.gameObject.AddComponent<Image>().color = TerminalUiTheme.Accent;
            _station = CreateText("Menu Station", _card, 11, TerminalUiTheme.Accent);
            _title = CreateText("Menu Title", _card, 30, TerminalUiTheme.Text);
            _subtitle = CreateText("Menu Subtitle", _card, 13, TerminalUiTheme.Muted);
            _home = CreateRect("Menu Home", _card);
            _levels = CreateRect("Menu Levels", _card);
            _settings = CreateRect("Menu Settings", _card);
            Stretch(_home);
            Stretch(_levels);
            Stretch(_settings);
            _play = CreateButton("Menu Continue", _home, () => ResumeRequested?.Invoke(), true);
            _levelSelect = CreateButton("Menu Level Select", _home, ShowLevels);
            _settingsSelect = CreateButton("Menu Settings Select", _home, ShowSettings);
            _back = CreateButton("Menu Back", _card, ShowHome);
            _russian = CreateButton("Language Russian", _card, () => GameLocalization.SelectLanguage("ru"));
            _english = CreateButton("Language English", _card, () => GameLocalization.SelectLanguage("en"));
            _mute = CreateButton("Menu Sound", _settings, ToggleMute);
            _atmosphereLabel = CreateText("Menu Atmosphere Label", _settings, 13, TerminalUiTheme.Text);
            _effectsLabel = CreateText("Menu Effects Label", _settings, 13, TerminalUiTheme.Text);
            _atmosphere = CreateSlider("Menu Atmosphere", _settings, SetAtmosphere);
            _effects = CreateSlider("Menu Effects", _settings, SetEffects);
            ConstructLevelButtons();
            _opener = CreateButton("Open Menu", _canvas, ShowHome);
            _opener.GetComponentInChildren<Text>().gameObject.SetActive(false);
            RectTransform glyph = CreateRect("Menu Icon", _opener.transform);
            Stretch(glyph);
            MenuIconGraphic graphic = glyph.gameObject.AddComponent<MenuIconGraphic>();
            graphic.color = TerminalUiTheme.Text;
            graphic.raycastTarget = false;
            _initialized = true;
            _root.gameObject.SetActive(false);
            Subscribe();
            RefreshTexts();
            RefreshLayout();
        }

        public void ShowHome()
        {
            ShowPage(0);
        }

        public void SetCurrentLevel(int levelIndex)
        {
            _currentLevelIndex = levelIndex;
            RefreshTexts();
        }

        public void ShowLevels()
        {
            ShowPage(1);
        }

        public void ShowSettings()
        {
            ShowPage(2);
        }

        public void Hide()
        {
            if (IsOpen == false)
            {
                return;
            }

            _root.gameObject.SetActive(false);
            _opener.gameObject.SetActive(true);
            VisibilityChanged?.Invoke(false);
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            RectTransform rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void ConstructLevelButtons()
        {
            _levelButtons = new Button[_levelCount];
            _levelStars = new ResultStarGraphic[_levelCount][];

            for (int index = 0; index < _levelCount; index++)
            {
                int selected = index;
                Button button = CreateButton("Menu Level " + (index + 1), _levels,
                    () => LevelRequested?.Invoke(selected));
                _levelButtons[index] = button;
                _levelStars[index] = new ResultStarGraphic[3];

                for (int star = 0; star < 3; star++)
                {
                    RectTransform rect = CreateRect("Level Award " + (star + 1), button.transform);
                    _levelStars[index][star] = rect.gameObject.AddComponent<ResultStarGraphic>();
                    _levelStars[index][star].SetAppearance(0, 0);
                }
            }
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            GameLocalization.Changed += RefreshTexts;
            Font.textureRebuilt += OnFontTextureRebuilt;

            if (_audio != null)
            {
                _audio.Changed += RefreshAudio;
            }

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (_subscribed == false)
            {
                return;
            }

            GameLocalization.Changed -= RefreshTexts;
            Font.textureRebuilt -= OnFontTextureRebuilt;

            if (_audio != null)
            {
                _audio.Changed -= RefreshAudio;
            }

            _subscribed = false;
        }

        private void ShowPage(int page)
        {
            bool opened = IsOpen;
            _page = page;
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            _opener.gameObject.SetActive(false);
            _home.gameObject.SetActive(page == 0);
            _levels.gameObject.SetActive(page == 1);
            _settings.gameObject.SetActive(page == 2);
            _back.gameObject.SetActive(page != 0);
            RefreshTexts();
            ApplyLayout();

            if (opened == false)
            {
                VisibilityChanged?.Invoke(true);
            }
        }

        private void RefreshTexts()
        {
            _station.text = "L-07  /  LIMINAL PROTOCOL";
            _title.text = _page == 0 ? "LAST EXIT" :
                GameLocalization.Get(_page == 1 ? "menu.levels" : "menu.settings");
            _subtitle.text = _page == 1 ? GameLocalization.Get("menu.levelHint") :
                _page == 2 ? GameLocalization.Get("settings.summary") :
                GameLocalization.Get("menu.subtitle") + "\n" + GameLocalization.Get("menu.level", _currentLevelIndex + 1);
            SetLabel(_play, GameLocalization.Get("menu.continue"));
            SetLabel(_levelSelect, GameLocalization.Get("menu.levels"));
            SetLabel(_settingsSelect, GameLocalization.Get("menu.settings"));
            SetLabel(_back, "‹  " + GameLocalization.Get("menu.back"));
            SetLabel(_russian, "Русский");
            SetLabel(_english, "English");
            TerminalUiTheme.StyleButton(_russian, GameLocalization.LanguageCode == "ru");
            TerminalUiTheme.StyleButton(_english, GameLocalization.LanguageCode == "en");

            for (int index = 0; index < _levelCount; index++)
            {
                bool unlocked = _progress == null ? index == 0 : _progress.IsLevelUnlocked(index);
                int stars = _progress == null ? 0 : _progress.GetBestStars(index);
                _levelButtons[index].interactable = unlocked;
                SetLabel(_levelButtons[index], (index + 1).ToString("00"));
                _levelButtons[index].GetComponentInChildren<Text>().color = unlocked ? TerminalUiTheme.Text :
                    TerminalUiTheme.Muted;

                for (int star = 0; star < 3; star++)
                {
                    _levelStars[index][star].gameObject.SetActive(unlocked);
                    _levelStars[index][star].SetAppearance(star < stars ? 1 : 0, 0);
                }
            }

            RefreshAudio();
            _fontWarmupFrames = 2;

            if (_scale > 0)
            {
                ApplyLayout();
            }
        }

        private void RefreshAudio()
        {
            bool available = _audio != null;
            _mute.interactable = available;
            _atmosphere.interactable = available;
            _effects.interactable = available;
            SetLabel(_mute, GameLocalization.Get("settings.sound") + "  ·  " +
                GameLocalization.Get(available && _audio.IsMuted ? "settings.off" : "settings.on"));
            _atmosphereLabel.text = GameLocalization.Get("settings.atmosphere");
            _effectsLabel.text = GameLocalization.Get("settings.effects");

            if (available)
            {
                _atmosphere.SetValueWithoutNotify(_audio.AtmosphereVolume);
                _effects.SetValueWithoutNotify(_audio.EffectsVolume);
            }
        }

        private void RefreshLayout()
        {
            float scale = GetComponent<Canvas>().scaleFactor / WebViewportMetrics.RenderDensity;
            Vector2 size = _canvas.rect.size;

            if (Mathf.Approximately(scale, _scale) && size == _canvasSize)
            {
                return;
            }

            _scale = Mathf.Max(.001f, scale);
            _canvasSize = size;
            ApplyLayout();
            _fontWarmupFrames = 2;
        }

        private void WarmVisibleText()
        {
            // WebGL can grow a dynamic atlas after the first English startup geometry was cached.
            // Request the visible glyphs together, then invalidate every UV cache after the last request.
            Text[] labels = _card.GetComponentsInChildren<Text>();

            foreach (Text label in labels)
            {
                if (label.font != null && label.font.dynamic)
                {
                    label.font.RequestCharactersInTexture(label.text, label.fontSize, label.fontStyle);
                }
            }

            foreach (Text label in labels)
            {
                label.cachedTextGenerator.Invalidate();
                label.SetAllDirty();
            }
        }

        private void ApplyLayout()
        {
            float width = Mathf.Min(460, _canvasSize.x * _scale - 24);
            float height = Mathf.Min(520, _canvasSize.y * _scale - 16);
            bool compact = height < 460;
            _card.anchorMin = Vector2.one * .5f;
            _card.anchorMax = _card.anchorMin;
            _card.pivot = Vector2.one * .5f;
            _card.sizeDelta = new Vector2(width, height) / _scale;
            _card.anchoredPosition = Vector2.zero;
            Place(_accent, 0, 1, width, 2);
            Place(_station.rectTransform, 0, compact ? 18 : 30, width - 24, 18);
            Place(_title.rectTransform, 0, compact ? 44 : 78, width - 24, compact ? 32 : 46);
            Place(_subtitle.rectTransform, 0, compact ? 72 : 124, width - 32, compact ? 24 : 36);
            _station.fontSize = Mathf.CeilToInt(10 / _scale);
            _title.fontSize = Mathf.CeilToInt((compact ? 22 : 30) / _scale);
            _subtitle.fontSize = Mathf.CeilToInt((compact ? 11 : 13) / _scale);
            PlaceButton(_play, 0, compact ? 126 : 212, width - 40, compact ? 44 : 52);
            PlaceButton(_levelSelect, compact ? -width * .235f : 0, compact ? 182 : 276,
                compact ? width * .45f : width - 40, 48);
            PlaceButton(_settingsSelect, compact ? width * .235f : 0, compact ? 182 : 338,
                compact ? width * .45f : width - 40, 48);
            PlaceButton(_back, -width * .34f, height - 78, width * .27f, 44);
            SetLabel(_back, "‹  " + GameLocalization.Get("menu.back"));
            PlaceButton(_russian, -width * .235f, height - 27, width * .45f, 44);
            PlaceButton(_english, width * .235f, height - 27, width * .45f, 44);
            PlaceButton(_mute, 0, compact ? 112 : 202, width - 40, 44);
            PlaceAudioRow(_atmosphereLabel, _atmosphere, width, compact ? 160 : 279);
            PlaceAudioRow(_effectsLabel, _effects, width, compact ? 208 : 353);
            // In short windows Back sits beside the title; audio rows retain full touch height.
            if (compact && _page != 0)
            {
                PlaceButton(_back, -width * .39f, 42, width * .18f, 44);
                _back.GetComponentInChildren<Text>().text = "‹";
            }

            int columns = compact ? 5 : 3;
            float gap = 8;
            float tileWidth = (width - 40 - gap * (columns - 1)) / columns;
            float tileHeight = compact ? 44 : 48;
            float gridTop = compact ? 106 : 168;

            for (int index = 0; index < _levelCount; index++)
            {
                float x = (index % columns - (columns - 1) * .5f) * (tileWidth + gap);
                float y = gridTop + index / columns * (tileHeight + gap);
                PlaceButton(_levelButtons[index], x, y, tileWidth, tileHeight);
                Text label = _levelButtons[index].GetComponentInChildren<Text>();
                label.rectTransform.offsetMin = new Vector2(0, tileHeight * .28f / _scale);
                label.rectTransform.offsetMax = Vector2.zero;

                for (int star = 0; star < 3; star++)
                {
                    RectTransform rect = _levelStars[index][star].rectTransform;
                    rect.anchorMin = new Vector2(.5f, 0);
                    rect.anchorMax = rect.anchorMin;
                    rect.anchoredPosition = new Vector2((star - 1) * 15, 11) / _scale;
                    rect.sizeDelta = new Vector2(15, 15) / _scale;
                }
            }

            float screenWidth = _canvasSize.x * _scale;
            bool portrait = TerminalUiTheme.UsesStackedHud(screenWidth, _canvasSize.y * _scale);
            bool shortLandscape = portrait == false && _canvasSize.y * _scale < 560;
            RectTransform opener = (RectTransform)_opener.transform;
            opener.anchorMin = portrait ? new Vector2(.5f, 1) : Vector2.zero;
            opener.anchorMax = opener.anchorMin;
            opener.anchoredPosition = portrait ? new Vector2(0, -28) / _scale :
                new Vector2(TerminalUiTheme.GetSideMargin(screenWidth) +
                    TerminalUiTheme.GetLeftPanelWidth(screenWidth) * .5f,
                    shortLandscape ? 98 : 150) / _scale;
            opener.sizeDelta = new Vector2(portrait ? 44 : shortLandscape ? 84 :
                TerminalUiTheme.GetLeftPanelWidth(screenWidth) - 32, 44) / _scale;
        }

        private void PlaceAudioRow(Text label, Slider slider, float width, float top)
        {
            Place(label.rectTransform, -width * .28f, top, width * .36f, 40);
            Place((RectTransform)slider.transform, width * .21f, top, width * .45f, 44);
            label.fontSize = Mathf.CeilToInt(13 / _scale);
        }

        private void PlaceButton(Button button, float x, float top, float width, float height)
        {
            Place((RectTransform)button.transform, x, top, width, height);
            button.GetComponentInChildren<Text>().fontSize = Mathf.CeilToInt(14 / _scale);
        }

        private void Place(RectTransform rect, float x, float top, float width, float height)
        {
            rect.anchorMin = new Vector2(.5f, 1);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = new Vector2(x, -top) / _scale;
            rect.sizeDelta = new Vector2(width, height) / _scale;
        }

        private Text CreateText(string name, Transform parent, int fontSize, Color color)
        {
            Text text = CreateRect(name, parent).gameObject.AddComponent<Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
        }

        private Button CreateButton(string name, Transform parent, UnityEngine.Events.UnityAction action,
            bool primary = false)
        {
            RectTransform rect = CreateRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Text label = CreateText(name + " Label", rect, 14, TerminalUiTheme.Text);
            Stretch(label.rectTransform);
            TerminalUiTheme.StyleButton(button, primary);
            button.onClick.AddListener(action);
            return button;
        }

        private Slider CreateSlider(string name, Transform parent, UnityEngine.Events.UnityAction<float> changed)
        {
            RectTransform rect = CreateRect(name, parent);
            rect.gameObject.AddComponent<Image>().color = Color.clear;
            Slider slider = rect.gameObject.AddComponent<Slider>();
            RectTransform track = CreateRect("Track", rect);
            track.anchorMin = new Vector2(0, .5f);
            track.anchorMax = new Vector2(1, .5f);
            track.sizeDelta = new Vector2(-18, 2);
            track.gameObject.AddComponent<Image>().color = TerminalUiTheme.Border;
            RectTransform fillArea = CreateRect("Fill Area", track);
            Stretch(fillArea);
            RectTransform fill = CreateRect("Fill", fillArea);
            Stretch(fill);
            fill.gameObject.AddComponent<Image>().color = TerminalUiTheme.Accent;
            RectTransform handleArea = CreateRect("Handle Area", rect);
            Stretch(handleArea);
            handleArea.offsetMin = new Vector2(9, 0);
            handleArea.offsetMax = new Vector2(-9, 0);
            RectTransform handle = CreateRect("Handle", handleArea);
            handle.sizeDelta = new Vector2(12, 24);
            Image handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = TerminalUiTheme.Text;
            slider.targetGraphic = handleImage;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.onValueChanged.AddListener(changed);
            return slider;
        }

        private static void SetLabel(Button button, string value)
        {
            button.GetComponentInChildren<Text>().text = value;
        }

        private void ToggleMute()
        {
            _audio?.SetMuted(_audio.IsMuted == false);
        }

        private void SetAtmosphere(float value)
        {
            _audio?.SetAtmosphereVolume(value);
        }

        private void SetEffects(float value)
        {
            _audio?.SetEffectsVolume(value);
        }

        private void OnFontTextureRebuilt(Font font)
        {
            if (font == _font)
            {
                _fontWarmupFrames = 2;
            }
        }
    }
}
