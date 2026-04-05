using UnityEngine;

/// <summary>
/// 플레이어의 체력·스탯·카르마를 통합하는 최상위 엔티티 컴포넌트.
/// CharacterBase(HP + Stat) + KarmaHandler를 조합해 플레이어 전용 피해 공식을 구현합니다.
///
/// 피격 공식: max(1, RawDamage - Def) * (1 + KarmaPoints * 0.1)
/// 공격 공식: (TotalAtk + WeaponBaseDmg) * 1.0
///
/// Milestone 2에서 PlayerStateMachine이 이 클래스를 상속합니다.
/// </summary>
[RequireComponent(typeof(KarmaHandler))]
public class PlayerEntity : CharacterBase, ISkillUser
{
    private KarmaHandler _karma;

    // ── 편의 프로퍼티 ─────────────────────────────────────────────

    public float CurrentMana => Stats.CurrentMana;
    public float MaxMana     => Stats.MaxMana;

    /// <summary>무기 히트박스 등 외부 공격 스크립트가 참조하는 공격력.</summary>
    public float TotalAtk => Stats.TotalAtk;

    // ── 초기화 ────────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        _karma = GetComponent<KarmaHandler>();
    }

    // ── 데미지 공식 ───────────────────────────────────────────────

    /// <summary>방어력 감산 후 카르마 배율을 추가 적용한 최종 피격 데미지.</summary>
    protected override float CalculateIncomingDamage(float rawDamage)
    {
        float karmaMulti = _karma.GetMultiplier();
        return DamageCalculator.CalcDamageTaken(rawDamage, Stats.TotalDef, karmaMulti);
    }

    // ── 사망 처리 ─────────────────────────────────────────────────

    protected override void OnDeath()
    {
        _karma.AddKarma(1);
        Debug.Log($"[Player] 사망 → 카르마 {_karma.KarmaPoints}pt");
        // Milestone 2: Death 상태 전환 처리 예정
    }

    // ── ISkillUser 구현 ───────────────────────────────────────────

    public void ConsumeMana(float amount) => Stats.ConsumeMana(amount);
}
