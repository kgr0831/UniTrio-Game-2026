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
    private JustDodgeController _justDodge;

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

        // 회피 저스트 컨트롤러 자동 보장 (씬/프리팹 수동 부착 없이도 동작)
        _justDodge = GetComponent<JustDodgeController>();
        if (_justDodge == null)
            _justDodge = gameObject.AddComponent<JustDodgeController>();

        // E-4 플레이어 피격음 (생존 시에만; 사망 블로우는 OnDeath에서 사망음 재생)
        if (Health != null) Health.OnHit += HandlePlayerHitSfx;
    }

    private void HandlePlayerHitSfx()
    {
        if (IsAlive && AudioManager.Instance != null)
            AudioManager.Instance.PlayPlayerHit();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (Health != null) Health.OnHit -= HandlePlayerHitSfx;
    }

    // ── 회피 저스트 가로채기 ───────────────────────────────────────

    /// <summary>
    /// 대시 무적 윈도우 중 적 공격이 닿으면 회피 저스트를 발동하고 해당 피해를 무시합니다.
    /// 그 외에는 기본 피격 처리(LivingEntity.TakeDamage)로 진행합니다.
    /// </summary>
    public override void TakeDamage(float damage, GameObject source = null)
    {
        if (IsAlive && source != null && _justDodge != null && _justDodge.TryConsumeDodge(source))
            return;

        base.TakeDamage(damage, source);
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

        // E-5 플레이어 사망음
        if (AudioManager.Instance != null) AudioManager.Instance.PlayPlayerDeath();

        Debug.Log($"[Player] 사망 → 카르마 {_karma.KarmaPoints}pt");
        // Milestone 2: Death 상태 전환 처리 예정
    }

    // ── ISkillUser 구현 ───────────────────────────────────────────

    public void ConsumeMana(float amount) => Stats.ConsumeMana(amount);
}
