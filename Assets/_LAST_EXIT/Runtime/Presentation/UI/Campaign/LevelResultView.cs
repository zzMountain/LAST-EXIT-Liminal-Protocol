using LastExit.Platform;
using UnityEngine;
using UnityEngine.UI;

namespace LastExit.Presentation
{
    // Owns result decoration, adaptive layout and a finite award animation. Scoring stays in the session.
    public class LevelResultView
    {
        private const int MaximumStars = 3;
        private const float RevealDelay = .18f;
        private const float StarInterval = .24f;
        private const float RevealDuration = .42f;
        private readonly RectTransform _card;
        private readonly Text _title;
        private readonly Text _details;
        private readonly Button _repeat;
        private readonly Button _next;
        private readonly Text _eyebrow;
        private readonly Text _rating;
        private readonly RectTransform _accent;
        private readonly RectTransform _divider;
        private readonly ResultStarGraphic[] _stars = new ResultStarGraphic[MaximumStars];
        private int _awarded = -1;
        private float _elapsed;
        private float _scale = 1;
        private Vector2 _canvasSize;

        public LevelResultView(RectTransform card, Text title, Text details, Button repeat, Button next)
        {
            _card = card;
            _title = title;
            _details = details;
            _repeat = repeat;
            _next = next;
            card.GetComponent<Image>().color = TerminalUiTheme.Panel;
            Outline border = card.gameObject.AddComponent<Outline>();
            border.effectColor = TerminalUiTheme.Border;
            border.effectDistance = new Vector2(1, -1);
            Color backdrop = TerminalUiTheme.Background;
            backdrop.a = .9f;
            card.parent.GetComponent<Image>().color = backdrop;
            TerminalUiTheme.StyleText(_title);
            TerminalUiTheme.StyleText(_details, true);
            _eyebrow = CreateLabel("Result Station", GameLocalization.Get("result.eyebrow"), TerminalUiTheme.Accent);
            _rating = CreateLabel("Result Rating", string.Empty, TerminalUiTheme.Accent);
            _accent = CreateLine("Result Accent", TerminalUiTheme.Accent);
            _divider = CreateLine("Result Divider", TerminalUiTheme.Border);

            for (int index = 0; index < _stars.Length; index++)
            {
                GameObject star = new GameObject("Result Star " + (index + 1), typeof(RectTransform));
                star.transform.SetParent(card, false);
                _stars[index] = star.AddComponent<ResultStarGraphic>();
                _stars[index].SetAppearance(0, 0);
            }

            _title.fontStyle = FontStyle.Normal;
            _title.resizeTextForBestFit = false;
            _details.resizeTextForBestFit = false;
            TerminalUiTheme.StyleButton(_next, true);
            TerminalUiTheme.StyleButton(_repeat);
        }

        public void SetResult(int awarded)
        {
            _eyebrow.text = GameLocalization.Get("result.eyebrow");
            _rating.text = GameLocalization.Get(awarded == MaximumStars ? "result.ratingBest" :
                awarded == 2 ? "result.ratingGood" : "result.ratingComplete");

            if (_awarded == awarded)
            {
                return;
            }

            _awarded = awarded;
            _elapsed = 0;

            foreach (ResultStarGraphic star in _stars)
            {
                star.gameObject.SetActive(awarded > 0);
                star.rectTransform.localScale = Vector3.one;
                star.SetAppearance(0, 0);
            }

            if (_canvasSize != Vector2.zero)
            {
                ApplyLayout(_scale, _canvasSize);
            }
        }

        public void Tick(float deltaTime)
        {
            float end = RevealDelay + StarInterval * (MaximumStars - 1) + RevealDuration;

            if (_awarded <= 0 || _elapsed >= end || deltaTime <= 0)
            {
                return;
            }

            _elapsed = Mathf.Min(end, _elapsed + deltaTime);

            for (int index = 0; index < _awarded; index++)
            {
                float progress = Mathf.Clamp01((_elapsed - RevealDelay - index * StarInterval) / RevealDuration);
                float pulse = Mathf.Sin(progress * Mathf.PI);
                _stars[index].SetAppearance(Mathf.SmoothStep(0, 1, progress), pulse);
                _stars[index].rectTransform.localScale = Vector3.one * (1 + pulse * .16f);
            }
        }

        public void ApplyLayout(float scale, Vector2 canvasSize)
        {
            _scale = scale;
            _canvasSize = canvasSize;
            float width = Mathf.Min(canvasSize.x * scale - 24, 420);
            float height = Mathf.Min(canvasSize.y * scale - 16, 400);
            bool compact = height < 400;
            bool victory = _awarded > 0;
            _card.sizeDelta = new Vector2(width, height) / scale;
            SetRect(_eyebrow.rectTransform, 0, 20, width - 32, 18);
            SetRect(_title.rectTransform, 0, compact ? 44 : 52, width - 24, compact ? 28 : 34);
            SetRect(_accent, 0, 1, width - 2, 2);
            SetRect(_divider, 0, height - 119, width - 40, 1);
            _divider.gameObject.SetActive(compact == false);
            _eyebrow.fontSize = Mathf.CeilToInt(10 / scale);
            _title.fontSize = Mathf.CeilToInt((compact ? 18 : 23) / scale);
            _details.fontSize = Mathf.CeilToInt(13 / scale);
            _rating.fontSize = Mathf.CeilToInt(14 / scale);
            _rating.gameObject.SetActive(victory && compact == false);
            SetRect(_rating.rectTransform, 0, 205, width - 32, 24);
            SetRect(_details.rectTransform, 0, victory ? compact ? 153 : 241 : height * .4f,
                width - 40, victory ? compact ? 32 : 40 : compact ? 64 : 66);
            _details.alignment = TextAnchor.MiddleCenter;
            _title.alignment = TextAnchor.MiddleCenter;

            for (int index = 0; index < _stars.Length; index++)
            {
                bool center = index == 1;
                float size = compact ? center ? 62 : 54 : center ? 92 : 80;
                float spacing = Mathf.Min(90, (width - 64) / 3);
                SetRect(_stars[index].rectTransform, (index - 1) * spacing,
                    (compact ? 100 : 134) + (center ? compact ? -4 : -6 : 5), size, size);
            }

            SetRect((RectTransform)_next.transform, 0, height - 86, width - 40, 46);
            SetRect((RectTransform)_repeat.transform, 0,
                height - (_next.gameObject.activeSelf ? 32 : 56), width - 40, 46);
            _next.GetComponentInChildren<Text>().fontSize = Mathf.CeilToInt(15 / scale);
            _repeat.GetComponentInChildren<Text>().fontSize = Mathf.CeilToInt(14 / scale);
        }

        private Text CreateLabel(string name, string value, Color color)
        {
            GameObject label = new GameObject(name, typeof(RectTransform));
            label.transform.SetParent(_card, false);
            Text text = label.AddComponent<Text>();
            text.font = _details.font;
            text.text = value;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
        }

        private RectTransform CreateLine(string name, Color color)
        {
            GameObject line = new GameObject(name, typeof(RectTransform));
            line.transform.SetParent(_card, false);
            Image image = line.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image.rectTransform;
        }

        private void SetRect(RectTransform rect, float x, float top, float width, float height)
        {
            rect.anchorMin = new Vector2(.5f, 1);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(x, -top) / _scale;
            rect.sizeDelta = new Vector2(width, height) / _scale;
        }
    }
}
