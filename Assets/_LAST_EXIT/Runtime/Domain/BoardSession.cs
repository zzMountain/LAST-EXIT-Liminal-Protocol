using System;
using System.Collections.Generic;

namespace LastExit.Domain
{
    /// <summary>
    /// Owns deterministic state. Concurrent lanes complete one common boarding batch.
    /// A full bus retains its footprint until its Depart step is acknowledged.
    /// </summary>
    public class BoardSession
    {
        private const int ProvisionalDoorOffset = 1;

        private readonly LevelDefinition _definition;
        private readonly bool _boardingBeatEnabled;
        private readonly string[] _observedEchoHeads;
        private readonly Dictionary<string, BusDefinition> _buses = new Dictionary<string, BusDefinition>();
        private readonly List<BoardStep> _boardingSteps = new List<BoardStep>();
        private readonly string[] _activeBusIds;
        private readonly Dictionary<string, int> _lostSeatsByBus = new Dictionary<string, int>();
        private readonly int[] _heads;
        private readonly int[] _leftPositions;
        private readonly int[] _occupancies;
        private int _reserveIndex;
        private int _lostSeats;
        private string _lastIsolationResult;
        private int _isolationCount;
        private int _generation;
        private long _nextToken;
        private bool _isVictory;
        private bool _isDefeat;
        private bool _hasDeferredSecurityCheck;
        private int _securityQueueIndex = -1;
        private int _cctvCharges;
        private int _cctvUses;
        private int _stability;
        private int _emergencyResets;
        private bool _hasContinuedBoardingBeat;
        private bool _hasQueuedScan;
        private int _anomalyVersion;

        public BoardSession(LevelDefinition definition, bool? boardingBeatEnabled = null)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _boardingBeatEnabled = boardingBeatEnabled ?? definition.UsesBoardingBeat;
            _observedEchoHeads = new string[definition.Queues.Count];
            _heads = new int[definition.Queues.Count];
            _activeBusIds = new string[definition.Rows.Count];
            _leftPositions = new int[definition.Rows.Count];
            _occupancies = new int[definition.Rows.Count];
            BoardingSteps = _boardingSteps.AsReadOnly();

            foreach (BusDefinition bus in definition.Buses)
            {
                _buses.Add(bus.Id, bus);
                _lostSeatsByBus.Add(bus.Id, 0);
            }

            Restart();
        }

        public BoardStep PendingStep { get; private set; }

        public bool IsAwaitingBoardingBeat { get; private set; }

        /// <summary>Active immutable boarding commands, ordered by queue A to D.</summary>
        public IReadOnlyList<BoardStep> BoardingSteps { get; }

        public bool CanMove => _isVictory == false && _isDefeat == false && _securityQueueIndex < 0 &&
            (PendingStep == null || PendingStep.Kind == BoardStepKind.Board);

        // GDD §6; DEC-009 did not add a separate star penalty for lost Human seats.
        public int ResultStars => _isVictory == false ? 0 : _emergencyResets > 0 ? 1 :
            _cctvUses <= _definition.TargetCctvUses ? 3 : 2;

        private bool CanAct => PendingStep == null && _isVictory == false && _isDefeat == false;

        /// <summary>The gameplay clock calls this once the neutral interval has elapsed.</summary>
        public bool TryContinueBoardingBeat()
        {
            if (CanAct == false || IsAwaitingBoardingBeat == false)
            {
                return false;
            }

            IsAwaitingBoardingBeat = false;
            _hasContinuedBoardingBeat = true;
            Resolve(continueBoardingPhase: true);
            return true;
        }

        public bool TryScan()
        {
            if (CanAct == false || _cctvCharges <= 0)
            {
                return false;
            }

            IssueStep(BoardStepKind.Scan);

            if (_boardingBeatEnabled)
            {
                CommitScan();
            }

            return true;
        }

