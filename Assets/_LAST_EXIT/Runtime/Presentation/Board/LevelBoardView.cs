using System;
using System.Collections;
using System.Collections.Generic;
using LastExit.Domain;
using UnityEngine;

namespace LastExit.Presentation
{
    // Instantiates shared art and maps integer cells to world coordinates. Never changes the rules.
    public class LevelBoardView : MonoBehaviour, ILevelBoardInput, ILevelSequenceBoard, ILevelHudBoard,
        ILevelInspectionBoard
    {
        // Four passenger positions fit on the marked platform in front of the station wall.
        private const int PlatformQueueSlots = 4;

        private readonly Dictionary<string, Transform> _buses = new Dictionary<string, Transform>();
        private readonly Dictionary<string, Transform> _passengers = new Dictionary<string, Transform>();
        private readonly Dictionary<string, Renderer[]> _passengerRenderers = new Dictionary<string, Renderer[]>();
        private readonly Dictionary<string, PassengerScanDistortion> _scanDistortions =
            new Dictionary<string, PassengerScanDistortion>();
        private readonly Dictionary<string, Transform> _doors = new Dictionary<string, Transform>();
        private readonly Dictionary<string, Renderer[]> _bodies = new Dictionary<string, Renderer[]>();
        private readonly Dictionary<string, Vector3> _queuePositions = new Dictionary<string, Vector3>();
        private readonly Dictionary<string, PassengerLocomotion> _locomotion =
            new Dictionary<string, PassengerLocomotion>();
        private readonly Dictionary<string, IPassengerBoardingFeedback> _boardingFeedback =
            new Dictionary<string, IPassengerBoardingFeedback>();
        [SerializeField] private GameObject _busTemplate;
        [SerializeField] private GameObject _standardBusTemplate;
        [SerializeField] private GameObject _longBusTemplate;
        [SerializeField] private GameObject _passengerTemplate;
        [SerializeField] private RuntimeAnimatorController _passengerController;
        [SerializeField] private Transform _actors;
        [SerializeField] private Material[] _routeMaterials;
        [SerializeField] private Material[] _emblemMaterials;
        [SerializeField] private float _originX = -5;
        [SerializeField, Min(.1f)] private float _cellSize = 1;
        [SerializeField] private float _rowStartZ = 2;
        [SerializeField, Min(1)] private float _rowSpacing = 3.7f;
        [SerializeField] private float _queueHeadZ = 6.6f;
        [SerializeField] private Vector3 _isolationPosition = new Vector3(-7, 0, 6.6f);
        [SerializeField, Min(.1f)] private float _queueSpacing = 1.35f;
        [SerializeField, Range(.1f, 1)] private float _doorPanelWidthCells = .8f;
        private bool _paused;
        private int _rowCount = 2;

        public float QueueHeadZ => _queueHeadZ;
        public float RowEndZ => _rowStartZ - (_rowCount - 1) * _rowSpacing;
        public float CellSize => _cellSize;

        public float GetWorldX(float cell)
        {
            return _originX + cell * _cellSize;
        }

        public float GetCellX(float worldX)
        {
            return (worldX - _originX) / _cellSize;
        }

        public Vector3 GetBusPosition(int rowIndex, int leftX)
        {
            return new Vector3(GetWorldX(leftX), 0, _rowStartZ - rowIndex * _rowSpacing);
        }

        public Transform GetBus(string id)
        {
            return _buses[id];
        }

        public Transform GetPassenger(string id)
        {
            return _passengers[id];
        }

        public Bounds GetPassengerBounds(string id)
        {
            return CombineBounds(_passengerRenderers[id]);
        }

        public void SetPassengerScan(string id, bool visible, float elapsed)
        {
            if (_scanDistortions.TryGetValue(id, out PassengerScanDistortion distortion))
            {
                distortion.SetFrame(visible, elapsed);
            }
        }

        public Vector3 GetDoor(string id)
        {
            return _doors[id].position;
        }

        public Vector3 GetIsolationPosition()
        {
            return _isolationPosition;
        }

        public Bounds GetBusBounds(string id)
        {
            return CombineBounds(_bodies[id]);
        }

        public void Clear()
        {
            foreach (Transform bus in _buses.Values)
            {
                bus.gameObject.SetActive(false);
                Destroy(bus.gameObject);
            }

            foreach (Transform passenger in _passengers.Values)
            {
                passenger.gameObject.SetActive(false);
                Destroy(passenger.gameObject);
            }

            _buses.Clear();
            _passengers.Clear();
            _passengerRenderers.Clear();
            _scanDistortions.Clear();
            _doors.Clear();
            _bodies.Clear();
            _queuePositions.Clear();
            _locomotion.Clear();
            _boardingFeedback.Clear();
        }

        public void Synchronize(BoardSnapshot snapshot)
        {
            _rowCount = snapshot.Rows.Count;
            HashSet<string> remainingBuses = new HashSet<string>();

            for (int rowIndex = 0; rowIndex < snapshot.Rows.Count; rowIndex++)
            {
                BusSnapshot bus = snapshot.Rows[rowIndex].Bus;

                if (bus == null)
                {
                    continue;
                }

                remainingBuses.Add(bus.Id);

                if (_buses.ContainsKey(bus.Id) == false)
                {
                    CreateBus(bus, rowIndex);
                }
            }

            RemoveMissing(_buses, remainingBuses, true);
            HashSet<string> remainingPassengers = new HashSet<string>();

            foreach (QueueSnapshot queue in snapshot.Queues)
            {
                for (int index = 0; index < queue.RemainingPassengers.Count; index++)
                {
                    PassengerDefinition passenger = queue.RemainingPassengers[index];
                    remainingPassengers.Add(passenger.Id);
                    int slot = Mathf.Min(index, PlatformQueueSlots);
                    Vector3 target = new Vector3(GetWorldX(queue.X), 0, _queueHeadZ + slot * _queueSpacing);

                    if (_passengers.ContainsKey(passenger.Id) == false)
                    {
                        GameObject instance = Instantiate(_passengerTemplate, _actors);
                        instance.name = passenger.Id;
                        instance.SetActive(true);
                        ApplyRoute(instance, passenger.Route);
                        instance.transform.position = target;
                        Animator animator = instance.GetComponentInChildren<Animator>();

                        if (animator == null || instance.TryGetComponent(out PassengerLocomotion locomotion) == false ||
                            instance.TryGetComponent(out PassengerBoardingFeedback feedback) == false)
                        {
                            throw new InvalidOperationException(
                                "Passenger template needs Animator, PassengerLocomotion and PassengerBoardingFeedback.");
                        }

                        animator.runtimeAnimatorController = _passengerController;
                        locomotion.Initialize(animator);
                        locomotion.SetPaused(_paused);
                        _locomotion.Add(passenger.Id, locomotion);
                        feedback.SetPaused(_paused);
                        _boardingFeedback.Add(passenger.Id, feedback);
                        _passengers.Add(passenger.Id, instance.transform);
                        _passengerRenderers.Add(passenger.Id, GetPassengerRenderers(instance));
                        _scanDistortions.Add(passenger.Id, new PassengerScanDistortion(animator.transform));
                    }

                    if (_queuePositions.TryGetValue(passenger.Id, out Vector3 previous) == false || previous != target)
                    {
                        // The tail waits behind the wall. The next arrival walks in from that same point.
                        foreach (Renderer renderer in _passengerRenderers[passenger.Id])
                        {
                            renderer.forceRenderingOff = index >= PlatformQueueSlots;
                        }

                        _locomotion[passenger.Id].MoveTo(target);
                        _queuePositions[passenger.Id] = target;
                    }
                }
            }

            RemoveMissing(_passengers, remainingPassengers, false);
        }

        public void SetPaused(bool paused)
        {
            _paused = paused;

            foreach (PassengerLocomotion locomotion in _locomotion.Values)
            {
                locomotion.SetPaused(paused);
            }

            foreach (IPassengerBoardingFeedback feedback in _boardingFeedback.Values)
            {
                feedback.SetPaused(paused);
            }
        }

        public IEnumerator WalkPassenger(string id, Vector3 target)
        {
            PassengerLocomotion locomotion = _locomotion[id];
            locomotion.MoveTo(target);

            while (locomotion.IsMoving)
            {
                yield return null;
            }
        }

        public void CancelPassengerMovement()
        {
            foreach (PassengerLocomotion locomotion in _locomotion.Values)
            {
                locomotion.Stop();
            }

            foreach (IPassengerBoardingFeedback feedback in _boardingFeedback.Values)
            {
                feedback.ResetFeedback();
            }
        }

        public IEnumerator PlayBoardingFeedback(string id)
        {
            return _boardingFeedback[id].Play();
        }

        public IEnumerator PlayCorruptionFeedback(string id)
        {
            return _boardingFeedback[id].PlayImpact();
        }

        private static Renderer[] GetPassengerRenderers(GameObject passenger)
        {
            List<Renderer> physical = new List<Renderer>();

            foreach (Renderer renderer in passenger.GetComponentsInChildren<Renderer>())
            {
                if (renderer is LineRenderer == false)
                {
                    physical.Add(renderer);
                }
            }

            return physical.ToArray();
        }

        private static Bounds CombineBounds(Renderer[] renderers)
        {
            Bounds bounds = renderers[0].bounds;

            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return bounds;
        }

        private static void EmphasizeDoor(GameObject instance)
        {
            MaterialPropertyBlock properties = new MaterialPropertyBlock();

            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                bool panel = renderer.name.EndsWith("_DoorPanel", StringComparison.Ordinal);
                bool threshold = renderer.name == "Entry Threshold Light";

                if (panel || threshold)
                {
                    renderer.GetPropertyBlock(properties);
                    properties.SetColor("_BaseColor", panel ? new Color(.025f, .055f, .06f) :
                        new Color(.94f, .97f, .9f));
                    renderer.SetPropertyBlock(properties);
                    properties.Clear();
                }
            }
        }

