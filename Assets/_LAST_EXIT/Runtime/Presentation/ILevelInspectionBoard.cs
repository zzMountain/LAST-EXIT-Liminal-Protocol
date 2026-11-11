using UnityEngine;

namespace LastExit.Presentation
{
    public interface ILevelInspectionBoard
    {
        public Bounds GetPassengerBounds(string id);
        public void SetPassengerScan(string id, bool visible, float elapsed);
    }
}
