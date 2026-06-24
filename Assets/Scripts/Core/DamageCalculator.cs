using UnityEngine;

/// <summary>
/// 데미지 공식을 한 곳에서 관리하는 정적 유틸리티.
///
/// 공격 공식 : (StatAtk + ItemAtk) * Multiplier
/// 피격 공식 : max(1, Damage - Def) * KarmaMultiplier
/// 카르마     : 1점당 받는 데미지 10% 증폭
/// </summary>
public static class DamageCalculator
{
    /// <summary>최종 공격력 계산.</summary>
    /// <param name="statAtk">캐릭터 스탯 기반 공격력 (StatSystem.TotalAtk)</param>
    /// <param name="itemAtk">무기 고유 데미지 (WeaponData.Damage 등)</param>
    /// <param name="multiplier">버프/크리티컬 등 추가 배율</param>
    public static float CalcOutgoingDamage(float statAtk, float itemAtk = 0f, float multiplier = 1f)
    {
        return Mathf.Max(0f, (statAtk + itemAtk) * multiplier);
    }

    /// <summary>최종 피격 데미지 계산. 방어력 감산 후 최소 1 보장, 카르마 배율 적용.</summary>
    public static float CalcDamageTaken(float rawDamage, float defense, float karmaMultiplier = 1f)
    {
        float reduced = Mathf.Max(1f, rawDamage - defense);
        return reduced * karmaMultiplier;
    }

    /// <summary>카르마 포인트 → 데미지 배율. 1점당 10% 증가.</summary>
    public static float GetKarmaMultiplier(int karmaPoints)
    {
        return 1f + karmaPoints * 0.1f;
    }
}
