using System;
using System.Collections;
using UnityEngine;

namespace LastExit.Presentation
{
    public class LevelAudioPlayback : MonoBehaviour, ILevelAudioPlayback
    {
        private const int LoopCount = 3;
        private const int VoiceCount = 8;
        private const float DragTailSeconds = 0.12f;

        private readonly bool[] _wantedLoops = new bool[LoopCount];
        private readonly bool[] _pausedLoops = new bool[LoopCount];
        private readonly bool[] _runningLoops = new bool[LoopCount];
        private readonly float[] _voiceVolumes = new float[VoiceCount];
        private readonly float[] _lastPlayed = new float[(int)LevelSound.Count];
        [SerializeField] private LevelAudioLibrary _library;
        [SerializeField] private AudioSource[] _loops = new AudioSource[LoopCount];
        [SerializeField] private AudioSource[] _voices = new AudioSource[VoiceCount];
        [SerializeField, Min(0)] private float _minimumRepeatSeconds = 0.08f;
        private IAudioLevels _levels;
        private bool _subscribed;
        private int _nextVoice;
        private float _dragRemaining;
        private Coroutine _dragTailRoutine;

        public bool IsPaused { get; private set; }
        public bool IsActivated { get; private set; }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            CancelDragTail();
            StopAllSources();
            Array.Clear(_wantedLoops, 0, LoopCount);
            Array.Clear(_pausedLoops, 0, LoopCount);
            Array.Clear(_runningLoops, 0, LoopCount);
            IsActivated = false;
            IsPaused = false;
        }

        public void Initialize(IAudioLevels levels)
        {
            if (levels == null)
            {
                throw new ArgumentNullException(nameof(levels));
            }

            ValidateSources();
            CancelDragTail();
            Unsubscribe();
            StopAllSources();
            _levels = levels;
            IsActivated = false;
            IsPaused = false;
            _nextVoice = 0;
            Array.Clear(_wantedLoops, 0, LoopCount);
            Array.Clear(_pausedLoops, 0, LoopCount);
            Array.Clear(_runningLoops, 0, LoopCount);

            for (int index = 0; index < _lastPlayed.Length; index++)
            {
                _lastPlayed[index] = float.NegativeInfinity;
            }

            for (int index = 0; index < LoopCount; index++)
            {
                _loops[index].playOnAwake = false;
                _loops[index].loop = true;
                _loops[index].clip = _library.GetCue((LevelSound)index).Clip;
            }

            foreach (AudioSource voice in _voices)
            {
                voice.playOnAwake = false;
                voice.loop = false;
            }

            if (isActiveAndEnabled)
            {
                Subscribe();
            }

            ApplyLevels();
        }

        // The caller invokes this only in response to a real pointer/UI gesture.
        public void Activate()
        {
            if (_levels == null || isActiveAndEnabled == false)
            {
                return;
            }

            IsActivated = true;
            _wantedLoops[(int)LevelSound.Atmosphere] = true;
            ApplyLevels();
        }

        public void Play(LevelSound sound)
        {
            if (CanEmit() == false)
            {
                return;
            }

            if (IsLoop(sound))
            {
                SetLoop(sound, true);
                return;
            }

            LevelAudioCue cue = _library.GetCue(sound);
            float volume = cue.Volume * _levels.EffectsVolume;
            int soundIndex = (int)sound;
            float now = Time.unscaledTime;

            if (volume <= 0 || now - _lastPlayed[soundIndex] < _minimumRepeatSeconds)
            {
                return;
            }

            int voiceIndex = GetVoiceIndex();
            AudioSource voice = _voices[voiceIndex];
            voice.Stop();
            voice.clip = cue.Clip;
            voice.volume = volume;
            _voiceVolumes[voiceIndex] = cue.Volume;
            voice.Play();
            _lastPlayed[soundIndex] = now;
            _nextVoice = (voiceIndex + 1) % VoiceCount;
        }

        public void SetLoop(LevelSound sound, bool wanted)
        {
            if (IsLoop(sound) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(sound), "Only the first three cues are loops.");
            }

            int index = (int)sound;

            if (sound == LevelSound.BusDrag && wanted == false)
            {
                CancelDragTail();
            }

            _wantedLoops[index] = wanted;

