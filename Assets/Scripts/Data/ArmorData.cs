using UnityEngine;

/// <summary>
/// 방어구 데이터. IBonusProvider를 구현하여 장착 시 StatSystem에 보너스를 등록합니다.
/// 장착: StatSystem.RegisterBonus(this) / 해제: StatSystem.UnregisterBonus(this)
/// 매 프레임 스탯 재계산 없이 장착·해제 시점에만 트리거됩니다.
/// </summary>
[CreateAssetMenu(fileName = "NewArmorData", menuName = "Data/Items/Armor")]
public class ArmorData : ItemData, IBonusProvider
{
    [Header("Stat Bonuses")]
    [Tooltip("방어력 보너스")]
    public float DefBonus;
    [Tooltip("공격력 보너스 (특수 방어구)")]
    public float AtkBonus;
    [Tooltip("이동속도 보너스")]
    public float SpeedBonus;
    [Tooltip("최대 마나 보너스")]
    public float ManaBonus;
    [Tooltip("최대 체력 보너스")]
    public float MaxHPBonus;
    [Tooltip("공격 속도 보너스")]
    public float AtkSpeedBonus;

    protected override void OnEnable()
    {
        Type = ItemType.Armor;
    }

    public override string GetStatDescription() =>
        $"DEF +{DefBonus}  ATK +{AtkBonus}  HP +{MaxHPBonus}  SPD +{SpeedBonus}  MP +{ManaBonus}";

    // ── IBonusProvider ───────────────────────────────────────────────
    public float GetAttackBonus()   => AtkBonus;
    public float GetMagicAttackBonus() => 0f; // 방어구는 현재 마공 보너스 없음
    public float GetDefenseBonus()  => DefBonus;
    public float GetSpeedBonus()    => SpeedBonus;
    public float GetManaBonus()     => ManaBonus;
    public float GetMaxHPBonus()    => MaxHPBonus;
    public float GetAttackSpeedBonus() => AtkSpeedBonus;

}
