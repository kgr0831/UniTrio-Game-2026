using UnityEngine;

/// <summary>
/// 모든 생명체(플레이어, 몬스터)의 공통 추상 기반 클래스.
/// HealthSystem 컴포넌트에 HP 관리를 위임하고, IDamageable 인터페이스를 구현합니다.
/// 파생 클래스는 OnDeath()만 구현하면 됩니다.
/// </summary>
[RequireComponent(typeof(HealthSystem))]
public abstract class LivingEntity : MonoBehaviour, IDamageable
{
    /// <summary>HP를 관리하는 컴포넌트. 외부에서 HP 이벤트 구독 시 사용합니다.</summary>
    public HealthSystem Health { get; private set; }

    public bool IsAlive => Health.IsAlive;

    protected virtual void Awake()
    {
        Health = GetComponent<HealthSystem>();
        Health.OnDied += HandleDeath;
    }

    /// <summary>
    /// 무적 상태 플래그. true이면 TakeDamage 호출이 무시됩니다.
    /// DashState 등에서 set합니다.
    /// </summary>
    public bool IsInvincible { get; set; } = false;

    /// <summary>외부에서 피해를 입힐 때의 진입점. source = 공격 주체 (선택).</summary>
    public virtual void TakeDamage(float damage, GameObject source = null)
    {
        if (IsInvincible || !IsAlive) return;
        float final = CalculateIncomingDamage(damage);
        Health.ApplyDamage(final);
    }

    /// <summary>
    /// 수신 측 보정(방어력, 카르마 등) 적용 지점.
    /// 기본 구현은 raw 그대로 전달. 파생 클래스에서 오버라이드합니다.
    /// </summary>
    protected virtual float CalculateIncomingDamage(float rawDamage) => rawDamage;

    private void HandleDeath() => OnDeath();

    /// <summary>
    /// 사망 시 처리 – 애니메이션 전환, 드롭, 풀 반환 등 각 파생 클래스에서 구현합니다.
    /// </summary>
    protected abstract void OnDeath();

    protected virtual void OnDestroy()
    {
        // 이벤트 누수 방지
        if (Health != null)
            Health.OnDied -= HandleDeath;
    }
}
