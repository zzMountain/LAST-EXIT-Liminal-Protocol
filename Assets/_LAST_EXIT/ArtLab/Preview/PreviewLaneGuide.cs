using UnityEngine;
using UnityEngine.UI;

namespace LastExit.ArtLab.Preview
{
    public class PreviewLaneGuide : MonoBehaviour, IPreviewLaneGuide
    {
        [SerializeField] private Color _blockedColor = new Color(.64f, .75f, .78f, .48f);
        [SerializeField] private Color _openColor = new Color(.61f, 1, .72f, .95f);
        [SerializeField] private Vector2 _statusOffset = new Vector2(-88, 46);
        [SerializeField] private string _openPathText = "Путь открыт";
        [SerializeField] private float _laneX;
        [SerializeField] private string _route;
        [SerializeField] private Image _line;
        [SerializeField] private RectTransform _head;
        [SerializeField] private Image _stop;
        [SerializeField] private Text _status;
        [SerializeField] private float _emptyEndZ = -14.2f;
        [SerializeField] private float _bodyClearance = .16f;
        [SerializeField] private float _guideHeight = .08f;
        [SerializeField] private float _queueStartZ = 5.35f;
        [SerializeField] private float _lineWidth = 1.5f;
        [SerializeField] private float _headHeight = .1f;
        [SerializeField] private float _queueHeadZ = 5.95f;
        [SerializeField, Min(.001f)] private float _doorTolerance = .15f;
        private IPreviewBoardBus[] _buses;

        public void Initialize(float laneX, string route, Image line, RectTransform head, Image stop, Text status)
        {
            _laneX = laneX;
            _route = route;
            _line = line;
            _head = head;
            _stop = stop;
            _status = status;
            _status.text = string.Empty;
        }

        public void Bind(IPreviewBoardBus[] buses)
        {
            _buses = (IPreviewBoardBus[])buses.Clone();
        }

        public void Refresh(Camera camera, RectTransform canvas, int selectedIndex)
        {
            int blockerIndex = PreviewBoardGeometry.GetFirstBodyIndex(_buses, _laneX);
            IPreviewBoardBus blocker = blockerIndex < 0 ? null : _buses[blockerIndex];
            bool open = PreviewBoardGeometry.IsDoorAvailable(blocker, _laneX, _route, _doorTolerance);
            float endZ = blocker == null ? _emptyEndZ : blocker.GetPhysicalBounds().max.z + _bodyClearance;
            Vector2 start = PreviewScreenProjection.Project(camera, canvas,
                new Vector3(_laneX, _guideHeight, _queueStartZ));
            Vector2 end = PreviewScreenProjection.Project(camera, canvas, new Vector3(_laneX, _guideHeight, endZ));
            Vector2 delta = end - start;
            RectTransform line = _line.rectTransform;
            Vector2 linePosition = (start + end) * .5f;
            Vector2 lineSize = new Vector2(_lineWidth, delta.magnitude);
            Quaternion lineRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 90);

            if (line.anchoredPosition != linePosition)
            {
                line.anchoredPosition = linePosition;
            }

            if (line.sizeDelta != lineSize)
            {
                line.sizeDelta = lineSize;
            }

            if (line.localRotation != lineRotation)
            {
                line.localRotation = lineRotation;
            }

            Color lineColor = open ? _openColor : _blockedColor;

            if (_line.color != lineColor)
            {
                _line.color = lineColor;
            }

            if (_stop.color != lineColor)
            {
                _stop.color = lineColor;
            }

            if (_stop.rectTransform.anchoredPosition != end)
            {
                _stop.rectTransform.anchoredPosition = end;
            }

            Vector2 headPosition = PreviewScreenProjection.Project(camera, canvas,
                new Vector3(_laneX, _headHeight, _queueHeadZ));

            if (_head.anchoredPosition != headPosition)
            {
                _head.anchoredPosition = headPosition;
            }

            Vector2 statusPosition = end + _statusOffset;

            if (_status.rectTransform.anchoredPosition != statusPosition)
            {
                _status.rectTransform.anchoredPosition = statusPosition;
            }

            bool showStatus = open && blockerIndex == selectedIndex;
            string statusText = showStatus ? _openPathText : string.Empty;

            if (_status.text != statusText)
            {
                _status.text = statusText;
            }
        }
    }
}
