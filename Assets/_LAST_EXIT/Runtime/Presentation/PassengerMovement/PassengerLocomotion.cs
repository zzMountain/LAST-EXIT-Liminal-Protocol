using UnityEngine;

namespace LastExit.Presentation
{
    // One in-place rig; translation and cadence share the same measured walking speed.
    public class PassengerLocomotion : MonoBehaviour
    {
        private const float ArrivalTolerance = .001f;

        private static readonly int s_idleState = Animator.StringToHash("Idle");
        private static readonly int s_walkState = Animator.StringToHash("Walk");
        private static readonly int s_walkRate = Animator.StringToHash("WalkRate");
        [SerializeField, Min(.1f)] private float _walkingSpeed = 6.5f;
        [SerializeField, Min(.1f)] private float _acceleration = 32;
        [SerializeField, Min(1)] private float _turnDegreesPerSecond = 540;
        [SerializeField, Min(.01f)] private float _blendSeconds = .14f;
        [SerializeField, Min(.1f)] private float _strideLength = 1;
        [SerializeField] private float _modelForwardYaw = 180;
        private Animator _animator;
        private Vector3 _target;
        private float _speed;
        private float _walkClipSeconds;
        private bool _paused;

        public bool IsMoving { get; private set; }

        private void Update()
        {
            if (_paused)
            {
                return;
            }

            Vector3 offset = _target - transform.position;
            float remaining = offset.magnitude;
            float desiredSpeed = Mathf.Min(_walkingSpeed, Mathf.Sqrt(2 * _acceleration * remaining));
            _speed = Mathf.MoveTowards(_speed, desiredSpeed, _acceleration * Time.deltaTime);
            float distance = Mathf.Min(remaining, _speed * Time.deltaTime);

            if (remaining > ArrivalTolerance)
            {
                Vector3 direction = offset / remaining;
                Quaternion facing = Quaternion.LookRotation(direction) * Quaternion.Euler(0, _modelForwardYaw, 0);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, facing,
                    _turnDegreesPerSecond * Time.deltaTime);
                transform.position += direction * distance;
                _animator.SetFloat(s_walkRate, _speed * _walkClipSeconds / _strideLength);
            }

            if (remaining - distance <= ArrivalTolerance)
            {
                transform.position = _target;
                _animator.CrossFadeInFixedTime(s_idleState, _blendSeconds);
                enabled = false;
            }
        }

        private void OnDisable()
        {
            IsMoving = false;
            _speed = 0;
        }

        public void Initialize(Animator animator)
        {
            _animator = animator;
            _animator.applyRootMotion = false;
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name.Contains("Walk"))
                {
                    _walkClipSeconds = clip.length;
                }
            }

            enabled = false;
        }

        public void MoveTo(Vector3 target)
        {
            _target = target;

            if (Vector3.Distance(transform.position, target) <= ArrivalTolerance)
            {
                return;
            }

            if (IsMoving == false)
            {
                _speed = 0;
                _animator.SetFloat(s_walkRate, 0);
                _animator.CrossFadeInFixedTime(s_walkState, _blendSeconds);
            }

            IsMoving = true;
            enabled = true;
        }

        public void SetPaused(bool paused)
        {
            _paused = paused;
            _animator.speed = paused ? 0 : 1;
        }

        public void Stop()
        {
            if (IsMoving)
            {
                _animator.CrossFadeInFixedTime(s_idleState, _blendSeconds);
                enabled = false;
            }
        }
    }
}
