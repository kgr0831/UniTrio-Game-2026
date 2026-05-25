using UnityEngine;

/// <summary>
/// 정적 유틸리티: 히트박스에서 호출하여 속성 디버프를 적용합니다.
/// DamageCalculator와 동일한 패턴으로, 디버프 적용 로직을 한 곳에서 관리합니다 (SRP).
/// </summary>
public static class DebuffApplier
{
    /// <summary>
    /// 대상에게 현재 속성의 디버프를 적용합니다.
    /// </summary>
    /// <param name="receiver">디버프 수신자 (몬스터)</param>
    /// <param name="element">적용할 속성</param>
    /// <param name="stats">공격자의 스탯 시스템</param>
    public static void Apply(DebuffReceiver receiver, ElementType element, StatSystem stats)
    {
        if (receiver == null || stats == null) return;

        float elementalAtk = stats.GetElementalAtk(element);

        // 대상의 속성 저항 가져오기
        var runtime = receiver.GetComponent<MonsterRuntimeData>();
        float resistance = runtime != null ? runtime.GetElementalResistance(element) : 0f;

        receiver.ApplyDebuff(element, elementalAtk, resistance);
    }

    // PlayerEntity 캐시 (매 호출마다 Find 방지)
    private static PlayerEntity _cachedPlayer;

    /// <summary>
    /// 투사체/폭발 등 StatSystem 참조가 없는 곳에서 사용하는 편의 메서드.
    /// Collider2D에서 DebuffReceiver를 자동 탐색하고, 플레이어의 현재 속성으로 디버프를 적용합니다.
    /// </summary>
    public static void ApplyFromProjectile(Collider2D targetCollider)
    {
        if (targetCollider == null) return;

        DebuffReceiver receiver = targetCollider.GetComponentInParent<DebuffReceiver>();
        if (receiver == null) return;

        var elemSystem = ElementalWeaponSystem.Instance;
        if (elemSystem == null) return;

        if (_cachedPlayer == null)
            _cachedPlayer = Object.FindObjectOfType<PlayerEntity>();
        if (_cachedPlayer == null) return;

        Apply(receiver, elemSystem.CurrentElement, _cachedPlayer.Stats);
    }

    /// <summary>
    /// IDamageable을 가진 GameObject에서 직접 DebuffReceiver를 탐색하여 디버프를 적용합니다.
    /// ExplosionEffect 등 OverlapCircle 기반 범위 공격에서 사용합니다.
    /// </summary>
    public static void ApplyFromProjectile(GameObject targetObj)
    {
        if (targetObj == null) return;

        DebuffReceiver receiver = targetObj.GetComponentInParent<DebuffReceiver>();
        if (receiver == null) return;

        var elemSystem = ElementalWeaponSystem.Instance;
        if (elemSystem == null) return;

        if (_cachedPlayer == null)
            _cachedPlayer = Object.FindObjectOfType<PlayerEntity>();
        if (_cachedPlayer == null) return;

        Apply(receiver, elemSystem.CurrentElement, _cachedPlayer.Stats);
    }
}
