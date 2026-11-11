using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using LastExit.Domain;
using UnityEngine;

namespace LastExit.Presentation
{
    // Read-only acceptance telemetry. No commands or QA solution are exposed to the player build.
    public static class LevelDiagnostics
    {
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void Report(ILevelTrace game, Camera camera, ILevelDiagnosticBoard board,
            ILevelDiagnosticControls controls)
        {
            BoardSnapshot snapshot = game.Snapshot;
            DiagnosticState state = new DiagnosticState();
            state.levelId = game.LevelId;
            state.hasNextLevel = game.HasNextLevel;
            state.arrivalVariantId = game.ArrivalVariantId;
            state.generation = snapshot.Generation;
            state.victory = snapshot.IsVictory;
            state.tutorial = game.TutorialStep.ToString();
            state.paused = game.IsPaused;
            state.phase = game.PendingStep == null ? "Ready" : game.PendingStep.Kind.ToString();
            List<DiagnosticBoarding> boarding = new List<DiagnosticBoarding>();

            foreach (BoardStep step in game.BoardingSteps)
            {
                Vector3 position = board.GetPassengerPosition(step.PassengerId);
                boarding.Add(new DiagnosticBoarding
                {
                    passengerId = step.PassengerId, queueId = step.QueueId, busId = step.BusId,
                    worldX = position.x, worldY = position.y, worldZ = position.z
                });
            }

            state.boarding = boarding.ToArray();
            state.trace = new List<string>(game.Trace).ToArray();
            state.continueButton = GetPoint(controls.GetContinuePosition());
            state.repeatButton = GetPoint(controls.GetRepeatPosition());
            state.restartButton = GetPoint(controls.GetRestartPosition());
            state.nextButton = GetPoint(controls.GetNextPosition());
            state.scanButton = GetPoint(controls.GetScanPosition());
            state.riskButton = GetPoint(controls.GetRiskPosition());
            state.quarantineButton = GetPoint(controls.GetQuarantinePosition());
            state.selectionButton = GetPoint(controls.GetSelectionPosition());
            state.closeButton = GetPoint(controls.GetClosePosition());
            state.queueButtons = new[] { GetPoint(controls.GetQueuePosition("A")),
                GetPoint(controls.GetQueuePosition("B")), GetPoint(controls.GetQueuePosition("C")),
                GetPoint(controls.GetQueuePosition("D")) };
            state.cctvPhase = controls.CctvPhase;
            state.cctvRemaining = controls.CctvRemaining;
            state.selectedQueue = controls.SelectedQueue;
            state.reserve = new List<string>(snapshot.NextBusIds).ToArray();
            state.lostSeats = snapshot.LostSeats;
            state.securityCheck = snapshot.HasSecurityCheck;
            state.defeat = snapshot.IsDefeat;
            state.stability = snapshot.Stability;
            state.cctvCharges = snapshot.CctvCharges;
            state.revealedEcho = false;
            List<DiagnosticBus> buses = new List<DiagnosticBus>();
            int remaining = 0;

            foreach (QueueSnapshot queue in snapshot.Queues)
            {
                remaining += queue.RemainingPassengers.Count;
                foreach (PassengerDefinition passenger in queue.RemainingPassengers)
                {
                    state.revealedEcho |= passenger.Kind != "human";
                }
            }

            state.remainingPassengers = remaining;

            foreach (RowSnapshot row in snapshot.Rows)
            {
                BusSnapshot bus = row.Bus;

                if (bus == null)
                {
                    continue;
                }

                DiagnosticBus entry = new DiagnosticBus();
                entry.id = bus.Id;
                entry.row = row.Id;
                entry.leftX = bus.LeftX;
                entry.occupied = bus.Occupied;
                entry.capacity = bus.Capacity;
                entry.lostSeats = bus.LostSeats;
                entry.remainingBuses = snapshot.NextBusIds.Count;
                Vector3 center = board.GetBusBounds(bus.Id).center;
                Vector3 screen = camera.WorldToScreenPoint(center);
                entry.x = screen.x;
                entry.y = camera.pixelHeight - screen.y;
                List<DiagnosticPoint> cells = new List<DiagnosticPoint>();

                for (int cell = 0; cell <= game.GridWidth - bus.FootprintCells; cell++)
                {
                    Vector3 position = center;
                    position.x += board.GetWorldX(cell) - board.GetWorldX(bus.LeftX);
                    Vector3 destination = camera.WorldToScreenPoint(position);
                    cells.Add(new DiagnosticPoint { cell = cell, x = destination.x,
                        y = camera.pixelHeight - destination.y });
                }

                entry.cells = cells.ToArray();
                buses.Add(entry);
            }

            state.buses = buses.ToArray();
#if UNITY_WEBGL && !UNITY_EDITOR
            LastExitReport(JsonUtility.ToJson(state));
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void LastExitReport(string json);
#endif

        private static DiagnosticPoint GetPoint(Vector2 position)
        {
            return new DiagnosticPoint { x = position.x, y = position.y };
        }

        [Serializable]
        private class DiagnosticState
        {
            public DiagnosticBoarding[] boarding;
            public string levelId;
            public bool hasNextLevel;
            public string arrivalVariantId;
            public int generation;
            public bool victory;
            public string tutorial;
            public bool paused;
            public string phase;
            public int remainingPassengers;
            public DiagnosticBus[] buses;
            public string[] trace;
            public DiagnosticPoint continueButton;
            public DiagnosticPoint repeatButton;
            public DiagnosticPoint restartButton;
            public DiagnosticPoint nextButton;
            public DiagnosticPoint scanButton;
            public DiagnosticPoint riskButton;
            public DiagnosticPoint quarantineButton;
            public DiagnosticPoint selectionButton;
            public DiagnosticPoint closeButton;
            public DiagnosticPoint[] queueButtons;
            public string cctvPhase;
            public float cctvRemaining;
            public string selectedQueue;
            public string[] reserve;
            public int lostSeats;
            public bool securityCheck;
            public bool defeat;
            public int stability;
            public int cctvCharges;
            public bool revealedEcho;
        }

        [Serializable]
        private class DiagnosticBoarding
        {
            public string passengerId;
            public string queueId;
            public string busId;
            public float worldX;
            public float worldY;
            public float worldZ;
        }

        [Serializable]
        private class DiagnosticBus
        {
            public string id;
            public string row;
            public int leftX;
            public int occupied;
            public int capacity;
            public int lostSeats;
            public int remainingBuses;
            public float x;
            public float y;
            public DiagnosticPoint[] cells;
        }

        [Serializable]
        private class DiagnosticPoint
        {
            public int cell;
            public float x;
            public float y;
        }
    }
}
