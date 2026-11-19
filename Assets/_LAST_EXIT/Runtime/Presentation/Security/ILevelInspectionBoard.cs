using UnityEngine;

namespace LastExit.Presentation
{
    public interface ILevelInspectionBoard
    {
        public float QueueHeadZ { get; }
        public float GetWorldX(float cell);
        public Bounds GetPassengerBounds(string id);
        public void SetPassengerScan(string id, bool visible, float elapsed);
    }
}
