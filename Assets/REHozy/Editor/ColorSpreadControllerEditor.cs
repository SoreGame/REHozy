using REHozy.Rendering;
using UnityEditor;
using UnityEngine;

namespace REHozy.Editor
{
    [CustomEditor(typeof(ColorSpreadController))]
    public sealed class ColorSpreadControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var controller = (ColorSpreadController)target;

            EditorGUI.BeginChangeCheck();
            var enabled = EditorGUILayout.ToggleLeft("Эффект включён (сцена и игра)", controller.EffectEnabled);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(controller, "Toggle Color Spread Effect");
                controller.SetEffectEnabled(enabled);
                EditorUtility.SetDirty(controller);
                SceneView.RepaintAll();
            }

            if (!controller.EffectEnabled)
                EditorGUILayout.HelpBox(
                    "Эффект выключен — сцена и Game View без Color Spread.",
                    MessageType.Info);

            EditorGUILayout.Space(6f);

            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "effectEnabled");
            serializedObject.ApplyModifiedProperties();
        }
    }
}
