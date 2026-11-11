using System.Collections;
using UnityEngine;

namespace LastExit.Presentation
{
    // One finite door-entry accent, driven and cancelled by the boarding sequence.
    public class PassengerBoardingFeedback : MonoBehaviour, IPassengerBoardingFeedback
    {
        private const int RingSegments = 24;
        private readonly Vector3[] _points = new Vector3[RingSegments];
        [SerializeField] private LineRenderer _pulse;
        [SerializeField, Min(.05f)] private float _seconds = .24f;
        [SerializeField, Min(.01f)] private float _height = .4f;
        [SerializeField, Min(.01f)] private float _radius = .7f;
        [SerializeField, Min(.01f)] private float _pulseHeight = 1.25f;
        [SerializeField] private Color _color = new Color(.65f, .94f, 1, .9f);
        private Vector3 _origin;
        private bool _paused;

        public bool IsPlaying { get; private set; }

        private void OnDisable()
        {
            ResetFeedback();
        }

        public IEnumerator Play()
        {
            ResetFeedback();
            _origin = transform.position;
            IsPlaying = true;
            _pulse.enabled = true;
            float elapsed = 0;

            while (IsPlaying && (elapsed < _seconds || _paused))
            {
                if (_paused == false)
                {
                    elapsed = Mathf.Min(_seconds, elapsed + Time.deltaTime);
                    float progress = elapsed / _seconds;
                    transform.position = _origin + Vector3.up * (Mathf.Sin(progress * Mathf.PI) * _height);
                    RenderPulse(progress);
                }

                yield return null;
            }

            ResetFeedback();
        }

        public void SetPaused(bool paused)
        {
            _paused = paused;
        }

        public void ResetFeedback()
        {
            if (IsPlaying)
            {
                transform.position = _origin;
                IsPlaying = false;
            }

            // Editor authoring may disable the template before assigning its renderer.
            if (_pulse != null)
            {
                _pulse.enabled = false;
            }
        }

        private void RenderPulse(float progress)
        {
            float radius = Mathf.Lerp(_radius * .65f, _radius, progress);
            Vector3 center = transform.position + new Vector3(0, _pulseHeight, -.12f);

            for (int index = 0; index < RingSegments; index++)
            {
                float angle = index * Mathf.PI * 2 / RingSegments;
                _points[index] = center + new Vector3(Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius, 0);
            }

            Color color = _color;
            color.a *= 1 - progress * progress;
            _pulse.startColor = color;
            _pulse.endColor = color;
            _pulse.SetPositions(_points);
        }
    }
}
