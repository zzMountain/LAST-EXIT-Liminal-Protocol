using System;
using UnityEngine;
using UnityEngine.UI;

namespace LastExit.ArtLab.Preview
{
    [CreateAssetMenu(menuName = "LAST EXIT/Preview HUD Layout")]
    public class PreviewHudLayoutPreset : ScriptableObject
    {
        private const float MinimumCanvasScale = .01f;

        [SerializeField] private PreviewRectLayout _header;
        [SerializeField] private PreviewRectLayout _footer;
        [SerializeField] private PreviewRectLayout _reset;
        [SerializeField] private PreviewTextLayout[] _headerText;
        [SerializeField] private PreviewTextLayout[] _footerText;
        [SerializeField, Min(1)] private float _minimumButtonPixels = 44;

        public int Revision { get; private set; }

        private void OnValidate()
        {
            Revision++;
        }

        public void Capture(RectTransform header, RectTransform footer, RectTransform reset,
            Text[] headerText, Text[] footerText)
        {
            _header = new PreviewRectLayout(header);
            _footer = new PreviewRectLayout(footer);
            _reset = new PreviewRectLayout(reset);
            _headerText = CaptureText(headerText);
            _footerText = CaptureText(footerText);
            Revision++;
        }

        public void Apply(RectTransform header, RectTransform footer, RectTransform reset,
            Text[] headerText, Text[] footerText, float scale)
        {
            if (_header == null || _footer == null || _reset == null || _headerText == null ||
                _footerText == null || _headerText.Length != headerText.Length ||
                _footerText.Length != footerText.Length)
            {
                throw new InvalidOperationException("HUD layout preset does not match its scene bindings. " +
                    "Capture it from the HUD Inspector.");
            }

            _header.Apply(header);
            _footer.Apply(footer);
            _reset.Apply(reset);
            float minimumHeight = _minimumButtonPixels / Mathf.Max(MinimumCanvasScale, scale);
            reset.sizeDelta = new Vector2(reset.sizeDelta.x, Mathf.Max(reset.sizeDelta.y, minimumHeight));

            for (int index = 0; index < headerText.Length; index++)
            {
                _headerText[index].Apply(headerText[index]);
            }

            for (int index = 0; index < footerText.Length; index++)
            {
                _footerText[index].Apply(footerText[index]);
            }
        }

        private static PreviewTextLayout[] CaptureText(Text[] source)
        {
            PreviewTextLayout[] result = new PreviewTextLayout[source.Length];

            for (int index = 0; index < source.Length; index++)
            {
                result[index] = new PreviewTextLayout(source[index]);
            }

            return result;
        }
    }
}
