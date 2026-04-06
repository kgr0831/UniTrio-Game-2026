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

    protected override void OnEnable()
    {
        Type = ItemType.Armor;
    }

    public override string GetStatDescription() =>
        $"DEF +{DefBonus}  ATK +{AtkBonus}  SPD +{SpeedBonus}  MP +{ManaBonus}";

    // ── IBonusProvider ───────────────────────────────────────────────
    public float GetAttackBonus()   => AtkBonus;
    public float GetDefenseBonus()  => DefBonus;
    public float GetSpeedBonus()    => SpeedBonus;
    public float GetManaBonus()     => ManaBonus;
}
