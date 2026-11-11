using UnityEngine;
using UnityEngine.UI;

namespace LastExit.ArtLab.Preview
{
    [RequireComponent(typeof(RectTransform))]
    public class PreviewBusBadge : MonoBehaviour, IPreviewBusBadge
    {
        [SerializeField] private Image _background;
        [SerializeField] private RectTransform _badge;
        [SerializeField] private RectTransform _door;
        [SerializeField] private GameObject _arrows;
        [SerializeField] private RectTransform _rowLabel;
        [Header("Presentation")]
        [SerializeField] private Color _normalColor = new Color(.09f, .15f, .18f, .86f);
        [SerializeField] private Color _selectedColor = new Color(.24f, .34f, .38f, .96f);
        [SerializeField] private float _badgeHeightAboveRoof = .18f;
        [SerializeField] private float _badgeDepthOffset = -.35f;
        [SerializeField] private float _doorHeightAboveRoof = .28f;
        [SerializeField] private float _doorDepthBeyondBody = .12f;
        [SerializeField] private Vector2 _doorScreenOffset = new Vector2(0, 8);
        [SerializeField] private float _rowLabelX = -8.3f;
        [SerializeField] private float _rowLabelHeight = .1f;
        private IPreviewBusShape _bus;
        private bool _isSelected;
        private bool _styleDirty = true;

        private void OnValidate()
        {
            // Defer Unity object writes until the normal preview refresh, outside validation/import callbacks.
            _styleDirty = true;
        }

        public void Initialize(Image background, RectTransform door,
            GameObject arrows, RectTransform rowLabel)
        {
            _background = background;
            _badge = (RectTransform)transform;
            _door = door;
            _arrows = arrows;
            _rowLabel = rowLabel;
        }

        public void Bind(IPreviewBusShape bus)
        {
            _bus = bus;
            ApplySelection();
        }

        public void Refresh(Camera camera, RectTransform canvas)
        {
            if (_styleDirty)
            {
                ApplySelection();
            }

            Bounds bounds = _bus.GetPhysicalBounds();
            Vector3 center = bounds.center;
            center.y = bounds.max.y + _badgeHeightAboveRoof;
            center.z = bounds.center.z + _badgeDepthOffset;
            Vector2 badgePosition = PreviewScreenProjection.Project(camera, canvas, center);

            if (_badge.anchoredPosition != badgePosition)
            {
                _badge.anchoredPosition = badgePosition;
            }

            Vector3 doorPosition = _bus.DoorPosition;
            doorPosition.y = bounds.max.y + _doorHeightAboveRoof;
            doorPosition.z = bounds.max.z + _doorDepthBeyondBody;
            Vector2 projectedDoor = PreviewScreenProjection.Project(camera, canvas, doorPosition) + _doorScreenOffset;

            if (_door.anchoredPosition != projectedDoor)
            {
                _door.anchoredPosition = projectedDoor;
            }

            Vector2 rowPosition = PreviewScreenProjection.Project(camera, canvas,
                new Vector3(_rowLabelX, _rowLabelHeight, bounds.center.z));

            if (_rowLabel.anchoredPosition != rowPosition)
            {
                _rowLabel.anchoredPosition = rowPosition;
            }
        }

        public void SetSelected(bool isSelected)
        {
            _isSelected = isSelected;
            ApplySelection();
        }

        private void ApplySelection()
        {
            _styleDirty = false;

            if (_door.gameObject.activeSelf != _isSelected)
            {
                _door.gameObject.SetActive(_isSelected);
            }

            if (_arrows.activeSelf != _isSelected)
            {
                _arrows.SetActive(_isSelected);
            }

            Color backgroundColor = _isSelected ? _selectedColor : _normalColor;

            if (_background.color != backgroundColor)
            {
                _background.color = backgroundColor;
            }
        }
    }
}
