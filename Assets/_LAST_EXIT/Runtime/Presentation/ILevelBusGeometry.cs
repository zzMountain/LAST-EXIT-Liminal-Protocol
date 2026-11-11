using UnityEngine;

namespace LastExit.Presentation
{
    public interface ILevelBusGeometry : ILevelBusBounds
    {
        public Vector3 GetDoor(string id);
    }
}