        /// <summary>Reserves intent only; the charge is spent at the next safe common boundary.</summary>
        public bool TryQueueScan()
        {
            if (_boardingBeatEnabled == false || _boardingSteps.Count == 0 || _cctvCharges <= 0
                || _isVictory || _isDefeat)
            {
                return false;
            }

            _hasQueuedScan = true;
            return true;
        }

        public bool TryQuarantine(string queueId)
        {
            int queueIndex = FindQueueIndex(queueId);

            if (CanAct == false || queueIndex < 0 || GetHead(queueIndex) == null)
            {
                return false;
            }

            IssueStep(BoardStepKind.Quarantine, queueIndex: queueIndex);
            return true;
        }

        public bool TryAcceptRisk()
        {
            if (CanAct == false || _securityQueueIndex < 0)
            {
                return false;
            }

            LaneAccess access = GetLaneAccess(_definition.Queues[_securityQueueIndex].Id);
            IssueStep(BoardStepKind.Corruption, FindRowIndex(access.FirstRowId), _securityQueueIndex);
            return true;
        }

        public bool TryMoveBus(string rowId, int leftX)
        {
            if (CanMoveBus(rowId, leftX) == false)
            {
                return false;
            }

            int rowIndex = FindRowIndex(rowId);
            int previousX = _leftPositions[rowIndex];
            _leftPositions[rowIndex] = leftX;

            if (PendingStep != null)
            {
                // Active walkers retain their immutable commands and stationary doors.
                Resolve();
                return true;
            }

            IssueStep(BoardStepKind.Move, rowIndex, -1, previousX);
            return true;
        }

        /// <summary>Checks the entire horizontal sweep, including a continuous drag preview.</summary>
        public bool CanMoveBus(string rowId, float leftX)
        {
            int rowIndex = FindRowIndex(rowId);

            if (CanMove == false || rowIndex < 0)
            {
                return false;
            }

            BusDefinition bus = GetCurrentBus(rowIndex);

            if (bus == null || (leftX >= 0 && leftX <= _definition.Width - bus.FootprintCells) == false
                || _occupancies[rowIndex] + _lostSeatsByBus[bus.Id] >= bus.Capacity)
            {
                return false;
            }

            if (PendingStep == null)
            {
                return true;
            }

            float sweepLeft = Math.Min(_leftPositions[rowIndex], leftX);
            float sweepRight = Math.Max(_leftPositions[rowIndex], leftX) + bus.FootprintCells;

            foreach (BoardStep boarding in _boardingSteps)
            {
                int boardingRowIndex = FindRowIndex(boarding.RowId);

                if (rowIndex == boardingRowIndex)
                {
                    return false;
                }

                int laneX = _definition.Corridors[boarding.QueueId];

                if (rowIndex < boardingRowIndex && laneX >= sweepLeft && laneX < sweepRight)
                {
                    return false;
                }
            }

            return true;
        }

        public bool TryAcknowledge(int generation, long token)
        {
            if (_boardingSteps.Count > 0)
            {
                for (int index = 0; index < _boardingSteps.Count; index++)
                {
                    BoardStep boarding = _boardingSteps[index];

                    if (boarding.Generation == generation && boarding.Token == token)
                    {
                        _boardingSteps.RemoveAt(index);
                        PendingStep = _boardingSteps.Count > 0 ? _boardingSteps[0] : null;
                        CommitBoarding(boarding);
                        return true;
                    }
                }

                return false;
            }

            if (PendingStep == null || PendingStep.Generation != generation || PendingStep.Token != token)
            {
                return false;
            }

            BoardStep completed = PendingStep;
            PendingStep = null;

            switch (completed.Kind)
            {
                case BoardStepKind.Move:
                case BoardStepKind.Arrive:
                    Resolve();
                    break;
                case BoardStepKind.Depart:
                    CommitDeparture(completed);
                    break;
                case BoardStepKind.Scan:
                    if (_boardingBeatEnabled == false)
                    {
                        CommitScan();
                    }

                    break;
                case BoardStepKind.Quarantine:
                    CommitIsolation(completed);
                    break;
                case BoardStepKind.Corruption:
                    CommitIsolation(completed);
                    break;
            }

            return true;
        }

