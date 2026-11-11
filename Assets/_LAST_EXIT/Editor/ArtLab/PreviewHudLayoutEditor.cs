using LastExit.ArtLab.Preview;
using UnityEditor;
using UnityEngine;

namespace LastExit.Editor.ArtLab
{
    [CustomEditor(typeof(PreviewHudLayout))]
    public class PreviewHudLayoutEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            PreviewHudLayout layout = (PreviewHudLayout)target;
            EditorGUILayout.HelpBox("Оформление хранится в Portrait/Landscape Layout. Редактируйте эти assets в Inspector. " +
                "Для ручной правки RectTransform выключите Preview In Edit Mode, оформите HUD и сохраните нужный вариант кнопкой ниже.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(Application.isPlaying || layout.PreviewInEditMode))
            {
                CaptureButton("Записать текущий HUD → Portrait", layout, layout.PortraitLayout);
                CaptureButton("Записать текущий HUD → Landscape", layout, layout.LandscapeLayout);
            }
        }

        private static void CaptureButton(string label, PreviewHudLayout layout, PreviewHudLayoutPreset preset)
        {
            using (new EditorGUI.DisabledScope(preset == null))
            {
                if (GUILayout.Button(label))
                {
                    Undo.RecordObject(preset, "Capture HUD Layout");
                    layout.CaptureTo(preset);
                    EditorUtility.SetDirty(preset);
                    AssetDatabase.SaveAssets();
                }
            }
        }
    }
}
