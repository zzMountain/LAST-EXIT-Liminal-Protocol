using UnityEngine;

namespace LastExit.Presentation
{
    // Distorts the existing shared model; the passenger root and walking path stay untouched.
    public class PassengerScanDistortion
    {
        private const float PulseFrequency = 31;
        private const float WidthAmplitude = .35f;
        private const float HeightAmplitude = .22f;
        private const float TiltDegrees = 13;
        private readonly Transform _visual;
        private readonly Vector3 _scale;
        private readonly Quaternion _rotation;

        public PassengerScanDistortion(Transform visual)
        {
            _visual = visual;
            _scale = visual.localScale;
            _rotation = visual.localRotation;
        }

        public void SetFrame(bool visible, float elapsed)
        {
            if (_visual == null)
            {
                return;
            }

            float pulse = visible ? Mathf.Sin(elapsed * PulseFrequency + 1) : 0;
            _visual.localScale = Vector3.Scale(_scale,
                new Vector3(1 + WidthAmplitude * pulse, 1 - HeightAmplitude * pulse, 1));
            _visual.localRotation = _rotation * Quaternion.Euler(0, 0, TiltDegrees * pulse);
        }
    }
}
