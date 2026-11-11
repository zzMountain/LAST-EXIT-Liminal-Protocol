using System;

namespace LastExit.Presentation
{
    // Presentation state survives layout changes; only a new attempt resets it.
    public class CctvInspection
    {
        public const float VideoDuration = 4;

        public bool IsOpen { get; private set; }
        public bool IsVideo { get; private set; }
        public float Elapsed { get; private set; }
        public string SelectedQueue { get; private set; }

        public void OpenSelection()
        {
            IsOpen = true;
        }

        public void PlayVideo()
        {
            IsOpen = true;
            IsVideo = true;
            Elapsed = 0;
        }

        public void Advance(float seconds, bool paused)
        {
            if (IsVideo == false || paused)
            {
                return;
            }

            Elapsed = Math.Min(VideoDuration, Elapsed + Math.Max(0, seconds));
            IsVideo = Elapsed < VideoDuration;

            if (IsVideo == false)
            {
                IsOpen = false;
                SelectedQueue = null;
            }
        }

        public void Select(string queueId)
        {
            if (IsOpen && IsVideo == false)
            {
                SelectedQueue = queueId;
            }
        }

        public void Reset()
        {
            Close();
            Elapsed = 0;
        }

        private void Close()
        {
            IsOpen = false;
            IsVideo = false;
            SelectedQueue = null;
        }
    }
}
