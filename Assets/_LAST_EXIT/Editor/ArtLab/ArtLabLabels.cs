using UnityEngine;
using UnityEngine.Rendering;

namespace LastExit.Editor.ArtLab
{
    public static class ArtLabLabels
    {
        public static void Build(Transform parent)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            foreach (Transform anchor in parent.GetComponentsInChildren<Transform>(true))
            {
                if (anchor.name.StartsWith("LABEL_") == false)
                {
                    continue;
                }

                TextMesh text = anchor.gameObject.AddComponent<TextMesh>();
                text.font = font;
                text.text = anchor.name.Substring(6).Replace('_', ' ');

                if (text.text.StartsWith("QUEUE"))
                {
                    text.text = text.text.Substring(text.text.Length - 1);
                }
                else if (text.text.StartsWith("QUARANTINE"))
                {
                    text.text = "КАРАНТИН Q-3";
                }

                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.fontSize = 80;
                text.characterSize = text.text.Length > 5 ? .043f : .09f;

                if (text.text == "ВЪЕЗД" || text.text == "ВЫЕЗД")
                {
                    text.characterSize = .06f;
                }

                text.color = new Color(.72f, .9f, .91f);
                MeshRenderer renderer = anchor.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = font.material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }
    }
}
