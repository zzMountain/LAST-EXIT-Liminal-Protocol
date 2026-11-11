using UnityEngine;

namespace LastExit.ArtLab.Preview
{
    public interface IPreviewBusMotion
    {
        public Vector3 Position { get; set; }
        public Vector3 InitialPosition { get; }
        public Bounds GetPhysicalBounds();
    }
}
