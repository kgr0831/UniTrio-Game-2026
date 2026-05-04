using UnityEngine;

/// <summary>
/// 스킬 데이터. ItemData 계층에 편입되어 인벤토리·드래그앤드롭 시스템과 통합됩니다.
/// 스킬 트리 패널에서 QuickSlot으로 드래그 앤 드롭하여 배치하며, 해당 숫자키로 시전합니다.
/// </summary>
[CreateAssetMenu(fileName = "NewSkillData", menuName = "Data/Items/Skill")]
public class SkillData : ItemData
{
    [Header("Prefab Reference")]
    [Tooltip("실제 스킬 로직과 제원(SkillBase)이 담긴 프리팹")]
    public GameObject SkillPrefab;

    [Header("Legacy / UI Settings (Optional)")]
    [Tooltip("시전 시 소모되는 마나 (프리팹이 없을 때만 사용)")]
    public float ManaCost;
    [Tooltip("재사용 대기 시간 (초) (프리팹이 없을 때만 사용)")]
    public float Cooldown;
    [Tooltip("사용에 필요한 무기 타입 (프리팹이 없을 때만 사용)")]
    public WeaponType RequiredWeapon;

    protected override void OnEnable()
    {
        // 스킬 타입 자동 설정 (에디터에서 수동 변경 방지)
        Type = ItemType.Skill;
    }

    public virtual void Execute(GameObject player)
    {
        Debug.Log($"[Skill] {Name} executed by {player.name}");
    }

    public override string GetStatDescription()
    {
        if (SkillPrefab != null)
        {
            var skill = SkillPrefab.GetComponent<SkillBase>();
            if (skill != null)
                return $"필요 무기: {skill.RequiredWeapon} | 마나: {skill.ManaCost} | 쿨다운: {skill.Cooldown}s";
        }
        return $"필요 무기: {RequiredWeapon} | 마나: {ManaCost} | 쿨다운: {Cooldown}s";
    }
}

