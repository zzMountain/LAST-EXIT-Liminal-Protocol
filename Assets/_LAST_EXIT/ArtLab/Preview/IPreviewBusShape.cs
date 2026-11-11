using UnityEngine;

namespace LastExit.ArtLab.Preview
{
    public interface IPreviewBusShape
    {
        public Vector3 DoorPosition { get; }
        public Bounds GetPhysicalBounds();
    }
}
