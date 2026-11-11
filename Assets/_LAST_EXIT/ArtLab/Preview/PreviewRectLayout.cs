using System;
using UnityEngine;

namespace LastExit.ArtLab.Preview
{
    [Serializable]
    public class PreviewRectLayout
    {
        [SerializeField] private Vector2 _anchorMin;
        [SerializeField] private Vector2 _anchorMax;
        [SerializeField] private Vector2 _pivot;
        [SerializeField] private Vector2 _position;
        [SerializeField] private Vector2 _size;

        public PreviewRectLayout(RectTransform source)
        {
            _anchorMin = source.anchorMin;
            _anchorMax = source.anchorMax;
            _pivot = source.pivot;
            _position = source.anchoredPosition;
            _size = source.sizeDelta;
        }

        public void Apply(RectTransform target)
        {
            target.anchorMin = _anchorMin;
            target.anchorMax = _anchorMax;
            target.pivot = _pivot;
            target.anchoredPosition = _position;
            target.sizeDelta = _size;
        }
    }
}
