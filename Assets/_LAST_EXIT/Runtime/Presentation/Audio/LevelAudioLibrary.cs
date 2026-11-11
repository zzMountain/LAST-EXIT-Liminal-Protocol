using System;
using UnityEngine;

namespace LastExit.Presentation
{
    [CreateAssetMenu(menuName = "LAST EXIT/Level Audio Library")]
    public class LevelAudioLibrary : ScriptableObject
    {
        [SerializeField] private LevelAudioCue[] _cues = Array.Empty<LevelAudioCue>();

        public LevelAudioCue GetCue(LevelSound sound)
        {
            if (sound < LevelSound.Atmosphere || sound >= LevelSound.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(sound));
            }

            if (_cues == null)
            {
                throw new InvalidOperationException("Audio library has no assigned cues.");
            }

            foreach (LevelAudioCue cue in _cues)
            {
                if (cue != null && cue.Sound == sound)
                {
                    if (cue.Clip == null)
                    {
                        throw new InvalidOperationException($"Audio cue {sound} has no assigned clip.");
                    }

                    return cue;
                }
            }

            throw new InvalidOperationException($"Audio library is missing cue {sound}.");
        }
    }
}
