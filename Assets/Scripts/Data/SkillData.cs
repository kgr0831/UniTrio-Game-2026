using UnityEngine;

/// <summary>
/// 스킬 데이터. ItemData 계층에 편입되어 인벤토리·드래그앤드롭 시스템과 통합됩니다.
/// SkillSlotManager에 드래그 앤 드롭으로 장착하며, Q키로 시전합니다.
/// </summary>
[CreateAssetMenu(fileName = "NewSkillData", menuName = "Data/Items/Skill")]
public class SkillData : ItemData
{
    [Header("Skill Settings")]
    [Tooltip("시전 시 소모되는 마나")]
    public float ManaCost;
    [Tooltip("재사용 대기 시간 (초)")]
    public float Cooldown;

    protected override void OnEnable()
    {
        // 스킬 타입 자동 설정 (에디터에서 수동 변경 방지)
        Type = ItemType.Skill;
    }

    public override string GetStatDescription() =>
        $"마나 소모: {ManaCost} | 쿨다운: {Cooldown}s";
}
