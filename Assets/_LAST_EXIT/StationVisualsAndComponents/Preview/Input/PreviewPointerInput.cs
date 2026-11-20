using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace LastExit.ArtLab.Preview
{
    // Device sampling and gesture ownership only. No references to moving or selectable objects.
    public class PreviewPointerInput : MonoBehaviour, IPreviewPointerInput
    {
        private Camera _camera;
        private RectTransform[] _uiRegions;
        private InputDevice _gestureDevice;
        private Mouse _gestureMouse;
        private TouchControl _gestureTouch;
        private Vector2 _lastPosition;
        private int _touchId;
        private bool _initialized;

        public event Action<Vector2> Pressed;
        public event Action<Vector2> Moved;
        public event Action<Vector2> Released;
        public event Action Canceled;

        private void Update()
        {
            if (_initialized == false)
            {
                return;
            }

            if (_gestureDevice == null)
            {
                ReadAvailablePresses();
            }

            if (_gestureDevice != null)
            {
                ReadGesture();
            }
        }

        private void OnDisable()
        {
            CancelGesture();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused == false)
            {
                CancelGesture();
            }
        }

        public void Initialize(Camera camera, RectTransform[] uiRegions)
        {
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            if (uiRegions == null)
            {
                throw new ArgumentNullException(nameof(uiRegions));
            }

            foreach (RectTransform region in uiRegions)
            {
                if (region == null)
                {
                    throw new ArgumentException("Every pointer-blocking UI region must be assigned.",
                        nameof(uiRegions));
                }
            }

            CancelGesture();
            _camera = camera;
            _uiRegions = (RectTransform[])uiRegions.Clone();
            _initialized = true;
        }

        private void ReadAvailablePresses()
        {
            Touchscreen touchscreen = Touchscreen.current;

            if (touchscreen != null && touchscreen.enabled)
            {
                foreach (TouchControl touch in touchscreen.touches)
                {
                    if (touch.press.wasPressedThisFrame == false)
                    {
                        continue;
                    }

                    Vector2 position = touch.position.ReadValue();

                    if (IsOverUi(position))
                    {
                        continue;
                    }

                    _gestureDevice = touchscreen;
                    _gestureTouch = touch;
                    _touchId = touch.touchId.ReadValue();
                    _lastPosition = position;
                    Pressed?.Invoke(position);
                    return;
                }
            }

            Mouse mouse = Mouse.current;

            if (mouse == null || mouse.enabled == false || mouse.leftButton.wasPressedThisFrame == false)
            {
                return;
            }

            Vector2 mousePosition = mouse.position.ReadValue();

            if (IsOverUi(mousePosition))
            {
                return;
            }

            _gestureDevice = mouse;
            _gestureMouse = mouse;
            _lastPosition = mousePosition;
            Pressed?.Invoke(mousePosition);
        }

        private void ReadGesture()
        {
            if (_gestureDevice.added == false || _gestureDevice.enabled == false)
            {
                CancelGesture();
                return;
            }

            if (_gestureMouse != null)
            {
                ReadMouse();
                return;
            }

            ReadTouch();
        }

        private void ReadMouse()
        {
            Vector2 position = _gestureMouse.position.ReadValue();

            if (_gestureMouse.leftButton.wasReleasedThisFrame)
            {
                ReleaseGesture(position);
            }
            else if (_gestureMouse.leftButton.isPressed)
            {
                PublishMovement(position);
            }
            else
            {
                CancelGesture();
            }
        }

        private void ReadTouch()
        {
            if (_gestureTouch.touchId.ReadValue() != _touchId)
            {
                CancelGesture();
                return;
            }

            InputTouchPhase phase = _gestureTouch.phase.ReadValue();
            Vector2 position = _gestureTouch.position.ReadValue();

            if (phase == InputTouchPhase.Ended)
            {
                ReleaseGesture(position);
            }
            else if (phase == InputTouchPhase.Canceled || phase == InputTouchPhase.None)
            {
                CancelGesture();
            }
            else
            {
                PublishMovement(position);
            }
        }

        private void PublishMovement(Vector2 position)
        {
            if (position == _lastPosition)
            {
                return;
            }

            _lastPosition = position;
            Moved?.Invoke(position);
        }

        private bool IsOverUi(Vector2 position)
        {
            foreach (RectTransform region in _uiRegions)
            {
                if (region.gameObject.activeInHierarchy &&
                    RectTransformUtility.RectangleContainsScreenPoint(region, position, _camera))
                {
                    return true;
                }
            }

            return false;
        }

        private void ReleaseGesture(Vector2 position)
        {
            ClearGesture();
            Released?.Invoke(position);
        }

        private void CancelGesture()
        {
            if (_gestureDevice == null)
            {
                return;
            }

            ClearGesture();
            Canceled?.Invoke();
        }

        private void ClearGesture()
        {
            _gestureDevice = null;
            _gestureMouse = null;
            _gestureTouch = null;
            _touchId = 0;
        }
    }
}
