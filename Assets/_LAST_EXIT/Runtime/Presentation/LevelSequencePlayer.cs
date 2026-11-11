using System;
using System.Collections;
using System.Collections.Generic;
using LastExit.Domain;
using UnityEngine;

namespace LastExit.Presentation
{
    public class LevelSequencePlayer : MonoBehaviour, IStepCompletion
    {
        private readonly Dictionary<BoardStep, LevelStepPlayback> _playbacks =
            new Dictionary<BoardStep, LevelStepPlayback>();
        [SerializeField, Min(.05f)] private float _departureSeconds = .65f;
        [SerializeField, Min(.05f)] private float _arrivalSeconds = .5f;
        [SerializeField, Min(1)] private float _exitMarginCells = 4;
        [SerializeField] private QuarantineChamber _chamber;
        private ILevelSequenceBoard _board;
        private IQuarantinePlayback _chamberPlayback;
        private int _width;
        private bool _paused;

        public event Action<BoardStep> Completed;

        private void OnDisable()
        {
            Cancel();
        }

        public void Initialize(ILevelSequenceBoard board, int width)
        {
            _board = board;
            _width = width;
            _chamberPlayback = _chamber;
        }

        public void Play(BoardStep step, BoardSnapshot snapshot)
        {
            foreach (BoardStep active in _playbacks.Keys)
            {
                if (step.Kind != BoardStepKind.Board || active.Kind != BoardStepKind.Board ||
                    ReferenceEquals(step, active))
                {
                    throw new InvalidOperationException("Only independent boarding steps may animate together.");
                }
            }

            if (step.Kind == BoardStepKind.Arrive)
            {
                Transform bus = _board.GetBus(step.BusId);
                bus.position += Vector3.left * (_width + _exitMarginCells) * _board.CellSize;
            }

            Transform passenger = null;

            if (step.Kind == BoardStepKind.Board || step.Kind == BoardStepKind.Quarantine ||
                step.Kind == BoardStepKind.Corruption)
            {
                passenger = _board.GetPassenger(step.PassengerId);
            }

            LevelStepPlayback playback = new LevelStepPlayback(step, passenger);
            _playbacks.Add(step, playback);
            playback.Routine = StartCoroutine(Execute(playback, snapshot));
        }

        public void SetPaused(bool paused)
        {
            _paused = paused;
            _chamberPlayback.SetPaused(paused);
        }

        public void Cancel()
        {
            _board?.CancelPassengerMovement();
            _chamberPlayback?.ResetState();

            foreach (LevelStepPlayback playback in _playbacks.Values)
            {
                if (playback.Routine != null)
                {
                    StopCoroutine(playback.Routine);
                }

                playback.RestorePassenger();
            }

            _playbacks.Clear();
        }

        private IEnumerator Execute(LevelStepPlayback playback, BoardSnapshot snapshot)
        {
            // Always yield once: completion must never re-enter Play before its handle was assigned.
            yield return null;
            BoardStep step = playback.Step;

            switch (step.Kind)
            {
                case BoardStepKind.Move:
                    yield return MoveBus(step, snapshot);
                    break;
                case BoardStepKind.Board:
                    yield return BoardPassenger(step);
                    break;
                case BoardStepKind.Depart:
                    yield return DepartBus(step);
                    break;
                case BoardStepKind.Arrive:
                    yield return ArriveBus(step, snapshot);
                    break;
                case BoardStepKind.Quarantine:
                case BoardStepKind.Corruption:
                    yield return _chamberPlayback.Execute(_board, step.PassengerId);
                    break;
            }

            while (_paused)
            {
                yield return null;
            }

            _playbacks.Remove(step);
            Completed?.Invoke(step);
        }

        private IEnumerator BoardPassenger(BoardStep step)
        {
            Transform passenger = _board.GetPassenger(step.PassengerId);
            Vector3 door = _board.GetDoor(step.BusId);
            Vector3 target = new Vector3(door.x, passenger.position.y, door.z);
            yield return _board.WalkPassenger(step.PassengerId, target);
            yield return _board.PlayBoardingFeedback(step.PassengerId);

            while (_paused)
            {
                yield return null;
            }

            passenger.gameObject.SetActive(false);
        }

        private IEnumerator MoveBus(BoardStep step, BoardSnapshot snapshot)
        {
            int rowIndex = 0;

            while (snapshot.Rows[rowIndex].Id != step.RowId)
            {
                rowIndex++;
            }

            Transform bus = _board.GetBus(step.BusId);
            bus.position = _board.GetBusPosition(rowIndex, step.ToLeftX);
            yield return null;
        }

        private IEnumerator DepartBus(BoardStep step)
        {
            Transform bus = _board.GetBus(step.BusId);
            Vector3 target = bus.position + Vector3.right * (_width + _exitMarginCells) * _board.CellSize;
            yield return Move(bus, target, _departureSeconds);
        }

        private IEnumerator ArriveBus(BoardStep step, BoardSnapshot snapshot)
        {
            int rowIndex = 0;

            while (snapshot.Rows[rowIndex].Id != step.RowId)
            {
                rowIndex++;
            }

            Transform bus = _board.GetBus(step.BusId);
            Vector3 target = _board.GetBusPosition(rowIndex, step.ToLeftX);
            bus.position = target + Vector3.left * (_width + _exitMarginCells) * _board.CellSize;
            yield return Move(bus, target, _arrivalSeconds);
        }

        private IEnumerator Move(Transform actor, Vector3 target, float duration)
        {
            Vector3 origin = actor.position;
            float elapsed = 0;

            while (elapsed < duration)
            {
                if (_paused == false)
                {
                    elapsed = Mathf.Min(duration, elapsed + Time.deltaTime);
                    // Immediate visible response, then a short ease into the final position.
                    float remaining = 1 - elapsed / duration;
                    float progress = 1 - remaining * remaining;
                    actor.position = Vector3.Lerp(origin, target, progress);
                }

                yield return null;
            }

            actor.position = target;
        }
    }
}
