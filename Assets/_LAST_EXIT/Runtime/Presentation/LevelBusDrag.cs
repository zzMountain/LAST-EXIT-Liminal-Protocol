using System;
using System.Collections.Generic;
using LastExit.ArtLab.Preview;
using LastExit.Domain;
using UnityEngine;

namespace LastExit.Presentation
{
    public class LevelBusDrag : MonoBehaviour, ILevelDragFeedback
    {
        [SerializeField, Range(.5f, .9f)] private float _stopCaptureRadiusCells = .65f;
        [SerializeField, Min(44)] private float _minimumTargetPixels = 44;
        private IReadOnlyDictionary<string, int> _corridors;
        private Camera _camera;
        private IPreviewPointerInput _pointer;
        private ILevelInteraction _game;
        private ILevelBoardInput _board;
        private Plane _plane;
        private string _rowId;
        private string _busId;
        private Transform _actor;
        private Vector3 _origin;
        private float _offset;
        private int _footprint;
        private int _width;
        private bool _dragging;
        private bool _subscribed;
        private int _viewportWidth;
        private int _viewportHeight;
        private int _previewCell;
        private bool _moveBlocked;

        public event Action<string> SelectionChanged;
        public event Action<string, int> PreviewChanged;
        public event Action Moved;
        public event Action<bool> Released;
        public event Action Rejected;

        public string SelectedBusId { get; private set; }

        private void OnEnable()
        {
            if (_pointer != null)
            {
                Subscribe();
            }
        }

        private void OnDisable()
        {
            Cancel();
            Unsubscribe();
        }

        public void Initialize(Camera camera, IPreviewPointerInput pointer, ILevelInteraction game,
            ILevelBoardInput board, LevelDefinition definition)
        {
            Unsubscribe();
            _camera = camera;
            _pointer = pointer;
            _game = game;
            _board = board;
            _width = definition.Width;
            _corridors = definition.Corridors;

            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        public void Cancel()
        {
            if (_dragging)
            {
                _actor.position = _origin;
                _dragging = false;
            }

            SelectedBusId = null;
            _moveBlocked = false;
            SelectionChanged?.Invoke(null);
            PreviewChanged?.Invoke(null, 0);
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            _pointer.Pressed += OnPressed;
            _pointer.Moved += OnMoved;
            _pointer.Released += OnReleased;
            _pointer.Canceled += Cancel;
            _game.Changed += Cancel;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (_subscribed == false)
            {
                return;
            }

            _pointer.Pressed -= OnPressed;
            _pointer.Moved -= OnMoved;
            _pointer.Released -= OnReleased;
            _pointer.Canceled -= Cancel;
            _game.Changed -= Cancel;
            _subscribed = false;
        }

        private bool TryGetX(Vector2 position, out float x)
        {
            Ray ray = _camera.ScreenPointToRay(position);

            if (_plane.Raycast(ray, out float distance))
            {
                x = ray.GetPoint(distance).x + _offset;
                return true;
            }

            x = 0;
            return false;
        }

        private int GetTargetCell(float worldX)
        {
            float cell = _board.GetCellX(worldX);
            int target = Mathf.RoundToInt(cell);

            if (cell < 0 || cell > _width - _footprint)
            {
                return target;
            }

            float nearest = _stopCaptureRadiusCells;

            foreach (KeyValuePair<string, int> lane in _corridors)
            {
                int stop = lane.Value - 1;
                float distance = Mathf.Abs(cell - stop);

                if (stop >= 0 && stop <= _width - _footprint && distance <= nearest)
                {
                    nearest = distance;
                    target = stop;
                }
            }

            return target;
        }

        private bool IsViewportChanged()
        {
            return _viewportWidth != _camera.pixelWidth || _viewportHeight != _camera.pixelHeight;
        }

        private void OnPressed(Vector2 position)
        {
            Cancel();
            Ray ray = _camera.ScreenPointToRay(position);
            float nearest = float.MaxValue;
            float nearestScreen = float.MaxValue;
            RowSnapshot selected = null;
            bool blockedTarget = false;

            foreach (RowSnapshot row in _game.Snapshot.Rows)
            {
                if (row.Bus == null)
                {
                    continue;
                }

                Bounds bounds = _board.GetBusBounds(row.Bus.Id);

                if (_game.CanSelect(row.Bus.Id) == false)
                {
                    blockedTarget |= bounds.IntersectRay(ray) || ContainsTarget(bounds, position);
                    continue;
                }

                if (bounds.IntersectRay(ray, out float distance) && distance < nearest)
                {
                    nearest = distance;
                    selected = row;
                }
                else if (nearest == float.MaxValue && ContainsTarget(bounds, position))
                {
                    Vector2 center = _camera.WorldToScreenPoint(bounds.center);
                    float screenDistance = (position - center).sqrMagnitude;

                    if (screenDistance < nearestScreen)
                    {
                        nearestScreen = screenDistance;
                        selected = row;
                    }
                }
            }

            if (selected == null)
            {
                if (blockedTarget)
                {
                    Rejected?.Invoke();
                }

                return;
            }

            _rowId = selected.Id;
            _busId = selected.Bus.Id;
            _footprint = selected.Bus.FootprintCells;
            _actor = _board.GetBus(_busId);
            _origin = _actor.position;
            _plane = new Plane(Vector3.up, _origin);

            if (_plane.Raycast(ray, out float planeDistance))
            {
                _offset = _origin.x - ray.GetPoint(planeDistance).x;
                _viewportWidth = _camera.pixelWidth;
                _viewportHeight = _camera.pixelHeight;
                _dragging = true;
                SelectedBusId = _busId;
                SelectionChanged?.Invoke(_busId);
                _previewCell = GetTargetCell(_origin.x);
                PreviewChanged?.Invoke(_busId, _previewCell);
            }
        }

        private bool ContainsTarget(Bounds bounds, Vector2 position)
        {
            Vector2 minimum = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 maximum = new Vector2(float.MinValue, float.MinValue);

            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 world = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1,
                        (corner & 4) == 0 ? -1 : 1));
                Vector2 screen = _camera.WorldToScreenPoint(world);
                minimum = Vector2.Min(minimum, screen);
                maximum = Vector2.Max(maximum, screen);
            }

