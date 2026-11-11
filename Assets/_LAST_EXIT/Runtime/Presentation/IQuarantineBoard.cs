using System.Collections;
using UnityEngine;

namespace LastExit.Presentation
{
    public interface IQuarantineBoard
    {
        public Transform GetPassenger(string id);
        public IEnumerator WalkPassenger(string id, Vector3 target);
    }
}