        public void Restart()
        {
            _generation++;
            _boardingSteps.Clear();
            PendingStep = null;
            _isVictory = false;
            _isDefeat = false;
            _hasDeferredSecurityCheck = false;
            IsAwaitingBoardingBeat = false;
            _hasContinuedBoardingBeat = false;
            _hasQueuedScan = false;
            _anomalyVersion = 0;
            Array.Clear(_observedEchoHeads, 0, _observedEchoHeads.Length);
            _securityQueueIndex = -1;
            _cctvCharges = _definition.CctvCharges;
            _cctvUses = 0;
            _stability = _definition.InitialStability;
            _emergencyResets = 0;
            Array.Clear(_heads, 0, _heads.Length);
            _reserveIndex = 0;
            _lostSeats = 0;
            _lastIsolationResult = null;
            _isolationCount = 0;

            foreach (BusDefinition bus in _definition.Buses)
            {
                _lostSeatsByBus[bus.Id] = 0;
            }

            Array.Clear(_occupancies, 0, _occupancies.Length);

            for (int rowIndex = 0; rowIndex < _definition.Rows.Count; rowIndex++)
            {
                _activeBusIds[rowIndex] = _definition.Rows[rowIndex].InitialBusId;
                _leftPositions[rowIndex] = GetCurrentBus(rowIndex).InitialLeftX;
            }

            Resolve();
        }

        /// <summary>Returns independent immutable values, never live mutable state.</summary>
        public BoardSnapshot GetSnapshot()
        {
            List<RowSnapshot> rows = new List<RowSnapshot>();
            List<QueueSnapshot> queues = new List<QueueSnapshot>();

            for (int rowIndex = 0; rowIndex < _definition.Rows.Count; rowIndex++)
            {
                BusDefinition bus = GetCurrentBus(rowIndex);
                BusSnapshot snapshot = bus == null
                    ? null
                    : new BusSnapshot(bus, _leftPositions[rowIndex], _occupancies[rowIndex], _lostSeatsByBus[bus.Id]);

                rows.Add(new RowSnapshot(_definition.Rows[rowIndex].Id, snapshot));
            }

            for (int queueIndex = 0; queueIndex < _definition.Queues.Count; queueIndex++)
            {
                QueueDefinition queue = _definition.Queues[queueIndex];
                List<PassengerDefinition> remaining = new List<PassengerDefinition>();

                for (int passengerIndex = _heads[queueIndex]; passengerIndex < queue.Passengers.Count;
                    passengerIndex++)
                {
                    PassengerDefinition passenger = queue.Passengers[passengerIndex];
                    remaining.Add(new PassengerDefinition(passenger.Id, passenger.Route));
                }

                queues.Add(new QueueSnapshot(queue.Id, _definition.Corridors[queue.Id], remaining));
            }

            List<string> reserve = new List<string>();

            for (int index = _reserveIndex; index < _definition.NextBusIds.Count; index++)
            {
                reserve.Add(_definition.NextBusIds[index]);
            }

            return new BoardSnapshot(_generation, _isVictory, rows, queues, _cctvCharges, _cctvUses,
                _stability, _emergencyResets, _isDefeat, _securityQueueIndex >= 0, reserve, _lostSeats,
                _lastIsolationResult, _isolationCount, IsAwaitingBoardingBeat, _anomalyVersion);
        }

        public bool IsCctvAnomaly(string queueId)
        {
            int queueIndex = FindQueueIndex(queueId);
            PassengerDefinition passenger = queueIndex < 0 ? null : GetHead(queueIndex);
            return passenger != null && passenger.Kind == PassengerDefinition.EchoKind;
        }

        public LaneAccess GetLaneAccess(string queueId)
        {
            return EvaluateLaneAccess(queueId, -1, 0);
        }

