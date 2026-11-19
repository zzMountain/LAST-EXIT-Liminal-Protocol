using System;
using System.Collections.Generic;
using LastExit.Content;
using LastExit.Domain;

namespace LastExit.Presentation
{
    // Owns the attempt and its progression. Device sampling and animation are external collaborators.
    public class LevelGameSession : ILevelInteraction, ILevelReadout, ILevelStopPreview,
        ILevelAudioReadout
    {
        private readonly BoardSession _board;
        private readonly LevelDefinition _definition;
        private readonly LevelTutorial _tutorial;
        private readonly HashSet<BoardStep> _dispatched = new HashSet<BoardStep>();
        private readonly float _boardingBeatSeconds;
        private bool _waitingForBeat;
        private bool _skipBeatFrame;

        public LevelGameSession(LevelDefinition definition, bool hasNextLevel = false,
            string arrivalVariantId = LevelContentSelection.FixedVariantId, float boardingBeatSeconds = 1.5f)
        {
            if (string.IsNullOrWhiteSpace(arrivalVariantId))
            {
                throw new ArgumentException(
                    "An attempt requires an arrival variant identity.", nameof(arrivalVariantId));
            }

            _board = new BoardSession(definition);
            Snapshot = _board.GetSnapshot();
            _definition = definition;
            _tutorial = new LevelTutorial(definition);
            _boardingBeatSeconds = Math.Max(0, boardingBeatSeconds);
            RefreshBeatClock();
            HasNextLevel = hasNextLevel;
            ArrivalVariantId = arrivalVariantId;
        }

        public event Action Changed;
        public event Action<BoardStep> StepRequested;

        public BoardSnapshot Snapshot { get; private set; }
        public ILevelTutorialReadout Tutorial => _tutorial;
        public bool IsPaused { get; private set; }
        public bool IsSecurityViewing { get; private set; }
        public bool IsScanQueued { get; private set; }
        public float BoardingBeatRemaining { get; private set; }
        public bool IsAwaitingBoardingBeat => _board.IsAwaitingBoardingBeat;
        public string LevelId => _definition.Id;
        public string ArrivalVariantId { get; }
        public int GridWidth => _definition.Width;
        public bool HasNextLevel { get; }
        public int ResultStars => _board.ResultStars;
        public bool CanUseSecurity => IsPaused == false && PendingStep == null &&
            Snapshot.IsVictory == false && Snapshot.IsDefeat == false;
        public bool CanRequestScan => IsPaused == false &&
            (PendingStep == null || IsBoarding) && Snapshot.CctvCharges > 0 &&
            Snapshot.IsVictory == false && Snapshot.IsDefeat == false && IsScanQueued == false;
        public BoardStep PendingStep => _board.PendingStep;
        public IReadOnlyList<BoardStep> BoardingSteps => _board.BoardingSteps;

        private bool IsBoarding => PendingStep != null && PendingStep.Kind == BoardStepKind.Board;

        public LaneAccess GetLaneAccess(string queueId)
        {
            return _board.GetLaneAccess(queueId);
        }

        public bool IsCctvAnomaly(string queueId)
        {
            return _board.IsCctvAnomaly(queueId);
        }

        public void SetSecurityViewing(bool viewing)
        {
            if (IsSecurityViewing == viewing)
            {
                return;
            }

            IsSecurityViewing = viewing;
            _skipBeatFrame = true;
            Publish();
        }

        public BusStopPreview GetStopPreview(string busId, int leftX)
        {
            foreach (RowSnapshot row in Snapshot.Rows)
            {
                if (row.Bus == null || row.Bus.Id != busId)
                {
                    continue;
                }

                bool inBounds = leftX >= 0 && leftX <= _definition.Width - row.Bus.FootprintCells;
                bool allowed = inBounds && CanMove(row.Id, busId, leftX) &&
                    (IsBoarding || _tutorial.CanMove(busId, leftX));
                LaneAccess access = null;

                foreach (System.Collections.Generic.KeyValuePair<string, int> lane in _definition.Corridors)
                {
                    if (inBounds && lane.Value == leftX + 1)
                    {
                        access = _board.PreviewLaneAccess(lane.Key, row.Id, leftX);
                    }
                }

                return new BusStopPreview(busId, leftX, allowed, access);
            }

            return null;
        }

        public bool CanSelect(string busId)
        {
            RowSnapshot row = FindInputRow(busId);
            return row != null && _board.CanMoveBus(row.Id, row.Bus.LeftX);
        }

        public bool TryMove(string rowId, string busId, int leftX)
        {
            if (CanMove(rowId, busId, leftX) == false ||
                (IsBoarding == false && _tutorial.CanMove(busId, leftX) == false))
            {
                return false;
            }

            if (_board.TryMoveBus(rowId, leftX) == false)
            {
                return false;
            }

            _tutorial.Continue();
            Publish();
            return true;
        }

        public bool CanMove(string rowId, string busId, float leftX)
        {
            RowSnapshot row = FindInputRow(busId);
            return row != null && row.Id == rowId && _board.CanMoveBus(rowId, leftX);
        }

        public void Complete(BoardStep step)
        {
            if (IsPaused || IsSecurityViewing || step == null || IsCurrent(step) == false ||
                _board.TryAcknowledge(step.Generation, step.Token) == false)
            {
                return;
            }

            _tutorial.Complete(step);
            _dispatched.Remove(step);

            if (step.Kind == BoardStepKind.Quarantine)
            {
                _waitingForBeat = false;
            }

            Publish();
        }

        public void Restart()
        {
            IsSecurityViewing = false;
            IsScanQueued = false;
            _waitingForBeat = false;
            _board.Restart();
            _tutorial.Reset();
            _dispatched.Clear();
            Publish();
        }

        public void ContinueTutorial()
        {
            _tutorial.Continue();
            Publish();
        }

        public void Scan()
        {
            if (CanRequestScan == false)
            {
                return;
            }

            if (IsSecurityViewing && _board.TryScan())
            {
                BoardStep scan = _board.PendingStep;
                _board.TryAcknowledge(scan.Generation, scan.Token);
                Publish();
                return;
            }

            IsScanQueued = true;

            if (IsBoarding)
            {
                _board.TryQueueScan();
            }

            Publish();
        }

        public void AdvanceBoardingBeat(float seconds)
        {
            if (IsPaused || IsSecurityViewing || _board.IsAwaitingBoardingBeat == false)
            {
                return;
            }

            if (_skipBeatFrame)
            {
                _skipBeatFrame = false;
                return;
            }

            BoardingBeatRemaining = Math.Max(0, BoardingBeatRemaining - Math.Max(0, seconds));

            if (BoardingBeatRemaining <= 0 && _board.TryContinueBoardingBeat())
            {
                Publish();
            }
        }

        public void Quarantine(string queueId)
        {
            if (CanUseSecurity && _board.TryQuarantine(queueId))
            {
                Publish();
            }
        }

        public void SetPaused(bool paused)
        {
            if (IsPaused && paused == false)
            {
                _skipBeatFrame = true;
            }

            IsPaused = paused;
            Publish();
        }

        public void Publish()
        {
            RefreshBeatClock();
            ExecuteQueuedScan();
            Snapshot = _board.GetSnapshot();
            Changed?.Invoke();

            if (IsPaused || IsSecurityViewing)
            {
                return;
            }

            List<BoardStep> steps = new List<BoardStep>(_board.BoardingSteps);

            if (steps.Count == 0 && _board.PendingStep != null)
            {
                steps.Add(_board.PendingStep);
            }

            foreach (BoardStep step in steps)
            {
                if (IsPaused || IsCurrent(step) == false || _dispatched.Add(step) == false)
                {
                    continue;
                }

                _tutorial.Observe(step);
                Changed?.Invoke();

                if (IsPaused || IsCurrent(step) == false)
                {
                    _dispatched.Remove(step);
                    continue;
                }

                StepRequested?.Invoke(step);
            }
        }

        private RowSnapshot FindInputRow(string busId)
        {
            if (IsPaused || IsSecurityViewing || _board.CanMove == false ||
                (IsBoarding == false && _tutorial.CanSelect(busId) == false))
            {
                return null;
            }

            for (int index = 0; index < Snapshot.Rows.Count; index++)
            {
                RowSnapshot row = Snapshot.Rows[index];

                if (row.Bus != null && row.Bus.Id == busId)
                {
                    return row;
                }
            }

            return null;
        }

        private void RefreshBeatClock()
        {
            bool waiting = _board.IsAwaitingBoardingBeat;

            if (waiting && _waitingForBeat == false)
            {
                BoardingBeatRemaining = _boardingBeatSeconds;
                _skipBeatFrame = true;
            }

            _waitingForBeat = waiting;
        }

        private void ExecuteQueuedScan()
        {
            if (IsScanQueued == false || IsPaused || IsSecurityViewing)
            {
                return;
            }

            if (PendingStep != null && PendingStep.Kind != BoardStepKind.Scan)
            {
                return;
            }

            IsScanQueued = false;

            if (PendingStep != null || _board.TryScan())
            {
                BoardStep scan = _board.PendingStep;
                _board.TryAcknowledge(scan.Generation, scan.Token);
                IsSecurityViewing = true;
            }
        }

        private bool IsCurrent(BoardStep step)
        {
            if (ReferenceEquals(step, _board.PendingStep))
            {
                return true;
            }

            foreach (BoardStep boarding in _board.BoardingSteps)
            {
                if (ReferenceEquals(step, boarding))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
