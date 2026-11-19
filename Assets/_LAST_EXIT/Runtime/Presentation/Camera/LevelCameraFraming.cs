using UnityEngine;

namespace LastExit.Presentation
{
    [RequireComponent(typeof(Camera))]
    public class LevelCameraFraming : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private Vector3 _basePosition;
        [SerializeField, Min(1)] private float _landscapeSize = 10.5f;
        [SerializeField, Min(1)] private float _portraitSize = 12;
        [SerializeField, Min(1)] private float _securityPortraitSize = 14;
        [SerializeField, Min(0)] private float _threeRowPortraitOffset = 1.5f;
        [SerializeField, Min(0)] private float _securityPortraitOffset = 3;
        private int _rows;
        private bool _securityControls;
        private Rect _available = new Rect(0, 0, 1, 1);
        private Bounds _boardBounds;
        private bool _fitBoard;

        public void Configure(int rows, bool securityControls)
        {
            _rows = rows;
            _securityControls = securityControls;
            RefreshFraming();
        }

        public void RefreshFraming()
        {
            if (WebViewportMetrics.HasDrawableCamera(_camera) == false)
            {
                return;
            }

            if (_fitBoard)
            {
                FitBoard();
                return;
            }

            bool portrait = _camera.aspect < 1;
            float size = portrait ? _securityControls ? _securityPortraitSize : _portraitSize : _landscapeSize;
            float offset = portrait ? _securityControls ? _securityPortraitOffset :
                _rows >= 3 ? _threeRowPortraitOffset : 0 : 0;
            _camera.orthographicSize = size;
            _camera.transform.position = _basePosition - _camera.transform.up * offset;
        }

        public bool TrySetAvailableRegion(Rect available, Bounds boardBounds)
        {
            if (WebViewportMetrics.HasDrawableCamera(_camera) == false || WebViewportMetrics.HasArea(available) == false)
            {
                return false;
            }

            _available = available;
            _boardBounds = boardBounds;
            _fitBoard = true;
            RefreshFraming();
            return true;
        }

        private void FitBoard()
        {
            Vector3 right = _camera.transform.right;
            Vector3 up = _camera.transform.up;
            Vector3 extents = _boardBounds.extents;
            float halfWidth = Mathf.Abs(right.x) * extents.x + Mathf.Abs(right.y) * extents.y +
                Mathf.Abs(right.z) * extents.z;
            float halfHeight = Mathf.Abs(up.x) * extents.x + Mathf.Abs(up.y) * extents.y +
                Mathf.Abs(up.z) * extents.z;
            float size = Mathf.Max(halfHeight / Mathf.Max(.1f, _available.height),
                halfWidth / (_camera.aspect * Mathf.Max(.1f, _available.width))) * 1.08f;
            _camera.orthographicSize = Mathf.Max(1, size);
            Vector3 relative = _boardBounds.center - _basePosition;
            float horizontal = Vector3.Dot(relative, right) -
                (_available.center.x - .5f) * 2 * size * _camera.aspect;
            float vertical = Vector3.Dot(relative, up) - (_available.center.y - .5f) * 2 * size;
            _camera.transform.position = _basePosition + right * horizontal + up * vertical;
        }
    }
}
