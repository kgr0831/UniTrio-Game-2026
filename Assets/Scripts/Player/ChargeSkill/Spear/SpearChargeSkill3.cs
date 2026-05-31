using UnityEngine;
using System.Collections;

/// <summary>
/// 창 차징 3단계: 전방 돌진
/// - 크기 200% 증가 (3배), 데미지 400% 증가 (5배)
/// - 끝사거리 판정 강제
/// - 찌르기와 돌진 동시 발동, 돌진 0.4초 후 착지
/// - 무적 2초, 돌진 중 대시 애니메이션 재생
/// </summary>
public class SpearChargeSkill3 : IChargeSkill
{
    private const float DASH_DURATION       = 0.4f;
    private const float INVINCIBLE_DURATION = 2.0f;
    private const float DASH_SPEED_MULT     = 16f;   // 거리 2배
    private const float SIZE_MULT           = 3.0f;  // 200% 증가 = 3배
    private const float DAMAGE_MULT         = 5.0f;

    private static readonly Color DashTint = new Color(0.4f, 0.75f, 1f, 0.8f);
    private static readonly int   HashIsDashing = UnityEngine.Animator.StringToHash("IsDashing");

    public void Execute(ChargeSkillContext ctx)
    {
        ChargeSkillRunner runner = ctx.PlayerTransform.GetComponent<ChargeSkillRunner>();
        if (runner == null)
            runner = ctx.PlayerTransform.gameObject.AddComponent<ChargeSkillRunner>();

        runner.StartCoroutine(ExecuteSequence(ctx));
    }

    private IEnumerator ExecuteSequence(ChargeSkillContext ctx)
    {
        if (ctx.WeaponBehaviour == null) yield break;

        Debug.Log($"[SpearCharge3] 돌진 공격! 데미지: {ctx.BaseDamage * DAMAGE_MULT:F0}, 사이즈: {SIZE_MULT}x");

        PlayerMovement     move   = ctx.PlayerTransform.GetComponent<PlayerMovement>();
        LivingEntity       entity = ctx.PlayerTransform.GetComponent<LivingEntity>();
        SpearBehaviour     spear  = ctx.WeaponBehaviour as SpearBehaviour;
        Animator           anim   = ctx.PlayerTransform.GetComponent<Animator>();
        SpriteRenderer     sprite = ctx.PlayerTransform.GetComponent<SpriteRenderer>();

        // 무적 + 끝사거리 강제
        if (entity != null) entity.IsInvincible = true;
        if (spear  != null) spear.ForceIsTip    = true;

        // 무기 배율 설정 (ChargeSpeedMultiplier 높게 → 찌르기가 빠르게 시작되어 돌진과 동기화)
        ctx.WeaponBehaviour.ChargeSizeMultiplier   = SIZE_MULT;
        ctx.WeaponBehaviour.ChargeDamageMultiplier = DAMAGE_MULT;
        ctx.WeaponBehaviour.ChargeSpeedMultiplier  = 1.5f;

        PlayerWeaponController ctrl = ctx.PlayerTransform.GetComponent<PlayerWeaponController>();

        // ── 찌르기와 돌진 동시 발동 ──
        if (ctrl != null) ctrl.ForceBeginAttack(1);
        else ctx.WeaponBehaviour.BeginAttack(1);

        if (move  != null) move.DashVelocity = ctx.CursorDirection.normalized * DASH_SPEED_MULT;

        // 대시 애니메이션 + 색조 적용
        if (anim   != null) anim.SetBool(HashIsDashing, true);
        if (sprite != null) sprite.color = DashTint;

        yield return new WaitForSeconds(DASH_DURATION);

        // ── 돌진 종료 ──
        if (move   != null) move.DashVelocity = Vector2.zero;
        if (anim   != null) anim.SetBool(HashIsDashing, false);
        if (sprite != null) sprite.color = Color.white;

        ctx.WeaponBehaviour.OnDeactivated();
        ctx.WeaponBehaviour.ChargeSizeMultiplier   = 1f;
        ctx.WeaponBehaviour.ChargeDamageMultiplier = 1f;
        ctx.WeaponBehaviour.ChargeSpeedMultiplier  = 1f;

        // ── 무적 잔여 시간 유지 ──
        float invincibleRemaining = INVINCIBLE_DURATION - DASH_DURATION;
        if (invincibleRemaining > 0f)
            yield return new WaitForSeconds(invincibleRemaining);

        if (entity != null) entity.IsInvincible = false;
        if (spear  != null) spear.ForceIsTip    = false;
    }
}
