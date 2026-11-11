using UnityEngine;

namespace LastExit.Presentation
{
    public interface ILevelDiagnosticBoard : ILevelBusBounds
    {
        public Vector3 GetPassengerPosition(string id);
    }
}