        /// <summary>Evaluates one hypothetical snap without issuing a move or resolving passengers.</summary>
        public LaneAccess PreviewLaneAccess(string queueId, string rowId, int leftX)
        {
            int rowIndex = FindRowIndex(rowId);

            if (rowIndex < 0)
            {
                throw new ArgumentException("Unknown row.", nameof(rowId));
            }

            BusDefinition bus = GetCurrentBus(rowIndex);

            if (bus == null)
            {
                throw new ArgumentException("Cannot preview a departed bus.", nameof(rowId));
            }

            if (leftX < 0 || leftX > _definition.Width - bus.FootprintCells)
            {
                throw new ArgumentException("Preview position is outside the grid.", nameof(leftX));
            }

            return EvaluateLaneAccess(queueId, rowIndex, leftX);
        }

        private LaneAccess EvaluateLaneAccess(string queueId, int overrideRowIndex, int overrideLeftX)
        {
            int queueIndex = FindQueueIndex(queueId);

            if (queueIndex < 0)
            {
                if (_definition.Corridors.ContainsKey(queueId))
                {
                    return new LaneAccess(queueId, null, null, LaneAccessReason.EmptyQueue);
                }

                throw new ArgumentException("Unknown active queue.", nameof(queueId));
            }

            QueueDefinition queue = _definition.Queues[queueIndex];

            if (_heads[queueIndex] >= queue.Passengers.Count)
            {
                return new LaneAccess(queueId, null, null, LaneAccessReason.EmptyQueue);
            }

            int laneX = _definition.Corridors[queueId];
            int rowIndex = FindFirstBlocker(laneX, overrideRowIndex, overrideLeftX);

            if (rowIndex < 0)
            {
                return new LaneAccess(queueId, null, null, LaneAccessReason.NoBus);
            }

            BusDefinition bus = GetCurrentBus(rowIndex);
            string rowId = _definition.Rows[rowIndex].Id;
            int leftX = rowIndex == overrideRowIndex ? overrideLeftX : _leftPositions[rowIndex];
            LaneAccessReason reason = GetAccessReason(queueIndex, rowIndex, laneX, leftX, bus);
            return new LaneAccess(queueId, rowId, bus.Id, reason);
        }

        private void CommitScan()
        {
            _cctvCharges--;
            _cctvUses++;
        }

        private void CommitIsolation(BoardStep completed)
        {
            int queueIndex = FindQueueIndex(completed.QueueId);
            PassengerDefinition passenger = GetHead(queueIndex);
            _isolationCount++;
            _lastIsolationResult = completed.Kind == BoardStepKind.Corruption ? "RiskAccepted" :
                passenger.Kind == PassengerDefinition.HumanKind ? "HumanLost" : "EchoRemoved";
            _heads[queueIndex]++;
            _securityQueueIndex = -1;

            if (_boardingBeatEnabled && completed.Kind == BoardStepKind.Quarantine)
            {
                IsAwaitingBoardingBeat = false;
                _hasContinuedBoardingBeat = false;
            }

            if (completed.Kind == BoardStepKind.Corruption)
            {
                _stability--;
                _emergencyResets++;

                if (_stability == 0)
                {
                    _isDefeat = true;
                    IssueStep(BoardStepKind.Defeat);
                    return;
                }
            }
            else if (passenger.Kind == PassengerDefinition.HumanKind)
            {
                AssignLostSeat(passenger.Route);
                _stability--;

                if (_stability == 0)
                {
                    _isDefeat = true;
                    IssueStep(BoardStepKind.Defeat);
                    return;
                }
            }

            _hasDeferredSecurityCheck = FindReadyEchoQueue() >= 0;
            Resolve(continueBoardingPhase: true);
        }

