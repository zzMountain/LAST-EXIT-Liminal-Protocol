using UnityEngine;
using UnityEngine.UI;

namespace LastExit.ArtLab.Preview
{
    // Use the render target too: editor captures must match the corresponding runtime viewport.
    public class PreviewCanvasScaler : CanvasScaler
    {
        [SerializeField, Min(1)] private float _portraitReferenceWidth = 720;

        public void RefreshScale()
        {
            Handle();
        }

        protected override void HandleScaleWithScreenSize()
        {
            Canvas canvas = GetComponent<Canvas>();
            Camera camera = canvas.worldCamera;

            if (camera == null)
            {
                base.HandleScaleWithScreenSize();
                return;
            }

            if (camera.pixelWidth <= 0 || camera.pixelHeight <= 0)
            {
                return;
            }

            float widthScale = camera.pixelWidth / referenceResolution.x;
            float heightScale = camera.pixelHeight / referenceResolution.y;
            float scale = camera.aspect < 1
                ? camera.pixelWidth / _portraitReferenceWidth
                : Mathf.Sqrt(widthScale * heightScale);
            if (scale <= 0 || float.IsFinite(scale) == false)
            {
                return;
            }

            SetScaleFactor(scale);
            SetReferencePixelsPerUnit(referencePixelsPerUnit);
        }
    }
}
