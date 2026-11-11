using UnityEngine;

namespace LastExit.ArtLab.Preview
{
    public interface IPreviewLaneGuide
    {
        public void Refresh(Camera camera, RectTransform canvas, int selectedIndex);
    }
}
