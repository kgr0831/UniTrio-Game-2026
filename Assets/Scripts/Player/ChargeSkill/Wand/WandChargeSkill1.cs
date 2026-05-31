using UnityEngine;

/// <summary>
/// 지팡이 차징 1단계: 강화 마법구
/// - 일반 공격 구체와 동일한 비주얼/폭발 이펙트
/// - 크기 50% 증가 (1.5배 스케일)
/// - 데미지 150% 증가 (2.5배)
/// </summary>
public class WandChargeSkill1 : IChargeSkill
{
    private const float DAMAGE_MULTIPLIER  = 2.5f;
    private const float SIZE_MULTIPLIER    = 2.0f;  // 구체 크기 2배 증가
    private const float PROJECTILE_SPEED   = 10f;

    public void Execute(ChargeSkillContext ctx)
    {
        var wand = ctx.WeaponBehaviour as WandBehaviour;
        if (wand == null || wand.ProjectilePrefab == null)
        {
            Debug.LogWarning("[WandCharge1] WandBehaviour에 ProjectilePrefab이 없습니다.");
            return;
        }

        float damage = ctx.BaseDamage * DAMAGE_MULTIPLIER;

        // 일반 공격과 동일한 프리팹으로 발사 (풀링 시스템 활용)
        GameObject projObj = SimpleObjectPool.Instance.Get(
            wand.ProjectilePrefab,
            ctx.PlayerTransform.position,
            Quaternion.identity);

        MagicProjectile mp = projObj.GetComponent<MagicProjectile>();
        if (mp != null)
        {
            mp.SetStats(PROJECTILE_SPEED, damage, ctx.CursorDirection);
            // 원본 스케일 기준으로 1.5배 확대 (투사체 + 폭발 범위 동시 적용)
            mp.SetSizeMultiplier(SIZE_MULTIPLIER);

            if (ctx.ElementSystem != null)
            {
                Color c = ctx.ElementSystem.GetCurrentAuraColor() * 1.5f;
                c.a = 1f;
                mp.SetElementColor(c);
            }
        }

        Debug.Log($"[WandCharge1] 강화 마법구 발사 — 데미지: {damage:F0}, 크기: {SIZE_MULTIPLIER}x");
    }
}
