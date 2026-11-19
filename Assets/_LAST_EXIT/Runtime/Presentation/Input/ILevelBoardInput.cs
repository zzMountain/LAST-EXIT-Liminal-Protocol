using UnityEngine;

namespace LastExit.Presentation
{
    public interface ILevelBoardInput
    {
        public Transform GetBus(string id);
        public Bounds GetBusBounds(string id);
        public float GetCellX(float worldX);
        public float GetWorldX(float cell);
    }
}
