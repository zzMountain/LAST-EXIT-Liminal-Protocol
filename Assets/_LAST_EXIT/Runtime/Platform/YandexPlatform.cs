using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastExit.Platform
{
    // One scene composition owns this adapter and its callbacks for the entire campaign.
    public class YandexPlatform : MonoBehaviour, ICampaignPlatform
    {
        private const string LocalProgressKey = "LastExit.Progress.v1.Editor";
        [SerializeField, Min(1)] private int _completedLevelsPerAd = 3;
        private CampaignProgress _progress = new CampaignProgress
        {
            version = 1,
            completedLevels = Array.Empty<int>(),
            bestResults = Array.Empty<CampaignResultData>()
        };
        private Action _transitionCompleted;
        private Coroutine _readyRoutine;
        private int _levelCount;
        private int _completedSinceAd;
        private bool _initialized;
        private bool _adPending;
        private bool _sceneReadyRequested;
        private bool _externallyPaused;
#if !UNITY_EDITOR && !UNITY_WEBGL
        private bool _localSaveRejected;
#endif

        public event Action ProgressReady;
        public event Action PauseChanged;

        public bool IsPaused => _externallyPaused || (_initialized && IsSceneReady == false);
        public bool IsProgressReady { get; private set; }
        public bool IsSceneReady { get; private set; }
        public int ResumeLevelIndex => _progress.currentLevel;
        public string StorageStatus { get; private set; } = "initializing";

        private void OnEnable()
        {
            TryCompleteReadiness();
        }

        private void OnDisable()
        {
            CancelReadiness();
            SetGameplayActive(false);
        }

        private void OnDestroy()
        {
            CancelReadiness();
        }

        public void Initialize(int levelCount)
        {
            if (_initialized)
            {
                return;
            }

            if (levelCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(levelCount));
            }

            _initialized = true;
            _levelCount = levelCount;
#if UNITY_WEBGL && !UNITY_EDITOR
            LastExitPlatform_Initialize(gameObject.name, levelCount);
#elif UNITY_EDITOR
            ReceiveProgress(JsonUtility.ToJson(_progress));
            ReceiveStorageStatus("editor-in-memory");
#else
            string json = PlayerPrefs.GetString(LocalProgressKey, JsonUtility.ToJson(_progress));
            ReceiveProgress(json);
            ReceiveStorageStatus(_localSaveRejected ? "local-rejected-preserved" : "local-only");
#endif
        }

        public void MarkSceneReady()
        {
            _sceneReadyRequested = true;
            TryCompleteReadiness();
        }

        public void CancelSceneReadiness()
        {
            _sceneReadyRequested = false;
            CancelReadiness();
        }

        public void SetGameplayActive(bool active)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (_initialized)
            {
                LastExitPlatform_Gameplay(active ? 1 : 0);
            }
#endif
        }

        public void RecordCompletedLevel(CampaignResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            int levelIndex = result.LevelIndex;
            ValidateIndex(levelIndex);

            List<int> completed = new List<int>(_progress.completedLevels);
            List<CampaignResultData> results = new List<CampaignResultData>(_progress.bestResults);
            int resultIndex = results.FindIndex(previous => previous.levelIndex == levelIndex);
            bool improved = resultIndex < 0 || result.IsBetterThan(CreateResult(results[resultIndex]));
            bool firstCompletion = completed.Contains(levelIndex) == false;

            if (improved == false && firstCompletion == false)
            {
                return;
            }

            if (firstCompletion)
            {
                completed.Add(levelIndex);
                completed.Sort();
                _progress.completedLevels = completed.ToArray();
                _progress.currentLevel = Math.Min(levelIndex + 1, _levelCount - 1);
                _completedSinceAd++;
            }

            if (improved)
            {
                if (resultIndex < 0)
                {
                    results.Add(CreateResultData(result));
                }
                else
                {
                    results[resultIndex] = CreateResultData(result);
                }

                results.Sort((left, right) => left.levelIndex.CompareTo(right.levelIndex));
                _progress.bestResults = results.ToArray();
            }

            Save();
        }

        public void SetCurrentLevel(int levelIndex)
        {
            ValidateIndex(levelIndex);

            if (_progress.currentLevel == levelIndex)
            {
                return;
            }

            _progress.currentLevel = levelIndex;
            Save();
        }

        public void ShowTransitionAd(Action completed)
        {
            if (_adPending)
            {
                return;
            }

            SetGameplayActive(false);

            if (_completedSinceAd < _completedLevelsPerAd)
            {
                completed?.Invoke();
                return;
            }

            _completedSinceAd = 0;
            _adPending = true;
            _transitionCompleted = completed;
#if UNITY_WEBGL && !UNITY_EDITOR
            LastExitPlatform_Ad();
#else
            ReceiveAdResult("unavailable");
#endif
        }

        // These public methods are the documented Unity SendMessage bridge entry points.
        public void ReceiveProgress(string json)
        {
            if (IsProgressReady)
            {
                return;
            }

            if (CampaignProgress.TryDecode(json, _levelCount, out CampaignProgress decoded))
            {
                _progress = decoded;
            }
            else
            {
#if !UNITY_EDITOR && !UNITY_WEBGL
                _localSaveRejected = true;
#endif
                ReceiveStorageStatus("progress-rejected");
            }

            IsProgressReady = true;
            ProgressReady?.Invoke();
        }

        public void ReceivePause(string paused)
        {
            bool isPaused = paused == "1";

            if (_externallyPaused == isPaused)
            {
                return;
            }

            _externallyPaused = isPaused;
            PauseChanged?.Invoke();
        }

        public void ReceiveStorageStatus(string status)
        {
            StorageStatus = status;
        }

        public void ReceiveAdResult(string result)
        {
            if (_adPending == false)
            {
                return;
            }

            _adPending = false;
            Action completed = _transitionCompleted;
            _transitionCompleted = null;
            completed?.Invoke();
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void LastExitPlatform_Initialize(string target, int levelCount);
        [DllImport("__Internal")] private static extern void LastExitPlatform_Ready();
        [DllImport("__Internal")] private static extern void LastExitPlatform_Gameplay(int active);
        [DllImport("__Internal")] private static extern void LastExitPlatform_Save(string json);
        [DllImport("__Internal")] private static extern void LastExitPlatform_Ad();
#endif

        private static CampaignResultData CreateResultData(CampaignResult result)
        {
            return new CampaignResultData
            {
                levelIndex = result.LevelIndex,
                stars = result.Stars,
                cctvUses = result.CctvUses,
                emergencyResets = result.EmergencyResets,
                lostSeats = result.LostSeats
            };
        }

        private static CampaignResult CreateResult(CampaignResultData data)
        {
            return new CampaignResult(data.levelIndex, data.stars, data.cctvUses, data.emergencyResets, data.lostSeats);
        }

        private void Save()
        {
            string json = JsonUtility.ToJson(_progress);
#if UNITY_WEBGL && !UNITY_EDITOR
            LastExitPlatform_Save(json);
#elif !UNITY_EDITOR
            if (_localSaveRejected)
            {
                ReceiveStorageStatus("local-rejected-preserved");
                return;
            }

            PlayerPrefs.SetString(LocalProgressKey, json);
            PlayerPrefs.Save();
#endif
        }

        private void TryCompleteReadiness()
        {
            if (_sceneReadyRequested == false || IsSceneReady || _readyRoutine != null ||
                isActiveAndEnabled == false || Application.isPlaying == false)
            {
                return;
            }

            _readyRoutine = StartCoroutine(CompleteReadiness());
        }

        private IEnumerator CompleteReadiness()
        {
            // Scene Awake/UI initialization can finish while Unity is still drawing its required startup logos.
            while (SplashScreen.isFinished == false)
            {
                yield return null;
            }

            // Let the first unobscured scene frame finish its camera/HUD projection before reporting ready.
            yield return null;
            _readyRoutine = null;
            IsSceneReady = true;
#if UNITY_WEBGL && !UNITY_EDITOR
            LastExitPlatform_Ready();
#endif
            PauseChanged?.Invoke();
        }

        private void CancelReadiness()
        {
            if (_readyRoutine == null)
            {
                return;
            }

            StopCoroutine(_readyRoutine);
            _readyRoutine = null;
        }

        private void ValidateIndex(int levelIndex)
        {
            if (IsProgressReady == false || levelIndex < 0 || levelIndex >= _levelCount)
            {
                throw new ArgumentOutOfRangeException(nameof(levelIndex));
            }
        }
    }
}
