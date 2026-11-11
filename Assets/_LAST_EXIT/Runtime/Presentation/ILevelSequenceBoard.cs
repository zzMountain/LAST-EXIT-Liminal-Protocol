using System.Collections;
using UnityEngine;

namespace LastExit.Presentation
{
    public interface ILevelSequenceBoard : IQuarantineBoard
    {
        public float CellSize { get; }
        public Transform GetBus(string id);
        public void CancelPassengerMovement();
        public IEnumerator PlayBoardingFeedback(string id);
        public Vector3 GetDoor(string id);
        public Vector3 GetBusPosition(int rowIndex, int leftX);
    }
}
