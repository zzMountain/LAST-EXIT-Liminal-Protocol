using UnityEngine;

namespace LastExit.ArtLab.Preview
{
    [ExecuteAlways]
    [RequireComponent(typeof(Canvas), typeof(PreviewHudLayout), typeof(PreviewCanvasScaler))]
    public class ArtLabPreviewHud : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private RectTransform _canvas;
        private IPreviewSelection _selection;
        private IPreviewBusBadge[] _badges;
        private IPreviewLaneGuide[] _lanes;
        private IPreviewHudLayout _layout;
        private IPreviewCameraFraming _framing;
        private PreviewCanvasScaler _scaler;
        private Canvas _rootCanvas;
        private int _selectedIndex = -1;
        private bool _isBound;
        private bool _isSubscribed;

        private void OnEnable()
        {
            if (_isBound)
            {
                Subscribe();
                OnSelectionChanged(_selection.SelectedIndex);
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void LateUpdate()
        {
            // Projection follows camera resizing and continuous X drag. No scene searches or allocations.
            if (_isBound)
            {
                RefreshLayout();
            }
        }

        public void Initialize(Camera camera)
        {
            _camera = camera;
            _canvas = (RectTransform)transform;
            _rootCanvas = GetComponent<Canvas>();
            _scaler = GetComponent<PreviewCanvasScaler>();
        }

        public void Bind(IPreviewSelection selection, IPreviewHudLayout layout, IPreviewCameraFraming framing,
            IPreviewBusBadge[] badges, IPreviewLaneGuide[] lanes)
        {
            Unsubscribe();
            _selection = selection;
            _layout = layout;
            _framing = framing;
            _badges = (IPreviewBusBadge[])badges.Clone();
            _lanes = (IPreviewLaneGuide[])lanes.Clone();
            _isBound = true;

            if (isActiveAndEnabled)
            {
                Subscribe();
            }

            OnSelectionChanged(_selection.SelectedIndex);
        }

        public void RefreshLayout()
        {
            _framing.RefreshFraming();
            _scaler.RefreshScale();
            _layout.Apply(_camera.aspect < 1, _rootCanvas.scaleFactor);

            foreach (IPreviewLaneGuide lane in _lanes)
            {
                lane.Refresh(_camera, _canvas, _selectedIndex);
            }

            foreach (IPreviewBusBadge badge in _badges)
            {
                badge.Refresh(_camera, _canvas);
            }
        }

        private void Subscribe()
        {
            if (_isSubscribed == false)
            {
                _selection.SelectionChanged += OnSelectionChanged;
                _isSubscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (_isSubscribed)
            {
                _selection.SelectionChanged -= OnSelectionChanged;
                _isSubscribed = false;
            }
        }

        private void OnSelectionChanged(int selectedIndex)
        {
            _selectedIndex = selectedIndex;

            for (int index = 0; index < _badges.Length; index++)
            {
                _badges[index].SetSelected(index == selectedIndex);
            }
        }
    }
}
