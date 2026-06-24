using UnityEngine;

/// <summary>
/// 장비 종류를 결정하는 enum.
/// 장비 슬롯 필터링과 UI 분기에 사용됩니다.
/// OCP — 새 장비 종류 추가 시 enum 확장만으로 대응 가능.
/// </summary>
public enum GadgetType
{
    Helmet,
    Armor,
    Leggings,
    Accessories,
    FireElemental,
    IceElemental,
    EarthElemental
}

/// <summary>
/// 모든 장비(투구, 갑옷, 각반, 장신구, 원소석)의 통합 ScriptableObject.
/// SRP — 장비 데이터만 보유, 장착/해제 로직은 EquipmentManager가 담당.
/// ISP — IBonusProvider 인터페이스로 StatSystem과 느슨하게 결합.
/// OCP — GadgetType enum 확장만으로 새로운 장비 종류 추가 가능.
/// </summary>
[CreateAssetMenu(fileName = "NewGadgetData", menuName = "Data/Items/Gadget")]
public class GadgetData : ItemData, IBonusProvider
{
    [Header("Gadget Identity")]
    [Tooltip("이 장비의 종류 (장비 슬롯 필터링에 사용)")]
    public GadgetType GadgetType;

    [Header("Stat Bonuses (Additive +)")]
    [Tooltip("공격력 보너스")]
    public float AtkBonus;
    [Tooltip("마법 공격력 보너스")]
    public float MagicAtkBonus;
    [Tooltip("방어력 보너스")]
    public float DefBonus;
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
        // 장비 아이템은 ItemType.Armor로 통일 (기존 enum 확장 없이 GadgetType으로 세분화)
        Type = ItemType.Armor;
    }

    // ── IBonusProvider 구현 ─────────────────────────────────────
    // StatSystem.RegisterBonus/UnregisterBonus와 계약 (DIP 준수)
    public float GetAttackBonus()      => AtkBonus;
    public float GetMagicAttackBonus() => MagicAtkBonus;
    public float GetDefenseBonus()     => DefBonus;
    public float GetSpeedBonus()       => SpeedBonus;
    public float GetManaBonus()        => ManaBonus;
    public float GetMaxHPBonus()       => MaxHPBonus;
    public float GetAttackSpeedBonus() => AtkSpeedBonus;

    public override string GetStatDescription()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append($"[{GadgetType}] ");
        if (AtkBonus != 0)      sb.Append($"ATK +{AtkBonus} ");
        if (MagicAtkBonus != 0) sb.Append($"MATK +{MagicAtkBonus} ");
        if (DefBonus != 0)      sb.Append($"DEF +{DefBonus} ");
        if (MaxHPBonus != 0)    sb.Append($"HP +{MaxHPBonus} ");
        if (SpeedBonus != 0)    sb.Append($"SPD +{SpeedBonus} ");
        if (ManaBonus != 0)     sb.Append($"MP +{ManaBonus} ");
        if (AtkSpeedBonus != 0) sb.Append($"ASPD +{AtkSpeedBonus} ");
        return sb.ToString().Trim();
    }
}
