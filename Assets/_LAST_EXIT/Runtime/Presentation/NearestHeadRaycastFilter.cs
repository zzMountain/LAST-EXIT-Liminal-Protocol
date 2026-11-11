using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LastExit.Presentation
{
    // Enlarged touch targets may overlap on a narrow board. Pointer position, not sibling order, owns the hit.
    public class NearestHeadRaycastFilter : MonoBehaviour, ICanvasRaycastFilter
    {
        private Button _owner;
        private IReadOnlyList<Button> _targets;

        public void Initialize(Button owner, IReadOnlyList<Button> targets)
        {
            _owner = owner;
            _targets = targets;
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            Button nearest = null;
            float nearestDistance = float.PositiveInfinity;

            if (_targets == null)
            {
                return false;
            }

            // The supplied order A→D also resolves an exactly equidistant pointer deterministically.
            foreach (Button target in _targets)
            {
                if (target == null || target.IsActive() == false || target.IsInteractable() == false)
                {
                    continue;
                }

                RectTransform rect = (RectTransform)target.transform;

                if (RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, eventCamera) == false)
                {
                    continue;
                }

                Vector2 center = RectTransformUtility.WorldToScreenPoint(eventCamera, rect.TransformPoint(rect.rect.center));
                float distance = (screenPoint - center).sqrMagnitude;

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = target;
                }
            }

            return nearest != null && nearest == _owner;
        }
    }
}
