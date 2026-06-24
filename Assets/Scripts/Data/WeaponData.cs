using UnityEngine;

/// <summary>
/// 무기 전용 데이터. IBonusProvider를 통해 캐릭터 스탯에 합연산 보너스를 제공합니다.
/// </summary>
[CreateAssetMenu(fileName = "NewWeaponData", menuName = "Data/Items/Weapon")]
public class WeaponData : ItemData, IBonusProvider
{
    [Header("Weapon Identity")]
    public WeaponType WeaponType;

    [Header("Stat Bonuses (Additive +)")]
    public float AttackBonus;
    public float MagicAttackBonus;
    public float DefenseBonus;
    public float MoveSpeedBonus;
    public float ManaBonus;
    public float MaxHPBonus;
    public float AttackSpeedBonus;

    // --- IBonusProvider Implementation ---
    public float GetAttackBonus()      => AttackBonus;
    public float GetMagicAttackBonus() => MagicAttackBonus;
    public float GetDefenseBonus()     => DefenseBonus;
    public float GetSpeedBonus()       => MoveSpeedBonus;
    public float GetManaBonus()        => ManaBonus;
    public float GetMaxHPBonus()       => MaxHPBonus;
    public float GetAttackSpeedBonus() => AttackSpeedBonus;

    public override string GetStatDescription() 
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        if (AttackBonus != 0)      sb.Append($"공격력 +{AttackBonus} ");
        if (MagicAttackBonus != 0) sb.Append($"마공 +{MagicAttackBonus} ");
        if (MaxHPBonus != 0)       sb.Append($"체력 +{MaxHPBonus} ");
        if (AttackSpeedBonus != 0) sb.Append($"공속 +{AttackSpeedBonus} ");
        // ... 필요한 만큼 추가 가능
        return sb.ToString().Trim();
    }
}

