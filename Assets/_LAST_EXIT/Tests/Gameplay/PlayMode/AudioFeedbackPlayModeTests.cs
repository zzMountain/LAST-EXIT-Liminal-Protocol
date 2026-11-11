using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using LastExit.ArtLab.Preview;
using LastExit.Content;
using LastExit.Domain;
using LastExit.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace LastExit.Tests.Gameplay.PlayMode
{
    public class AudioFeedbackPlayModeTests
    {
        private const string MutedKey = "LastExit.Audio.Muted";
        private const string AtmosphereKey = "LastExit.Audio.Atmosphere";
        private const string EffectsKey = "LastExit.Audio.Effects";
        private const int LoopCount = 3;
        private const int VoiceCount = 8;

        private readonly List<LevelSound> _emissions = new List<LevelSound>();
        private GameObject _root;
        private GameObject _optionsRoot;
        private LevelAudioLibrary _library;
        private LevelAudioPlayback _playback;
        private AudioSource[] _loops;
        private AudioSource[] _voices;
        private TestAudioLevels _levels;
        private bool _hadMuted;
        private bool _hadAtmosphere;
        private bool _hadEffects;
        private int _savedMuted;
        private float _savedAtmosphere;
        private float _savedEffects;

        [UnitySetUp]
        public IEnumerator LoadCatalogAndCreateIsolatedPlayback()
        {
            _hadMuted = PlayerPrefs.HasKey(MutedKey);
            _hadAtmosphere = PlayerPrefs.HasKey(AtmosphereKey);
            _hadEffects = PlayerPrefs.HasKey(EffectsKey);
            _savedMuted = PlayerPrefs.GetInt(MutedKey);
            _savedAtmosphere = PlayerPrefs.GetFloat(AtmosphereKey);
            _savedEffects = PlayerPrefs.GetFloat(EffectsKey);
            _emissions.Clear();
            yield return SceneManager.LoadSceneAsync("StationL07");

            foreach (GameObject sceneRoot in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                LevelAudioPlayback authored = sceneRoot.GetComponentInChildren<LevelAudioPlayback>(true);

                if (authored != null)
                {
                    _library = ReadField<LevelAudioLibrary>(authored, "_library");
                }
            }

            Assert.IsNotNull(_library, "StationL07 must contain its authored audio library.");
            _root = new GameObject("Isolated Audio Feedback Test");
            _root.SetActive(false);
            _playback = _root.AddComponent<LevelAudioPlayback>();
            _loops = CreateSources("Loop", LoopCount);
            _voices = CreateSources("Voice", VoiceCount);
            WriteField(_playback, "_library", _library);
            WriteField(_playback, "_loops", _loops);
            WriteField(_playback, "_voices", _voices);
            _levels = new TestAudioLevels();
            _root.SetActive(true);
            _playback.Initialize(_levels);
            _playback.Played += RecordEmission;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_playback != null)
            {
                _playback.Played -= RecordEmission;
            }

            if (_optionsRoot != null)
            {
                UnityEngine.Object.Destroy(_optionsRoot);
            }

            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
            }

            if (_hadMuted)
            {
                PlayerPrefs.SetInt(MutedKey, _savedMuted);
            }
            else
            {
                PlayerPrefs.DeleteKey(MutedKey);
            }

            RestoreFloat(AtmosphereKey, _hadAtmosphere, _savedAtmosphere);
            RestoreFloat(EffectsKey, _hadEffects, _savedEffects);
            PlayerPrefs.Save();
            yield return null;
            _root = null;
            _optionsRoot = null;
            _playback = null;
            _library = null;
        }

        [UnityTest]
        public IEnumerator AuthoredCatalogIsCompleteAndPlaybackWaitsForActivation()
        {
            for (int index = 0; index < (int)LevelSound.Count; index++)
            {
                Assert.IsNotNull(_library.GetCue((LevelSound)index).Clip);
            }

            _playback.Play(LevelSound.UiClick);
            _playback.SetLoop(LevelSound.ScanLoop, true);
            yield return null;
            Assert.IsFalse(_playback.IsActivated);
            Assert.AreEqual(0, _playback.EmissionCount);
            Assert.AreEqual(0, _playback.ActiveVoiceCount);
            Assert.IsFalse(_loops[0].isPlaying);
            Assert.IsFalse(_loops[2].isPlaying);

            _playback.SetLoop(LevelSound.ScanLoop, false);
            _playback.Activate();
            _playback.Activate();
            yield return null;
            Assert.IsTrue(_playback.IsActivated);
            Assert.IsTrue(_loops[0].isPlaying);
            CollectionAssert.AreEqual(new[] { LevelSound.Atmosphere }, _emissions);
        }

        [UnityTest]
        public IEnumerator MutedPlaybackIgnoresShotsAndResumesWantedLoopsOnlyOnce()
        {
            _levels.Set(true, 0.32f, 0.8f);
            _playback.Activate();
            _playback.SetLoop(LevelSound.ScanLoop, true);
            _playback.Play(LevelSound.UiClick);
            Assert.AreEqual(0, _playback.EmissionCount);
            Assert.AreEqual(0, _playback.ActiveVoiceCount);

            _levels.Set(false, 0.32f, 0.8f);
            _playback.SetLoop(LevelSound.ScanLoop, true);
            _playback.SetLoop(LevelSound.ScanLoop, true);
            _playback.Activate();
            yield return null;
            CollectionAssert.AreEqual(new[] { LevelSound.Atmosphere, LevelSound.ScanLoop }, _emissions);
            Assert.IsTrue(_loops[0].isPlaying);
            Assert.IsTrue(_loops[2].isPlaying);

            _levels.Set(true, 0.32f, 0.8f);
            _playback.Play(LevelSound.SecurityAlert);
            Assert.IsFalse(_loops[0].isPlaying);
            Assert.IsFalse(_loops[2].isPlaying);
            Assert.AreEqual(2, _playback.EmissionCount);
        }

        [UnityTest]
        public IEnumerator DragLoopStopsAfterMotionTailWithoutRestartingOnEveryMove()
        {
            _playback.Activate();

            for (int index = 0; index < 30; index++)
            {
                _playback.NotifyDragMotion();
            }

            Assert.AreEqual(2, _playback.EmissionCount);
            Assert.IsTrue(_loops[1].isPlaying);
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.IsFalse(_loops[1].isPlaying, "Stationary selection must not leave friction playing.");
            Assert.IsTrue(_loops[0].isPlaying);
            Assert.AreEqual(2, _playback.EmissionCount);
        }

        [UnityTest]
        public IEnumerator ResetTransientStopsVoicesDragAndScanButRetainsAtmosphere()
        {
            _playback.Activate();
            _playback.NotifyDragMotion();
            _playback.SetLoop(LevelSound.ScanLoop, true);
            _playback.Play(LevelSound.LevelLose);
            yield return null;
            Assert.Greater(_playback.ActiveVoiceCount, 0);
            _playback.ResetTransient();
            yield return null;
            Assert.AreEqual(0, _playback.ActiveVoiceCount);
            Assert.IsFalse(_loops[1].isPlaying);
            Assert.IsFalse(_loops[2].isPlaying);
            Assert.IsTrue(_loops[0].isPlaying);
        }

        [UnityTest]
        public IEnumerator DistinctCueBurstUsesBoundedVoicesAndSameCueStormIsCoalesced()
        {
            _playback.Activate();

            for (int index = (int)LevelSound.UiClick; index < (int)LevelSound.Count; index++)
            {
                _playback.Play((LevelSound)index);
            }

            Assert.AreEqual(VoiceCount, _playback.ActiveVoiceCount);
            int emitted = _playback.EmissionCount;

            for (int index = 0; index < 100; index++)
            {
                _playback.Play(LevelSound.LevelLose);
            }

            Assert.AreEqual(emitted, _playback.EmissionCount);
            Assert.LessOrEqual(_playback.ActiveVoiceCount, VoiceCount);
            Assert.AreEqual(LoopCount + VoiceCount, _root.GetComponentsInChildren<AudioSource>().Length);
            yield return new WaitForSecondsRealtime(0.12f);
            _playback.Play(LevelSound.LevelLose);
            Assert.AreEqual(emitted + 1, _playback.EmissionCount);
            Assert.LessOrEqual(_playback.ActiveVoiceCount, VoiceCount);
        }

        [UnityTest]
        public IEnumerator PauseStopsTransientAudioAndResumePreservesLoopsWithoutNewEmissions()
        {
            _playback.Activate();
            _playback.NotifyDragMotion();
            _playback.SetLoop(LevelSound.ScanLoop, true);
            _playback.Play(LevelSound.LevelLose);
            yield return null;
            int emitted = _playback.EmissionCount;
            _playback.SetPaused(true);
            _playback.Play(LevelSound.SecurityAlert);
            _playback.NotifyDragMotion();
            Assert.IsTrue(_playback.IsPaused);
            Assert.AreEqual(0, _playback.ActiveVoiceCount);
            Assert.IsFalse(_loops[0].isPlaying);
            Assert.IsFalse(_loops[1].isPlaying);
            Assert.IsFalse(_loops[2].isPlaying);
            yield return new WaitForSecondsRealtime(0.1f);
            _playback.SetPaused(false);
            _playback.SetPaused(false);
            yield return null;
            Assert.IsFalse(_playback.IsPaused);
            Assert.IsTrue(_loops[0].isPlaying);
            Assert.IsTrue(_loops[2].isPlaying);
            Assert.IsFalse(_loops[1].isPlaying);
            Assert.AreEqual(0, _playback.ActiveVoiceCount);
            Assert.AreEqual(emitted, _playback.EmissionCount);
        }

        [UnityTest]
        public IEnumerator CategoryLevelsApplyIndependentlyAndRebindingRemovesOldPublisher()
        {
            _playback.Activate();
            _playback.SetLoop(LevelSound.ScanLoop, true);
            _levels.Set(false, 0.21f, 0.63f);
            Assert.That(_loops[0].volume, Is.EqualTo(0.21f * _library.GetCue(LevelSound.Atmosphere).Volume)
                .Within(0.0001f));
            Assert.That(_loops[2].volume, Is.EqualTo(0.63f * _library.GetCue(LevelSound.ScanLoop).Volume)
                .Within(0.0001f));
            _levels.Set(true, 0.21f, 0.63f);
            _levels.Set(false, 0.21f, 0.63f);
            Assert.That(_loops[0].volume, Is.EqualTo(0.21f * _library.GetCue(LevelSound.Atmosphere).Volume)
                .Within(0.0001f));

            TestAudioLevels replacement = new TestAudioLevels();
            _playback.Initialize(replacement);
            _playback.Activate();
            _levels.Set(true, 0, 0);
            yield return null;
            Assert.IsTrue(_loops[0].isPlaying, "The previous settings publisher must be detached.");
            Assert.That(_loops[0].volume, Is.EqualTo(replacement.AtmosphereVolume
                * _library.GetCue(LevelSound.Atmosphere).Volume).Within(0.0001f));

            _root.SetActive(false);
            replacement.Set(true, 0, 0);
            _root.SetActive(true);
            yield return null;
            Assert.IsFalse(_playback.IsActivated);
            Assert.IsFalse(_loops[0].isPlaying);
        }

        [UnityTest]
        public IEnumerator OptionsPersistClampAndSynchronizeWithoutSpuriousInteractions()
        {
            PlayerPrefs.DeleteKey(MutedKey);
            PlayerPrefs.DeleteKey(AtmosphereKey);
            PlayerPrefs.DeleteKey(EffectsKey);
            AudioPreferences preferences = new AudioPreferences();
            Assert.IsFalse(preferences.IsMuted);
            Assert.AreEqual(0.32f, preferences.AtmosphereVolume);
            Assert.AreEqual(0.8f, preferences.EffectsVolume);
            AudioOptionsView view = CreateOptions(out Toggle soundEnabled, out Slider atmosphere,
                out Slider effects);
            int interactions = 0;
            view.Interacted += () => interactions++;
            view.Initialize(preferences);
            preferences.SetMuted(true);
            preferences.SetAtmosphereVolume(0.17f);
            preferences.SetEffectsVolume(0.57f);
            Assert.IsFalse(soundEnabled.isOn);
            Assert.AreEqual(0.17f, atmosphere.value);
            Assert.AreEqual(0.57f, effects.value);
            Assert.AreEqual(0, interactions, "Model-to-view synchronization must not emit user interactions.");

            view.Initialize(preferences);
            soundEnabled.isOn = true;
            atmosphere.value = 0.29f;
            effects.value = 0.71f;
            Assert.AreEqual(3, interactions, "Rebinding must not duplicate control listeners.");
            AudioPreferences reloaded = new AudioPreferences();
            Assert.IsFalse(reloaded.IsMuted);
            Assert.AreEqual(0.29f, reloaded.AtmosphereVolume);
            Assert.AreEqual(0.71f, reloaded.EffectsVolume);
            reloaded.SetAtmosphereVolume(-1);
            reloaded.SetEffectsVolume(2);
            Assert.AreEqual(0, reloaded.AtmosphereVolume);
            Assert.AreEqual(1, reloaded.EffectsVolume);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BoardingAndDepartureAreAudibleAtTheStartAboveDefaultAtmosphere()
        {
            LevelAudioCue atmosphere = _library.GetCue(LevelSound.Atmosphere);
            float background = MeasureRms(atmosphere.Clip, atmosphere.Clip.samples) * atmosphere.Volume * .32f;

            foreach (LevelSound sound in new[] { LevelSound.PassengerBoard, LevelSound.BusDepart })
            {
                LevelAudioCue cue = _library.GetCue(sound);
                int prefixSamples = Mathf.RoundToInt(cue.Clip.frequency * .02f);
                float onset = MeasureRms(cue.Clip, prefixSamples) * cue.Volume * .8f;
                Assert.That(onset, Is.GreaterThan(background * 2),
                    sound + " must have an audible first 20 ms instead of emerging late under the station loop.");
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator OpeningOptionsKeepsSliderHitsAboveLateSelectionTargets()
        {
            AudioOptionsView view = null;

            foreach (GameObject sceneRoot in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                AudioOptionsView candidate = sceneRoot.GetComponentInChildren<AudioOptionsView>(true);

                if (candidate != null)
                {
                    view = candidate;
                }
            }

            Assert.IsNotNull(view);
            RectTransform panel = view.PointerRegion;
            _optionsRoot = CreateControl("Late on-field selection target", panel.parent);
            RectTransform target = (RectTransform)_optionsRoot.transform;
            target.anchorMin = Vector2.zero;
            target.anchorMax = Vector2.one;
            target.offsetMin = Vector2.zero;
            target.offsetMax = Vector2.zero;
            _optionsRoot.AddComponent<Image>();
            ReadField<Button>(view, "_button").onClick.Invoke();
            yield return null;
            Canvas.ForceUpdateCanvases();

            Slider atmosphere = ReadField<Slider>(view, "_atmosphere");
            Canvas canvas = panel.GetComponentInParent<Canvas>();
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            PointerEventData pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(camera, atmosphere.transform.position)
            };
            List<RaycastResult> hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            Transform hit = hits[0].gameObject.transform;
            Assert.IsTrue(hit == panel || hit.IsChildOf(panel),
                "Audio controls must receive the pointer before late on-field selection targets.");
        }

        [Test]
        public void RouterRoutesOnceAndReplacementDetachesOldActivityWhilePauseReasonsRemainIndependent()
        {
            LevelGameSession game = new LevelGameSession(
                LevelJsonImporter.Parse(Resources.Load<TextAsset>("Levels/level_01").text));
            RecordingCuePlayback playback = new RecordingCuePlayback();
            AudioActivity activity = new AudioActivity();
            AudioActivity options = new AudioActivity();
            LevelAudioCueRouter router = new LevelAudioCueRouter(playback, options, activity, activity,
                activity, activity, activity, activity, _levels, game);
            router.Enable();
            router.Enable();
            activity.Press();
            Assert.AreEqual(1, playback.Activations, "Repeated enable must not attach duplicate handlers.");
            options.ActivateControl();
            activity.Select("bus");
            activity.Move();
            activity.Release(true);
            activity.Reject();
            activity.ShowVideo(true);
            activity.ShowVideo(false);
            Assert.IsTrue(game.TryMove("R1", "L01-R1", 1));
            game.Complete(game.PendingStep);
            Assert.That(game.BoardingSteps.Count, Is.GreaterThan(0));
            activity.Complete(game.BoardingSteps[0]);
            activity.ChangeChamberPhase("Opening");
            CollectionAssert.AreEqual(new[] { LevelSound.UiClick, LevelSound.BusSelect, LevelSound.BusSnap,
                LevelSound.ActionRejected, LevelSound.ScanStart, LevelSound.ScanEnd,
                LevelSound.PassengerBoard, LevelSound.QuarantineOpen },
                playback.Requests);
            Assert.AreEqual(1, playback.DragNotifications);

            game.SetPaused(true);
            Assert.IsTrue(playback.IsPaused);
            router.SetSessionEnabled(false);
            game.SetPaused(false);
            Assert.IsTrue(playback.IsPaused, "Returning focus must not clear disabled-session audio pause.");
            router.SetSessionEnabled(true);
            Assert.IsFalse(playback.IsPaused);
            router.Disable();
            router.Disable();
            int beforeReplacement = playback.Requests.Count;
            activity.ActivateControl();
            options.ActivateControl();
            Assert.AreEqual(beforeReplacement, playback.Requests.Count);
            game.SetPaused(true);
            Assert.IsFalse(playback.IsPaused, "The detached model must no longer control this binding.");

            AudioActivity replacement = new AudioActivity();
            AudioActivity replacementOptions = new AudioActivity();
            LevelAudioCueRouter nextRouter = new LevelAudioCueRouter(playback, replacementOptions, replacement,
                replacement, replacement, replacement, replacement, replacement, _levels, game);
            nextRouter.Enable();
            game.SetPaused(false);
            Assert.IsFalse(playback.IsPaused);
            activity.ActivateControl();
            activity.ShowVideo(true);
            replacement.ActivateControl();
            Assert.AreEqual(beforeReplacement + 1, playback.Requests.Count,
                "A replacement binding must respond only to the current publishers.");
            Assert.AreEqual(LevelSound.UiClick, playback.Requests[beforeReplacement]);
            nextRouter.Disable();
        }

        private static float MeasureRms(AudioClip clip, int sampleFrames)
        {
            float[] samples = new float[sampleFrames * clip.channels];
            Assert.IsTrue(clip.GetData(samples, 0), "The imported clip must be ready for playback.");
            double power = 0;

            foreach (float sample in samples)
            {
                power += sample * sample;
            }

            return Mathf.Sqrt((float)(power / samples.Length));
        }

        private static T ReadField<T>(object owner, string name)
        {
            FieldInfo field = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, name);
            return (T)field.GetValue(owner);
        }

        private static void WriteField(object owner, string name, object value)
        {
            FieldInfo field = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, name);
            field.SetValue(owner, value);
        }

        private static void RestoreFloat(string key, bool existed, float value)
        {
            if (existed)
            {
                PlayerPrefs.SetFloat(key, value);
            }
            else
            {
                PlayerPrefs.DeleteKey(key);
            }
        }

        private AudioSource[] CreateSources(string prefix, int count)
        {
            AudioSource[] sources = new AudioSource[count];

            for (int index = 0; index < count; index++)
            {
                GameObject child = new GameObject(prefix + index);
                child.transform.SetParent(_root.transform, false);
                AudioSource source = child.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0;
                sources[index] = source;
            }

            return sources;
        }

        private AudioOptionsView CreateOptions(out Toggle soundEnabled, out Slider atmosphere, out Slider effects)
        {
            _optionsRoot = new GameObject("Isolated Audio Options Test", typeof(RectTransform));
            _optionsRoot.SetActive(false);
            AudioOptionsView view = _optionsRoot.AddComponent<AudioOptionsView>();
            GameObject panel = CreateControl("Panel", _optionsRoot.transform);
            GameObject buttonObject = CreateControl("Open", _optionsRoot.transform);
            Button button = buttonObject.AddComponent<Button>();
            soundEnabled = CreateControl("Enabled", panel.transform).AddComponent<Toggle>();
            atmosphere = CreateControl("Atmosphere", panel.transform).AddComponent<Slider>();
            effects = CreateControl("Effects", panel.transform).AddComponent<Slider>();
            Text label = CreateControl("Label", buttonObject.transform).AddComponent<Text>();
            WriteField(view, "_button", button);
            WriteField(view, "_panel", panel.GetComponent<RectTransform>());
            WriteField(view, "_mute", soundEnabled);
            WriteField(view, "_atmosphere", atmosphere);
            WriteField(view, "_effects", effects);
            WriteField(view, "_label", label);
            _optionsRoot.SetActive(true);
            return view;
        }

        private GameObject CreateControl(string name, Transform parent)
        {
            GameObject control = new GameObject(name, typeof(RectTransform));
            control.transform.SetParent(parent, false);
            return control;
        }

        private void RecordEmission(LevelSound sound)
        {
            _emissions.Add(sound);
        }

        private class AudioActivity : IPointerActivation, ILevelDragFeedback, IControlActivation,
            IInspectionVideo, IStepCompletion, IChamberPhases
        {
            public event Action<Vector2> Pressed;
            public event Action<string> SelectionChanged;
            public event Action Moved;
            public event Action<bool> Released;
            public event Action Rejected;
            public event Action ControlActivated;
            public event Action<bool> VideoChanged;
            public event Action<BoardStep> Completed;
            public event Action<string> PhaseChanged;

            public void Press() => Pressed?.Invoke(Vector2.zero);
            public void Select(string busId) => SelectionChanged?.Invoke(busId);
            public void Move() => Moved?.Invoke();
            public void Release(bool accepted) => Released?.Invoke(accepted);
            public void Reject() => Rejected?.Invoke();
            public void ActivateControl() => ControlActivated?.Invoke();
            public void ShowVideo(bool visible) => VideoChanged?.Invoke(visible);
            public void Complete(BoardStep step) => Completed?.Invoke(step);
            public void ChangeChamberPhase(string phase) => PhaseChanged?.Invoke(phase);
        }

        private class RecordingCuePlayback : ILevelAudioPlayback
        {
            public event Action<LevelSound> Played;

            public List<LevelSound> Requests { get; } = new List<LevelSound>();
            public bool IsPaused { get; private set; }
            public bool IsActivated { get; private set; }
            public int ActiveVoiceCount => 0;
            public int Activations { get; private set; }
            public int DragNotifications { get; private set; }

            public void Activate()
            {
                IsActivated = true;
                Activations++;
            }

            public void Play(LevelSound sound)
            {
                Requests.Add(sound);
                Played?.Invoke(sound);
            }

            public void SetLoop(LevelSound sound, bool wanted)
            {
                // Native source loop semantics are covered by the isolated playback tests.
            }

            public void SetPaused(bool paused)
            {
                IsPaused = paused;
            }

            public void ResetTransient()
            {
                // This recording port observes routing; it owns no native sources.
            }

            public void NotifyDragMotion()
            {
                DragNotifications++;
            }
        }

        private class TestAudioLevels : IAudioLevels
        {
            public event Action Changed;

            public bool IsMuted { get; private set; }
            public float AtmosphereVolume { get; private set; } = 0.32f;
            public float EffectsVolume { get; private set; } = 0.8f;

            public void Set(bool muted, float atmosphere, float effects)
            {
                IsMuted = muted;
                AtmosphereVolume = atmosphere;
                EffectsVolume = effects;
                Changed?.Invoke();
            }
        }
    }
}
