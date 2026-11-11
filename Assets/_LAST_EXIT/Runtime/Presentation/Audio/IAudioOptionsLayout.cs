using UnityEngine;

namespace LastExit.Presentation
{
    public interface IAudioOptionsLayout
    {
        public RectTransform PointerRegion { get; }
        public void RefreshLayout(float scale, bool portrait);
    }
}
