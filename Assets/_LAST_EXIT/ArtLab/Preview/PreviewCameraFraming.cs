using UnityEngine;

namespace LastExit.ArtLab.Preview
{
    [RequireComponent(typeof(Camera))]
    public class PreviewCameraFraming : MonoBehaviour, IPreviewCameraFraming
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private float _landscapeSize;
        [SerializeField] private float _portraitSize;

        public void Initialize(float landscapeSize, float portraitSize)
        {
            _camera = GetComponent<Camera>();
            _landscapeSize = landscapeSize;
            _portraitSize = portraitSize;
            RefreshFraming();
        }

        public void RefreshFraming()
        {
            if (_camera != null)
            {
                float size = _camera.aspect < 1 ? _portraitSize : _landscapeSize;

                if (Mathf.Approximately(_camera.orthographicSize, size) == false)
                {
                    _camera.orthographicSize = size;
                }
            }
        }
    }
}
