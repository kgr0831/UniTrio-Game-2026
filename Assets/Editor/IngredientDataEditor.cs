using UnityEditor;
using UnityEngine;

/// <summary>
/// IngredientData 인스펙터에서 FuelCategory 설정 시
/// 연료 관련 정보를 시각적으로 표시하는 커스텀 에디터.
/// </summary>
[CustomEditor(typeof(IngredientData))]
public class IngredientDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 기본 ItemData 필드
        DrawPropertiesExcluding(serializedObject, "m_Script", "FuelCategory");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Fuel Settings (연료)", EditorStyles.boldLabel);

        SerializedProperty fuelProp = serializedObject.FindProperty("FuelCategory");
        EditorGUILayout.PropertyField(fuelProp, new GUIContent("연료 타입"));

        if ((FuelType)fuelProp.enumValueIndex != FuelType.None)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.HelpBox(
                "이 아이템은 연료로 사용됩니다.\n조리 시 1개씩 소모됩니다.",
                MessageType.Info);
            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
