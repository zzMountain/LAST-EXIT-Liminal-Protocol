using System;
using UnityEngine;
using UnityEngine.UI;

namespace LastExit.ArtLab.Preview
{
    [Serializable]
    public class PreviewTextLayout
    {
        [SerializeField] private string _label;
        [SerializeField, TextArea] private string _text;
        [SerializeField, Min(1)] private int _fontSize;
        [SerializeField] private FontStyle _fontStyle;
        [SerializeField] private TextAnchor _alignment;
        [SerializeField] private Color _color;
        [SerializeField] private PreviewRectLayout _rect;

        public PreviewTextLayout(Text source)
        {
            _label = source.name;
            _text = source.text;
            _fontSize = source.fontSize;
            _fontStyle = source.fontStyle;
            _alignment = source.alignment;
            _color = source.color;
            _rect = new PreviewRectLayout(source.rectTransform);
        }

        public void Apply(Text target)
        {
            target.text = _text;
            target.fontSize = _fontSize;
            target.fontStyle = _fontStyle;
            target.alignment = _alignment;
            target.color = _color;
            _rect.Apply(target.rectTransform);
        }
    }
}