        private void CreateBus(BusSnapshot bus, int rowIndex)
        {
            GameObject template = bus.FootprintCells == 4 ? _longBusTemplate :
                bus.FootprintCells == 3 ? _standardBusTemplate : _busTemplate;

            if (template == null)
            {
                throw new InvalidOperationException("The required shared modular bus template is not assigned.");
            }

            GameObject instance = Instantiate(template, _actors);
            instance.name = bus.Id;
            Transform root = instance.transform;
            root.position = Vector3.zero;
            instance.SetActive(true);
            ApplyRoute(instance, bus.Route);
            List<Renderer> physical = new List<Renderer>();
            Transform door = null;

            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.name.StartsWith("LE_BUS_", StringComparison.Ordinal))
                {
                    physical.Add(renderer);
                }
            }

            foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.EndsWith("DOOR_ANCHOR", StringComparison.Ordinal))
                {
                    door = child;
                }
            }

            if (door == null || physical.Count == 0)
            {
                throw new InvalidOperationException("Bus template needs physical body renderers and a door anchor.");
            }

            Renderer[] renderers = physical.ToArray();
            Bounds bounds = CombineBounds(renderers);
            Vector3 scale = root.localScale;
            scale *= bus.FootprintCells * _cellSize / bounds.size.x;
            root.localScale = scale;
            bounds = CombineBounds(renderers);
            float centerX = (bus.FootprintCells - 1) * _cellSize * .5f;
            root.position += new Vector3(centerX - bounds.center.x, -bounds.min.y, -bounds.center.z);

            // DEC-002 calibration: presentation entry follows the same provisional grid door as the rules.
            float doorDelta = _cellSize - door.position.x;

            foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
            {
                if (child == door || child.name.StartsWith("Entry ", StringComparison.Ordinal) ||
                    child.name.EndsWith("_DoorPanel", StringComparison.Ordinal))
                {
                    child.position += new Vector3(doorDelta, 0, 0);

                    if (child.name.EndsWith("_DoorPanel", StringComparison.Ordinal))
                    {
                        Renderer panel = child.GetComponent<Renderer>();
                        float panelScale = Mathf.Min(1, _cellSize * _doorPanelWidthCells / panel.bounds.size.x);
                        child.localScale *= panelScale;
                        child.position += new Vector3(_cellSize - panel.bounds.center.x, 0, 0);
                    }
                }
            }

            EmphasizeDoor(instance);

            // Anchor the instance to its logical left cell while retaining the calibrated art offset.
            GameObject wrapper = new GameObject(bus.Id + " Position");
            wrapper.transform.SetParent(_actors, false);
            root.SetParent(wrapper.transform, true);
            wrapper.transform.position = GetBusPosition(rowIndex, bus.LeftX);
            _buses.Add(bus.Id, wrapper.transform);
            _doors.Add(bus.Id, door);
            _bodies.Add(bus.Id, renderers);
        }

        private void ApplyRoute(GameObject instance, string route)
        {
            int routeIndex = "RTBG".IndexOf(route, StringComparison.Ordinal);

            if (routeIndex < 0)
            {
                throw new ArgumentException("Unknown route: " + route);
            }

            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;

                for (int index = 0; index < materials.Length; index++)
                {
                    string materialName = materials[index].name;

                    if (materialName.Contains("Emblem"))
                    {
                        materials[index] = _emblemMaterials[routeIndex];
                    }
                    else if (materialName.Contains("Route_") || materialName.Contains("Suit_ROUTE") ||
                        materialName.Contains("RouteAccent"))
                    {
                        materials[index] = _routeMaterials[routeIndex];
                    }
                }

                renderer.sharedMaterials = materials;
            }
        }

        private void RemoveMissing(Dictionary<string, Transform> instances, HashSet<string> remaining, bool buses)
        {
            List<string> removed = new List<string>();

            foreach (KeyValuePair<string, Transform> instance in instances)
            {
                if (remaining.Contains(instance.Key) == false)
                {
                    instance.Value.gameObject.SetActive(false);
                    Destroy(instance.Value.gameObject);
                    removed.Add(instance.Key);
                }
            }

            foreach (string id in removed)
            {
                instances.Remove(id);

                if (buses)
                {
                    _doors.Remove(id);
                    _bodies.Remove(id);
                }
                else
                {
                    _queuePositions.Remove(id);
                    _locomotion.Remove(id);
                    _boardingFeedback.Remove(id);
                    _passengerRenderers.Remove(id);
                    _scanDistortions.Remove(id);
                }
            }
        }
    }
}
