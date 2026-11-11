using System;
using UnityEngine;

namespace LastExit.ArtLab.Preview
{
    [DefaultExecutionOrder(-300)]
    public class PreviewBus : MonoBehaviour, IPreviewBoardBus, IPreviewBusMotion
    {
        [SerializeField] private Renderer[] _physicalRenderers;
        [SerializeField] private Transform _door;
        [SerializeField] private string _route;
        [SerializeField] private int _capacity;

        public Vector3 DoorPosition => _door.position;
        public string Route => _route;
        public int Capacity => _capacity;
        public Vector3 InitialPosition { get; private set; }
        public Vector3 Position
        {
            get => transform.position;
            set => transform.position = value;
        }

        private void Awake()
        {
            InitialPosition = transform.position;
        }

        public void Initialize(Renderer[] physicalRenderers, Transform door, string route, int capacity)
        {
            if (physicalRenderers.Length == 0 || door == null || capacity <= 0)
            {
                throw new ArgumentException("Preview bus needs physical renderers, door and capacity.");
            }

            _physicalRenderers = (Renderer[])physicalRenderers.Clone();
            _door = door;
            _route = route;
            _capacity = capacity;
            InitialPosition = transform.position;
        }

        public Bounds GetPhysicalBounds()
        {
            Bounds bounds = _physicalRenderers[0].bounds;

            for (int index = 1; index < _physicalRenderers.Length; index++)
            {
                bounds.Encapsulate(_physicalRenderers[index].bounds);
            }

            return bounds;
        }
    }
}
