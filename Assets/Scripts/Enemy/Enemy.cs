using UnityEngine;

/// <summary>
/// 기본 적 엔티티.
/// HP·피격 플래시는 HealthSystem 컴포넌트에 위임하고,
/// 사망 처리(파괴 또는 풀 반환)만 직접 담당합니다.
/// Milestone 4에서 MonsterBase 도입 시 CharacterBase로 변경 예정.
/// </summary>
public class Enemy : LivingEntity
{
    protected override void OnDeath()
    {
        Debug.Log($"[{gameObject.name}] 처치됨");
        // Milestone 4에서 오브젝트 풀링으로 교체 예정
        Destroy(gameObject);
    }
}