        private void CommitBoarding(BoardStep completed)
        {
            int rowIndex = FindRowIndex(completed.RowId);
            int queueIndex = FindQueueIndex(completed.QueueId);
            _heads[queueIndex]++;
            _occupancies[rowIndex]++;
            _hasDeferredSecurityCheck = FindReadyEchoQueue() >= 0;
            Resolve(continueBoardingPhase: true);
        }

        private void CommitDeparture(BoardStep completed)
        {
            int rowIndex = FindRowIndex(completed.RowId);
            _activeBusIds[rowIndex] = null;
            _occupancies[rowIndex] = 0;

            Resolve();
        }

        private void Resolve(bool continueBoardingPhase = false)
        {
            if (_boardingSteps.Count > 0)
            {
                // DEC-012: neither new heads nor newly opened lanes join an active batch.
                return;
            }

            ObserveAnomalyHeads();

            if (_hasQueuedScan)
            {
                _hasQueuedScan = false;
                IsAwaitingBoardingBeat = true;
                TryScan();
                return;
            }

            if (_boardingBeatEnabled && IsAwaitingBoardingBeat)
            {
                return;
            }

            if (_boardingBeatEnabled && FindReadyEchoQueue() >= 0)
            {
                DispatchBoardingBeat();
                return;
            }

            if (continueBoardingPhase)
            {
                if (TryIssueDeferredSecurityCheck())
                {
                    return;
                }

                DispatchBoarding();

                if (_boardingSteps.Count > 0 || _securityQueueIndex >= 0 || PendingStep != null
                    || IsAwaitingBoardingBeat)
                {
                    return;
                }
            }

            for (int rowIndex = 0; rowIndex < _definition.Rows.Count; rowIndex++)
            {
                BusDefinition bus = GetCurrentBus(rowIndex);

                if (bus != null && _occupancies[rowIndex] + _lostSeatsByBus[bus.Id] == bus.Capacity)
                {
                    IssueStep(BoardStepKind.Depart, rowIndex);
                    return;
                }
            }

            for (int rowIndex = 0; rowIndex < _definition.Rows.Count; rowIndex++)
            {
                if (GetCurrentBus(rowIndex) == null && _reserveIndex < _definition.NextBusIds.Count)
                {
                    string busId = _definition.NextBusIds[_reserveIndex++];
                    _activeBusIds[rowIndex] = busId;
                    _leftPositions[rowIndex] = _buses[busId].InitialLeftX;
                    IssueStep(BoardStepKind.Arrive, rowIndex);
                    return;
                }
            }

            if (TryIssueDeferredSecurityCheck())
            {
                return;
            }

            DispatchBoarding();

            if (_boardingSteps.Count > 0 || _securityQueueIndex >= 0 || PendingStep != null
                || IsAwaitingBoardingBeat)
            {
                return;
            }

            for (int queueIndex = 0; queueIndex < _definition.Queues.Count; queueIndex++)
            {
                if (_heads[queueIndex] < _definition.Queues[queueIndex].Passengers.Count)
                {
                    return;
                }
            }

            for (int rowIndex = 0; rowIndex < _definition.Rows.Count; rowIndex++)
            {
                if (GetCurrentBus(rowIndex) != null)
                {
                    return;
                }
            }

            _isVictory = true;
            IssueStep(BoardStepKind.Victory);
        }

        private bool TryIssueDeferredSecurityCheck()
        {
            if (_boardingBeatEnabled)
            {
                return false;
            }

            if (_hasDeferredSecurityCheck == false)
            {
                return false;
            }

            _hasDeferredSecurityCheck = false;
            int queueIndex = FindReadyEchoQueue();

            if (queueIndex < 0)
            {
                return false;
            }

            _securityQueueIndex = queueIndex;
            return true;
        }

        private int FindReadyEchoQueue()
        {
            for (int queueIndex = 0; queueIndex < _definition.Queues.Count; queueIndex++)
            {
                PassengerDefinition passenger = GetHead(queueIndex);

                if (passenger != null && passenger.Kind == PassengerDefinition.EchoKind
                    && GetLaneAccess(_definition.Queues[queueIndex].Id).CanBoard)
                {
                    return queueIndex;
                }
            }

            return -1;
        }

