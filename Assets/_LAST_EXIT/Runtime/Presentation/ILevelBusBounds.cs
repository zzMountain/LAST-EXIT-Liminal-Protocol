using UnityEngine;

namespace LastExit.Presentation
{
    public interface ILevelBusBounds
    {
        public Bounds GetBusBounds(string id);
        public float GetWorldX(float cell);
    }
}
