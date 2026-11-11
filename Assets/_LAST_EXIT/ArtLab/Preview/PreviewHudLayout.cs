using UnityEngine;
using UnityEngine.UI;

namespace LastExit.ArtLab.Preview
{
    public class PreviewHudLayout : MonoBehaviour, IPreviewHudLayout
    {
        private const float MinimumCanvasScale = .01f;

        [SerializeField] private RectTransform _header;
        [SerializeField] private RectTransform _footer;
        [SerializeField] private RectTransform _reset;
        [SerializeField, HideInInspector] private Text[] _headerText;
        [SerializeField, HideInInspector] private Text[] _footerText;
        [SerializeField] private PreviewHudLayoutPreset _portraitLayout;
        [SerializeField] private PreviewHudLayoutPreset _landscapeLayout;
        [SerializeField, Tooltip("Disable to edit the UI rectangles directly, then capture them into a layout asset.")]
        private bool _previewInEditMode = true;
        private PreviewHudLayoutPreset _activePreset;
        private int _revision = -1;
        private float _scale;

        public PreviewHudLayoutPreset PortraitLayout => _portraitLayout;
        public PreviewHudLayoutPreset LandscapeLayout => _landscapeLayout;
        public bool PreviewInEditMode => _previewInEditMode;

        private void OnValidate()
        {
            _activePreset = null;
        }

        public void Initialize(RectTransform header, RectTransform footer, RectTransform reset,
            Text[] headerText, Text[] footerText)
        {
            _header = header;
            _footer = footer;
            _reset = reset;
            _headerText = headerText;
            _footerText = footerText;
            _activePreset = null;
        }

        public void SetPresets(PreviewHudLayoutPreset portrait, PreviewHudLayoutPreset landscape)
        {
            _portraitLayout = portrait;
            _landscapeLayout = landscape;
            _activePreset = null;
        }

        public void CaptureTo(PreviewHudLayoutPreset preset)
        {
            preset.Capture(_header, _footer, _reset, _headerText, _footerText);
            _activePreset = null;
        }

        public void Apply(bool portrait, float canvasScale = 1)
        {
            if (Application.isPlaying == false && _previewInEditMode == false)
            {
                return;
            }

            PreviewHudLayoutPreset preset = portrait ? _portraitLayout : _landscapeLayout;

            // Historical scenes without presets retain their saved layout instead of being silently rebuilt.
            if (preset == null)
            {
                return;
            }

            float scale = Mathf.Max(MinimumCanvasScale, canvasScale);

            if (_activePreset == preset && _revision == preset.Revision && Mathf.Approximately(_scale, scale))
            {
                return;
            }

            preset.Apply(_header, _footer, _reset, _headerText, _footerText, scale);
            _activePreset = preset;
            _revision = preset.Revision;
            _scale = scale;
        }
    }
}
