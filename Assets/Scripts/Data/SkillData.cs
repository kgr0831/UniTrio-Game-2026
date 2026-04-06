using UnityEngine;

// --- SkillData.cs ---
[CreateAssetMenu(fileName = "New Skill", menuName = "Inventory/Skill")]
public class SkillData : ScriptableObject, IUseable {
    public int id;
    public string skillName;
    public Sprite icon; // 에디터에서 드래그해서 넣으세요

    public int ID => id;
    public string Name => skillName;
    public Sprite Icon => icon;

    public void Use() { Debug.Log($"{skillName} 스킬을 시전합니다!"); }
}