        private void DispatchBoarding()
        {
            if (_boardingBeatEnabled)
            {
                DispatchBoardingBeat();
                return;
            }

            for (int queueIndex = 0; queueIndex < _definition.Queues.Count; queueIndex++)
            {
                string queueId = _definition.Queues[queueIndex].Id;

                LaneAccess access = GetLaneAccess(queueId);

                if (access.CanBoard == false)
                {
                    continue;
                }

                PassengerDefinition passenger = GetHead(queueIndex);

                if (passenger.Kind == PassengerDefinition.EchoKind)
                {
                    _hasDeferredSecurityCheck = _boardingSteps.Count > 0;

                    if (_boardingSteps.Count == 0)
                    {
                        _securityQueueIndex = queueIndex;
                    }

                    return;
                }

                IssueStep(BoardStepKind.Board, FindRowIndex(access.FirstRowId), queueIndex);
            }
        }

        private void ObserveAnomalyHeads()
        {
            bool hasNewAnomaly = false;

            for (int queueIndex = 0; queueIndex < _heads.Length; queueIndex++)
            {
                PassengerDefinition head = GetHead(queueIndex);
                string echoId = head != null && head.Kind == PassengerDefinition.EchoKind ? head.Id : null;

                if (echoId != null && echoId != _observedEchoHeads[queueIndex])
                {
                    hasNewAnomaly = true;
                }

                _observedEchoHeads[queueIndex] = echoId;
            }

            if (hasNewAnomaly)
            {
                _anomalyVersion++;
            }
        }

        private void DispatchBoardingBeat()
        {
            bool hasEligibleHead = false;

            foreach (QueueDefinition queue in _definition.Queues)
            {
                if (GetLaneAccess(queue.Id).CanBoard)
                {
                    hasEligibleHead = true;
                    break;
                }
            }

            if (hasEligibleHead == false)
            {
                _hasContinuedBoardingBeat = false;
                return;
            }

            if (_hasContinuedBoardingBeat == false)
            {
                IsAwaitingBoardingBeat = true;
                return;
            }

            int echoQueueIndex = FindReadyEchoQueue();

            if (echoQueueIndex >= 0)
            {
                LaneAccess access = GetLaneAccess(_definition.Queues[echoQueueIndex].Id);
                IssueStep(BoardStepKind.Corruption, FindRowIndex(access.FirstRowId), echoQueueIndex);
                return;
            }

            _hasContinuedBoardingBeat = false;

            for (int queueIndex = 0; queueIndex < _definition.Queues.Count; queueIndex++)
            {
                LaneAccess access = GetLaneAccess(_definition.Queues[queueIndex].Id);

                if (access.CanBoard)
                {
                    IssueStep(BoardStepKind.Board, FindRowIndex(access.FirstRowId), queueIndex);
                }
            }
        }

        private int GetReservedSeats(string busId)
        {
            int reservedSeats = 0;

            foreach (BoardStep boarding in _boardingSteps)
            {
                if (boarding.BusId == busId)
                {
                    reservedSeats++;
                }
            }

            return reservedSeats;
        }

        private LaneAccessReason GetAccessReason(int queueIndex, int rowIndex, int laneX, int leftX, BusDefinition bus)
        {
            if (leftX + ProvisionalDoorOffset != laneX)
            {
                return LaneAccessReason.DoorMisaligned;
            }

            if (bus.Route != _definition.Queues[queueIndex].Passengers[_heads[queueIndex]].Route)
            {
                return LaneAccessReason.RouteMismatch;
            }

            if (_occupancies[rowIndex] + _lostSeatsByBus[bus.Id] + GetReservedSeats(bus.Id) >= bus.Capacity)
            {
                return LaneAccessReason.Full;
            }

            if (PendingStep != null && PendingStep.RowId == _definition.Rows[rowIndex].Id
                && (PendingStep.Kind == BoardStepKind.Arrive || PendingStep.Kind == BoardStepKind.Depart))
            {
                return LaneAccessReason.Unavailable;
            }

            return LaneAccessReason.Ready;
        }