            if (_levels != null)
            {
                RefreshLoop(index);
            }
        }

        public void SetPaused(bool paused)
        {
            if (IsPaused == paused)
            {
                return;
            }

            IsPaused = paused;

            if (paused)
            {
                SetLoop(LevelSound.BusDrag, false);
                StopVoices();

                for (int index = 0; index < LoopCount; index++)
                {
                    if (_loops[index] != null && _loops[index].isPlaying)
                    {
                        _loops[index].Pause();
                        _pausedLoops[index] = true;
                    }
                }
            }
            else if (_levels != null)
            {
                ApplyLevels();
            }
        }

        public void ResetTransient()
        {
            StopVoices();
            SetLoop(LevelSound.BusDrag, false);
            SetLoop(LevelSound.ScanLoop, false);
        }

        public void NotifyDragMotion()
        {
            if (CanEmit() == false)
            {
                return;
            }

            _dragRemaining = DragTailSeconds;
            SetLoop(LevelSound.BusDrag, true);

            if (_dragTailRoutine == null)
            {
                _dragTailRoutine = StartCoroutine(AwaitDragTail());
            }
        }

        private static bool IsLoop(LevelSound sound)
        {
            return sound >= LevelSound.Atmosphere && (int)sound < LoopCount;
        }

        private IEnumerator AwaitDragTail()
        {
            while (_dragRemaining > 0)
            {
                yield return null;
                _dragRemaining = Mathf.Max(0, _dragRemaining - Time.unscaledDeltaTime);
            }

            _dragTailRoutine = null;
            SetLoop(LevelSound.BusDrag, false);
        }

        private void CancelDragTail()
        {
            if (_dragTailRoutine != null)
            {
                StopCoroutine(_dragTailRoutine);
                _dragTailRoutine = null;
            }

            _dragRemaining = 0;
        }

        private void ValidateSources()
        {
            if (_library == null || _loops == null || _loops.Length != LoopCount
                || _voices == null || _voices.Length != VoiceCount)
            {
                throw new InvalidOperationException("Audio playback requires a library, three loops and eight voices.");
            }

            foreach (AudioSource source in _loops)
            {
                if (source == null)
                {
                    throw new InvalidOperationException("Audio loop source is not assigned.");
                }
            }

            foreach (AudioSource source in _voices)
            {
                if (source == null)
                {
                    throw new InvalidOperationException("Audio voice source is not assigned.");
                }
            }
        }

        private bool CanEmit()
        {
            return _levels != null && isActiveAndEnabled && IsActivated && IsPaused == false
                && _levels.IsMuted == false;
        }

        private void Subscribe()
        {
            if (_subscribed || _levels == null)
            {
                return;
            }

            _levels.Changed += ApplyLevels;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (_subscribed == false)
            {
                return;
            }

            _levels.Changed -= ApplyLevels;
            _subscribed = false;
        }

        private void ApplyLevels()
        {
            for (int index = 0; index < VoiceCount; index++)
            {
                _voices[index].volume = _voiceVolumes[index] * _levels.EffectsVolume;
            }

            if (_levels.IsMuted)
            {
                StopVoices();
            }

            for (int index = 0; index < LoopCount; index++)
            {
                RefreshLoop(index);
            }
        }

        private void RefreshLoop(int index)
        {
            AudioSource source = _loops[index];
            LevelSound sound = (LevelSound)index;
            LevelAudioCue cue = _library.GetCue(sound);
            float level = sound == LevelSound.Atmosphere ? _levels.AtmosphereVolume : _levels.EffectsVolume;
            source.volume = level * cue.Volume;

            if (_wantedLoops[index] == false || IsActivated == false || _levels.IsMuted || source.volume <= 0)
            {
                source.Stop();
                _pausedLoops[index] = false;
                _runningLoops[index] = false;
                return;
            }

            if (IsPaused || isActiveAndEnabled == false)
            {
                return;
            }

            if (_pausedLoops[index])
            {
                source.UnPause();
                _pausedLoops[index] = false;
            }
            else if (_runningLoops[index] == false)
            {
                source.Play();
                _runningLoops[index] = true;
            }
        }

        private int GetVoiceIndex()
        {
            for (int offset = 0; offset < VoiceCount; offset++)
            {
                int index = (_nextVoice + offset) % VoiceCount;

                if (_voices[index].isPlaying == false)
                {
                    return index;
                }
            }

            return _nextVoice;
        }

        private void StopVoices()
        {
            foreach (AudioSource voice in _voices)
            {
                if (voice != null)
                {
                    voice.Stop();
                }
            }
        }

        private void StopAllSources()
        {
            StopVoices();

            foreach (AudioSource source in _loops)
            {
                if (source != null)
                {
                    source.Stop();
                }
            }
        }
    }
}
