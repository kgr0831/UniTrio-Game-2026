using UnityEngine;

/// <summary>
/// 장신구 데이터. IBonusProvider를 구현하여 장착 시 StatSystem에 복합 스탯 보너스를 등록합니다.
/// ArmorData와 동일한 장착/해제 패턴 사용.
/// </summary>
[CreateAssetMenu(fileName = "NewAccessoryData", menuName = "Data/Items/Accessory")]
public class AccessoryData : ItemData, IBonusProvider
{
    [Header("Stat Bonuses")]
    [Tooltip("공격력 보너스")]
    public float AtkBonus;
    [Tooltip("방어력 보너스")]
    public float DefBonus;
    [Tooltip("이동속도 보너스")]
    public float SpeedBonus;
    [Tooltip("최대 마나 보너스")]
    public float ManaBonus;

    protected override void OnEnable()
    {
        Type = ItemType.Accessories;
    }

    public override string GetStatDescription() =>
        $"ATK +{AtkBonus}  DEF +{DefBonus}  SPD +{SpeedBonus}  MP +{ManaBonus}";

    // ── IBonusProvider ───────────────────────────────────────────────
    public float GetAttackBonus()  => AtkBonus;
    public float GetDefenseBonus() => DefBonus;
    public float GetSpeedBonus()   => SpeedBonus;
    public float GetManaBonus()    => ManaBonus;
}
