using UnityEngine;

namespace LastExit.ArtLab.Preview
{
    public interface IPreviewBusBadge
    {
        public void Refresh(Camera camera, RectTransform canvas);
        public void SetSelected(bool isSelected);
    }
}
