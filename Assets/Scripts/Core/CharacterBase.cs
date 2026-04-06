using UnityEngine;

/// <summary>
/// LivingEntity를 확장해 스탯(Atk, Def, MoveSpeed, Mana)을 추가한 추상 기반 클래스.
/// 플레이어와 보스 몬스터 모두 이 클래스를 상속합니다.
/// StatSystem 컴포넌트에 스탯 계산을 위임해 SRP를 유지합니다.
/// </summary>
[RequireComponent(typeof(StatSystem))]
public abstract class CharacterBase : LivingEntity
{
    /// <summary>스탯 계산 컴포넌트. 외부에서 버프 등록 시 사용합니다.</summary>
    public StatSystem Stats { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        Stats = GetComponent<StatSystem>();
    }

    /// <summary>
    /// 방어력을 반영한 최종 수신 데미지.
    /// 하위 클래스에서 카르마 등 추가 보정을 위해 다시 오버라이드할 수 있습니다.
    /// </summary>
    protected override float CalculateIncomingDamage(float rawDamage)
    {
        return DamageCalculator.CalcDamageTaken(rawDamage, Stats.TotalDef);
    }
}
