using UnityEditor;
using UnityEngine;

/// <summary>
/// ConsumableData 인스펙터에서 CookMethod가 None이 아닐 때만
/// 조리 관련 필드(CookingTime, CookedResult)를 표시하는 커스텀 에디터.
/// Inspector UX 향상: 불필요한 필드를 숨겨 기획자 실수를 방지합니다.
/// </summary>
[CustomEditor(typeof(ConsumableData))]
public class ConsumableDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 기본 ItemData 필드 (Id, Name, Description, Type, Icon, DropPrefab)
        DrawPropertiesExcluding(serializedObject,
            "m_Script", "HealAmount", "CookMethod", "CookingTime", "CookedResult");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Consumable Settings", EditorStyles.boldLabel);

        SerializedProperty healProp = serializedObject.FindProperty("HealAmount");
        EditorGUILayout.PropertyField(healProp);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Cooking Recipe (조리법)", EditorStyles.boldLabel);

        SerializedProperty cookMethodProp = serializedObject.FindProperty("CookMethod");
        EditorGUILayout.PropertyField(cookMethodProp);

        // CookMethod가 None이 아닐 때만 조리 관련 필드 노출
        if ((CookingMethod)cookMethodProp.enumValueIndex != CookingMethod.None)
        {
            EditorGUI.indentLevel++;

            SerializedProperty cookTimeProp = serializedObject.FindProperty("CookingTime");
            EditorGUILayout.PropertyField(cookTimeProp, new GUIContent("조리 시간 (초)"));

            SerializedProperty cookedResultProp = serializedObject.FindProperty("CookedResult");
            EditorGUILayout.PropertyField(cookedResultProp, new GUIContent("조리 결과물"));

            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
