using System;
using UnityEngine;

namespace LastExit.ArtLab.Preview
{
    [ExecuteAlways]
    [DefaultExecutionOrder(-200)]
    public class PreviewComposition : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private PreviewPointerInput _input;
        [SerializeField] private RectTransform[] _uiRegions;
        [SerializeField] private PreviewBusDrag _drag;
        [SerializeField] private PreviewBus[] _buses;
        [SerializeField] private PreviewBusBadge[] _badges;
        [SerializeField] private PreviewLaneGuide[] _lanes;
        [SerializeField] private ArtLabPreviewHud _hud;
        [SerializeField] private PreviewHudLayout _layout;
        [SerializeField] private PreviewCameraFraming _framing;

        private void OnEnable()
        {
            // An unconfigured component can be added in the Inspector before its references are assigned.
            if (_hud != null)
            {
                Bind();
            }
        }

        public void Initialize(Camera camera, PreviewPointerInput input, RectTransform[] uiRegions, PreviewBusDrag drag,
            PreviewBus[] buses, PreviewBusBadge[] badges, PreviewLaneGuide[] lanes,
            ArtLabPreviewHud hud, PreviewHudLayout layout, PreviewCameraFraming framing)
        {
            _camera = camera;
            _input = input;
            _uiRegions = (RectTransform[])uiRegions.Clone();
            _drag = drag;
            _buses = (PreviewBus[])buses.Clone();
            _badges = (PreviewBusBadge[])badges.Clone();
            _lanes = (PreviewLaneGuide[])lanes.Clone();
            _hud = hud;
            _layout = layout;
            _framing = framing;
            Bind();
        }

        private void Bind()
        {
            if (_buses.Length != _badges.Length)
            {
                throw new ArgumentException("Each preview bus needs exactly one badge in the same order.");
            }

            _input.Initialize(_camera, _uiRegions);
            _drag.Initialize(_camera, _input, _buses);

            for (int index = 0; index < _buses.Length; index++)
            {
                _badges[index].Bind(_buses[index]);
            }

            foreach (PreviewLaneGuide lane in _lanes)
            {
                lane.Bind(_buses);
            }

            _hud.Initialize(_camera);
            _hud.Bind(_drag, _layout, _framing, _badges, _lanes);
        }
    }
}
