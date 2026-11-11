using System;
using System.Collections;
using UnityEngine;

namespace LastExit.Presentation
{
    // A command-driven presentation. No autonomous preview animation or model mutation.
    public class QuarantineChamber : MonoBehaviour, IQuarantinePlayback, IChamberPhases
    {
        private const string ClosedPhase = "Closed";
        private const string ApproachPhase = "Approach";
        private const string ThresholdPhase = "Threshold";
        private const string OpeningPhase = "Opening";
        private const string EnteringPhase = "Entering";
        private const string IncineratingPhase = "Incinerating";
        private const string ClosingPhase = "Closing";

        [SerializeField] private Transform _leaf;
        [SerializeField] private Transform _threshold;
        [SerializeField] private Transform _inside;
        [SerializeField] private Light _flash;
        [SerializeField] private ParticleSystem _sparks;
        [SerializeField] private ParticleSystem _ash;
        [SerializeField] private Vector3 _closedPosition;
        [SerializeField] private Vector3 _openOffset = new Vector3(1.18f, 0, 0);
        [SerializeField, Min(.01f)] private float _openingSeconds = .3f;
        [SerializeField, Min(.01f)] private float _effectSeconds = .6f;
        [SerializeField, Min(.01f)] private float _closingSeconds = .3f;
        [SerializeField, Min(.01f)] private float _thresholdStopSeconds = .1f;
        [SerializeField, Min(0)] private float _flashIntensity = 7;
        private bool _paused;

        public event Action<string> PhaseChanged;

        public string Phase { get; private set; } = ClosedPhase;
        public float Progress { get; private set; }
        public Vector3 ThresholdPosition => _threshold.position;

        private void Awake()
        {
            ResetState();
        }

        private void OnDisable()
        {
            ResetState();
        }

        public void SetPaused(bool paused)
        {
            _paused = paused;
        }

        public void ResetState()
        {
            _leaf.localPosition = _closedPosition;
            _flash.intensity = 0;
            _sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _ash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            SetPhase(ClosedPhase);
            Progress = 0;
        }

        public IEnumerator Execute(IQuarantineBoard board, string passengerId)
        {
            Transform passenger = board.GetPassenger(passengerId);
            SetPhase(ApproachPhase);
            Vector3 threshold = _threshold.position;
            threshold.y = passenger.position.y;
            yield return board.WalkPassenger(passengerId, threshold);
            yield return AwaitResume();
            SetPhase(ThresholdPhase);
            yield return Advance(_thresholdStopSeconds);
            SetPhase(OpeningPhase);
            yield return Advance(_openingSeconds);
            SetPhase(EnteringPhase);
            Vector3 inside = _inside.position;
            inside.y = passenger.position.y;
            yield return board.WalkPassenger(passengerId, inside);
            yield return AwaitResume();
            SetPhase(IncineratingPhase);
            _sparks.Play();
            _ash.Play();
            // Simulate explicitly so focus pause freezes both lifetime and motion.
            _sparks.Pause();
            _ash.Pause();
            yield return Advance(_effectSeconds);
            passenger.gameObject.SetActive(false);
            _flash.intensity = 0;
            SetPhase(ClosingPhase);
            yield return Advance(_closingSeconds);
            ResetState();
        }

        private IEnumerator Advance(float duration)
        {
            float elapsed = 0;
            Progress = 0;

            while (elapsed < duration)
            {
                if (_paused == false)
                {
                    float delta = Mathf.Min(Time.deltaTime, duration - elapsed);
                    elapsed += delta;
                    Progress = elapsed / duration;

                    if (Phase == OpeningPhase || Phase == ClosingPhase)
                    {
                        float amount = Phase == OpeningPhase ? Progress : 1 - Progress;
                        _leaf.localPosition = _closedPosition + _openOffset * Mathf.SmoothStep(0, 1, amount);
                    }
                    else if (Phase == IncineratingPhase)
                    {
                        _flash.intensity = _flashIntensity * Mathf.Pow(1 - Progress, 2);
                        _sparks.Simulate(delta, true, false);
                        _ash.Simulate(delta, true, false);
                    }
                }

                yield return null;
            }

            yield return AwaitResume();
        }

        private void SetPhase(string phase)
        {
            if (Phase == phase)
            {
                return;
            }

            Phase = phase;
            PhaseChanged?.Invoke(phase);
        }

        private IEnumerator AwaitResume()
        {
            while (_paused)
            {
                yield return null;
            }
        }
    }
}
