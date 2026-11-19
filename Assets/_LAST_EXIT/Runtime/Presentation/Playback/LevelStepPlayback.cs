using LastExit.Domain;
using UnityEngine;

namespace LastExit.Presentation
{
    // Owns the presentation state needed to cancel one in-flight board step.
    internal class LevelStepPlayback
    {
        public LevelStepPlayback(BoardStep step, Transform passenger)
        {
            Step = step;
            Passenger = passenger;

            if (passenger != null)
            {
                Origin = passenger.position;
                Rotation = passenger.rotation;
                WasActive = passenger.gameObject.activeSelf;
            }
        }

        public BoardStep Step { get; }
        public Transform Passenger { get; }
        public Vector3 Origin { get; }
        public Quaternion Rotation { get; }
        public bool WasActive { get; }
        public Coroutine Routine { get; set; }

        public void RestorePassenger()
        {
            if (Passenger != null)
            {
                Passenger.SetPositionAndRotation(Origin, Rotation);
                Passenger.gameObject.SetActive(WasActive);
            }
        }
    }
}