            Vector2 center = (minimum + maximum) * .5f;
            Vector2 size = Vector2.Max(maximum - minimum,
                Vector2.one * (_minimumTargetPixels * WebViewportMetrics.RenderDensity));
            return new Rect(center - size * .5f, size).Contains(position);
        }

        private void OnMoved(Vector2 position)
        {
            if (_dragging && IsViewportChanged())
            {
                Cancel();
                return;
            }

            if (_dragging && TryGetX(position, out float x))
            {
                Vector3 preview = _origin;
                preview.x = Mathf.Clamp(x, _board.GetWorldX(0), _board.GetWorldX(_width - _footprint));

                if (_game.CanMove(_rowId, _busId, _board.GetCellX(preview.x)) == false)
                {
                    if (_moveBlocked == false)
                    {
                        _moveBlocked = true;
                        Rejected?.Invoke();
                    }

                    return;
                }

                _moveBlocked = false;

                if ((_actor.position - preview).sqrMagnitude > .000001f)
                {
                    Moved?.Invoke();
                }

                _actor.position = preview;
                int cell = GetTargetCell(x);

                if (cell != _previewCell)
                {
                    _previewCell = cell;
                    PreviewChanged?.Invoke(_busId, cell);
                }
            }
        }

        private void OnReleased(Vector2 position)
        {
            if (_dragging == false)
            {
                return;
            }

            if (IsViewportChanged())
            {
                Cancel();
                return;
            }

            _dragging = false;
            PreviewChanged?.Invoke(null, 0);

            if (TryGetX(position, out float x) == false)
            {
                _actor.position = _origin;
                Released?.Invoke(false);
                return;
            }

            int cell = GetTargetCell(x);
            Vector3 target = _origin;
            target.x = _board.GetWorldX(cell);
            _actor.position = target;

            bool accepted = _game.TryMove(_rowId, _busId, cell);

            if (accepted == false)
            {
                _actor.position = _origin;
            }

            Released?.Invoke(accepted);
        }
    }
}
