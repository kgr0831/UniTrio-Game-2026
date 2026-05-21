using UnityEngine;

/// <summary>
/// 기본 엔티티 클래스.
/// HP·피격 플래시는 HealthSystem 컴포넌트에 위임하고,
/// 사망 처리(파괴 또는 풀 반환)만 직접 담당합니다.
/// MonsterType을 통해 적대적/중립 엔티티를 구분합니다.
/// </summary>
public class Entity : LivingEntity
{
    [Header("Contact Damage")]
    [Tooltip("플레이어 접촉 시 주는 데미지")]
    [SerializeField] private float _contactDamage = 1f;
    [Tooltip("접촉 데미지 쿨다운 (초) — 같은 플레이어에게 연속 타격 방지")]
    [SerializeField] private float _contactDamageCooldown = 0.5f;

    private float _contactDamageTimer;

    private void Update()
    {
        // 타이머 방식 쿨다운 (GC 없음)
        if (_contactDamageTimer > 0f)
            _contactDamageTimer -= Time.deltaTime;
    }

    // OnCollisionStay2D: Rigidbody2D가 있는 오브젝트와 맞닿아 있는 동안 매 프레임 호출
    private void OnCollisionStay2D(Collision2D col)
    {
        if (!IsAlive || _contactDamageTimer > 0f) return;

        var damageable = col.gameObject.GetComponent<IDamageable>();
        if (damageable == null || !damageable.IsAlive) return;

        damageable.TakeDamage(_contactDamage, gameObject);
        _contactDamageTimer = _contactDamageCooldown;
    }

    protected override void OnDeath()
    {
        Debug.Log($"[{gameObject.name}] 처치됨");
        // Milestone 4에서 오브젝트 풀링으로 교체 예정
        Destroy(gameObject);
    }
}
