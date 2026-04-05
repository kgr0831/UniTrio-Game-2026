using UnityEngine;

/// <summary>
/// 피해를 받을 수 있는 모든 객체의 공통 인터페이스.
/// 타격 주체(source)를 함께 전달해 피격 반응·어그로 등 확장에 용이합니다.
/// </summary>
public interface IDamageable
{
    bool IsAlive { get; }

    /// <param name="damage">입힐 최종 데미지 수치</param>
    /// <param name="source">공격한 GameObject (선택). null 허용.</param>
    void TakeDamage(float damage, GameObject source = null);
}
