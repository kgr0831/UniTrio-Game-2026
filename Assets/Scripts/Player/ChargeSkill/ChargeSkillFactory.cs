using System.Collections.Generic;

/// <summary>
/// WeaponType과 차징 단계에 따라 적절한 IChargeSkill 인스턴스를 반환하는 팩토리.
/// 내부적으로 딕셔너리에 캐싱하여 GC를 방지합니다. (OCP 준수)
/// </summary>
public static class ChargeSkillFactory
{
    // (WeaponType, ChargeLevel) → IChargeSkill 인스턴스 캐싱
    private static readonly Dictionary<(WeaponType, int), IChargeSkill> _cache
        = new Dictionary<(WeaponType, int), IChargeSkill>();

    /// <summary>
    /// 무기 타입과 차징 단계에 맞는 스킬을 반환합니다.
    /// 없으면 null을 반환합니다.
    /// </summary>
    public static IChargeSkill GetSkill(WeaponType weaponType, int chargeLevel)
    {
        var key = (weaponType, chargeLevel);
        if (_cache.TryGetValue(key, out IChargeSkill skill))
            return skill;

        skill = CreateSkill(weaponType, chargeLevel);
        if (skill != null)
            _cache[key] = skill;

        return skill;
    }

    private static IChargeSkill CreateSkill(WeaponType weaponType, int chargeLevel)
    {
        switch (weaponType)
        {
            case WeaponType.Sword:
                switch (chargeLevel)
                {
                    case 1: return new SwordChargeSkill1();
                    case 2: return new SwordChargeSkill2();
                    case 3: return new SwordChargeSkill3();
                }
                break;

            case WeaponType.Bow:
                switch (chargeLevel)
                {
                    case 1: return new BowChargeSkill1();
                    case 2: return new BowChargeSkill2();
                    case 3: return new BowChargeSkill3();
                }
                break;

            case WeaponType.Spear:
                switch (chargeLevel)
                {
                    case 1: return new SpearChargeSkill1();
                    case 2: return new SpearChargeSkill2();
                    case 3: return new SpearChargeSkill3();
                }
                break;

            case WeaponType.Staff:
                switch (chargeLevel)
                {
                    case 1: return new WandChargeSkill1();
                    case 2: return new WandChargeSkill2();
                    case 3: return new WandChargeSkill3();
                }
                break;
        }

        return null;
    }
}
