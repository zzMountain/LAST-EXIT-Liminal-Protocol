using UnityEngine;

namespace LastExit.Presentation
{
    public interface ILevelHudBoard : ILevelBusGeometry
    {
        public float QueueHeadZ { get; }
        public float RowEndZ { get; }
        public Vector3 GetIsolationPosition();
        public Bounds GetPassengerBounds(string id);
    }
}
