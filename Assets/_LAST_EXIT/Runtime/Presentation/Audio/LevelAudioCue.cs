using System;
using UnityEngine;

namespace LastExit.Presentation
{
    [Serializable]
    public class LevelAudioCue
    {
        [SerializeField] private LevelSound _sound;
        [SerializeField] private AudioClip _clip;
        [SerializeField, Range(0, 1)] private float _volume = 1;

        public LevelSound Sound => _sound;
        public AudioClip Clip => _clip;
        public float Volume => _volume;
    }
}
