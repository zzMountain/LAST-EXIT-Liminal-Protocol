using System;
using UnityEngine;

namespace LastExit.ArtLab.Preview
{
    public class PreviewBusDrag : MonoBehaviour, IPreviewSelection
    {
        [SerializeField, Min(.01f)] private float _gridStep = 2;
        [SerializeField] private float _minimumX = -9;
        [SerializeField] private float _maximumX = 9;
        private Camera _camera;
        private IPreviewPointerInput _input;
        private IPreviewBusMotion[] _buses;
        private Plane _dragPlane;
        private float _pointerOffset;
        private bool _dragging;
        private bool _subscribed;

        public event Action<int> SelectionChanged;

        public IPreviewBusMotion Selected { get; private set; }
        public int SelectedIndex { get; private set; } = -1;

        private void OnEnable()
        {
            if (_input != null)
            {
                Subscribe();
            }
        }

        private void OnDisable()
        {
            FinishDrag();
            Unsubscribe();
        }

        public void Initialize(Camera camera, IPreviewPointerInput input, IPreviewBusMotion[] buses)
        {
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (buses == null)
            {
                throw new ArgumentNullException(nameof(buses));
            }

            if (_gridStep <= 0 || _minimumX >= _maximumX)
            {
                throw new InvalidOperationException("Drag needs a positive grid step and ordered X boundaries.");
            }

            foreach (IPreviewBusMotion bus in buses)
            {
                if (bus == null)
                {
                    throw new ArgumentException("Every bus motion reference must be assigned.", nameof(buses));
                }

                ValidateGridFit(bus);
            }

            FinishDrag();
            Unsubscribe();
            _camera = camera;
            _input = input;
            _buses = (IPreviewBusMotion[])buses.Clone();
            SetSelection(-1);

            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        public void ResetLayout()
        {
            _dragging = false;

            foreach (IPreviewBusMotion bus in _buses)
            {
                bus.Position = bus.InitialPosition;
            }

            SetSelection(-1);
        }

        private void ValidateGridFit(IPreviewBusMotion bus)
        {
            Bounds bounds = bus.GetPhysicalBounds();
            float minimum = _minimumX - (bounds.min.x - bus.Position.x);
            float maximum = _maximumX - (bounds.max.x - bus.Position.x);
            float first = Mathf.Ceil((minimum - bus.InitialPosition.x) / _gridStep);
            float last = Mathf.Floor((maximum - bus.InitialPosition.x) / _gridStep);

            if (first > last)
            {
                throw new ArgumentException("A bus has no grid position within the configured X boundaries.");
            }
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            _input.Pressed += OnPressed;
            _input.Moved += OnMoved;
            _input.Released += OnReleased;
            _input.Canceled += OnCanceled;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (_subscribed == false)
            {
                return;
            }

            _input.Pressed -= OnPressed;
            _input.Moved -= OnMoved;
            _input.Released -= OnReleased;
            _input.Canceled -= OnCanceled;
            _subscribed = false;
        }

        private void SetSelection(int index)
        {
            Selected = index < 0 ? null : _buses[index];

            if (SelectedIndex == index)
            {
                return;
            }

            SelectedIndex = index;
            SelectionChanged?.Invoke(index);
        }

        private void MoveTo(float desiredX, bool snap)
        {
            Vector3 position = Selected.Position;
            Bounds bounds = Selected.GetPhysicalBounds();
            float minimum = _minimumX - (bounds.min.x - position.x);
            float maximum = _maximumX - (bounds.max.x - position.x);

            if (snap)
            {
                float origin = Selected.InitialPosition.x;
                float first = Mathf.Ceil((minimum - origin) / _gridStep);
                float last = Mathf.Floor((maximum - origin) / _gridStep);
                float cell = Mathf.Clamp(Mathf.Round((desiredX - origin) / _gridStep), first, last);
                desiredX = origin + cell * _gridStep;
            }

            position.x = Mathf.Clamp(desiredX, minimum, maximum);
            Selected.Position = position;
        }

        private bool TryGetDragX(Vector2 position, out float x)
        {
            Ray ray = _camera.ScreenPointToRay(position);

            if (_dragPlane.Raycast(ray, out float distance))
            {
                x = ray.GetPoint(distance).x + _pointerOffset;
                return true;
            }

            x = 0;
            return false;
        }

        private void FinishDrag()
        {
            if (_dragging == false)
            {
                return;
            }

            MoveTo(Selected.Position.x, true);
            _dragging = false;
        }

        private void OnPressed(Vector2 position)
        {
            FinishDrag();
            Ray ray = _camera.ScreenPointToRay(position);
            float nearest = float.MaxValue;
            int selection = -1;

            for (int index = 0; index < _buses.Length; index++)
            {
                if (_buses[index].GetPhysicalBounds().IntersectRay(ray, out float distance) && distance < nearest)
                {
                    nearest = distance;
                    selection = index;
                }
            }

            SetSelection(selection);

            if (Selected == null)
            {
                return;
            }

            _dragPlane = new Plane(Vector3.up, Selected.Position);

            if (_dragPlane.Raycast(ray, out float planeDistance))
            {
                _pointerOffset = Selected.Position.x - ray.GetPoint(planeDistance).x;
                _dragging = true;
            }
        }

        private void OnMoved(Vector2 position)
        {
            if (_dragging && TryGetDragX(position, out float x))
            {
                MoveTo(x, false);
            }
        }

        private void OnReleased(Vector2 position)
        {
            if (_dragging == false)
            {
                return;
            }

            if (TryGetDragX(position, out float x))
            {
                MoveTo(x, true);
                _dragging = false;
            }
            else
            {
                FinishDrag();
            }
        }

        private void OnCanceled()
        {
            FinishDrag();
        }
    }
}