        private int FindFirstBlocker(int laneX, int overrideRowIndex, int overrideLeftX)
        {
            for (int rowIndex = 0; rowIndex < _definition.Rows.Count; rowIndex++)
            {
                BusDefinition bus = GetCurrentBus(rowIndex);
                int leftX = rowIndex == overrideRowIndex ? overrideLeftX : _leftPositions[rowIndex];

                if (bus != null && leftX <= laneX && laneX < leftX + bus.FootprintCells)
                {
                    return rowIndex;
                }
            }

            return -1;
        }

        private BusDefinition GetCurrentBus(int rowIndex)
        {
            string busId = _activeBusIds[rowIndex];
            return busId == null ? null : _buses[busId];
        }

        private void AssignLostSeat(string route)
        {
            for (int rowIndex = 0; rowIndex < _definition.Rows.Count; rowIndex++)
            {
                BusDefinition bus = GetCurrentBus(rowIndex);

                if (bus != null && bus.Route == route
                    && _occupancies[rowIndex] + _lostSeatsByBus[bus.Id] < bus.Capacity)
                {
                    RecordLostSeat(bus.Id);
                    return;
                }
            }

            for (int index = _reserveIndex; index < _definition.NextBusIds.Count; index++)
            {
                BusDefinition bus = _buses[_definition.NextBusIds[index]];

                if (bus.Route == route && _lostSeatsByBus[bus.Id] < bus.Capacity)
                {
                    RecordLostSeat(bus.Id);
                    return;
                }
            }

            throw new InvalidOperationException("No free seat remains for an incinerated Human route.");
        }

        private void RecordLostSeat(string busId)
        {
            _lostSeatsByBus[busId]++;
            _lostSeats++;
        }

        private PassengerDefinition GetHead(int queueIndex)
        {
            QueueDefinition queue = _definition.Queues[queueIndex];
            int head = _heads[queueIndex];
            return head < queue.Passengers.Count ? queue.Passengers[head] : null;
        }

        private int FindRowIndex(string rowId)
        {
            for (int index = 0; index < _definition.Rows.Count; index++)
            {
                if (_definition.Rows[index].Id == rowId)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindQueueIndex(string queueId)
        {
            for (int index = 0; index < _definition.Queues.Count; index++)
            {
                if (_definition.Queues[index].Id == queueId)
                {
                    return index;
                }
            }

            return -1;
        }

        private void IssueStep(BoardStepKind kind, int rowIndex = -1, int queueIndex = -1, int fromLeftX = 0)
        {
            string rowId = rowIndex >= 0 ? _definition.Rows[rowIndex].Id : null;
            string busId = rowIndex >= 0 ? GetCurrentBus(rowIndex).Id : null;
            string queueId = queueIndex >= 0 ? _definition.Queues[queueIndex].Id : null;
            string passengerId = queueIndex >= 0
                ? _definition.Queues[queueIndex].Passengers[_heads[queueIndex]].Id : null;
            int leftX = rowIndex >= 0 ? _leftPositions[rowIndex] : 0;
            BoardStep step = new BoardStep(kind, _generation, ++_nextToken, rowId, busId,
                queueId, passengerId, fromLeftX, leftX);

            if (kind == BoardStepKind.Board)
            {
                int insertIndex = 0;

                while (insertIndex < _boardingSteps.Count
                    && FindQueueIndex(_boardingSteps[insertIndex].QueueId) < queueIndex)
                {
                    insertIndex++;
                }

                _boardingSteps.Insert(insertIndex, step);
                PendingStep = _boardingSteps[0];
                return;
            }

            PendingStep = step;
        }
    }
}
