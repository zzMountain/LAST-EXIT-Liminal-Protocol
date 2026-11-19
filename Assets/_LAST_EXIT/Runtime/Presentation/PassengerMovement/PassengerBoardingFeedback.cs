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
        [SerializeField, Min(.05f)] private float _impactSeconds = .12f;
        [SerializeField] private Color _impactColor = new Color(1, .77f, .38f, 1);
        [SerializeField, Min(.01f)] private float _impactWidth = .1f;
        private Vector3 _origin;
        private Vector3 _originScale;
        private Color _pulseStartColor;
        private Color _pulseEndColor;
        private float _pulseStartWidth;
        private float _pulseEndWidth;
        private bool _isImpact;
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
            _originScale = transform.localScale;
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

        public IEnumerator PlayImpact()
        {
            ResetFeedback();
            _origin = transform.position;
            _originScale = transform.localScale;
            _pulseStartColor = _pulse.startColor;
            _pulseEndColor = _pulse.endColor;
            _pulseStartWidth = _pulse.startWidth;
            _pulseEndWidth = _pulse.endWidth;
            _isImpact = true;
            IsPlaying = true;
            _pulse.enabled = true;
            _pulse.startWidth = _impactWidth;
            _pulse.endWidth = _impactWidth;
            float elapsed = 0;

            while (IsPlaying && (elapsed < _impactSeconds || _paused))
            {
                if (_paused == false)
                {
                    elapsed = Mathf.Min(_impactSeconds, elapsed + Time.deltaTime);
                    float progress = elapsed / _impactSeconds;
                    transform.localScale = _originScale * Mathf.Lerp(1, .06f, progress);
                    RenderImpactPulse(progress);
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
                transform.localScale = _originScale;
                IsPlaying = false;
            }

            // Editor authoring may disable the template before assigning its renderer.
            if (_pulse != null)
            {
                if (_isImpact)
                {
                    _pulse.startColor = _pulseStartColor;
                    _pulse.endColor = _pulseEndColor;
                    _pulse.startWidth = _pulseStartWidth;
                    _pulse.endWidth = _pulseEndWidth;
                }

                _pulse.enabled = false;
            }

            _isImpact = false;
        }

        private void RenderImpactPulse(float progress)
        {
            float radius = Mathf.Lerp(_radius * .35f, _radius * 1.6f, progress);
            Vector3 center = _origin + new Vector3(0, _pulseHeight, -.12f);

            for (int index = 0; index < RingSegments; index++)
            {
                float angle = index * Mathf.PI * 2 / RingSegments;
                float tipRadius = index % 2 == 0 ? radius : radius * .55f;
                _points[index] = center + new Vector3(Mathf.Cos(angle) * tipRadius,
                    Mathf.Sin(angle) * tipRadius, 0);
            }

            Color color = _impactColor;
            color.a *= 1 - progress * progress;
            _pulse.startColor = color;
            _pulse.endColor = color;
            _pulse.SetPositions(_points);
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
