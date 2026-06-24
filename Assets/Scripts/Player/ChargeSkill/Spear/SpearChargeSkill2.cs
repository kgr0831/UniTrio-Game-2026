using UnityEngine;
using System.Collections;

/// <summary>
/// 창 차징 2단계: 3연속 찌르기 + 강력한 피니시
/// </summary>
public class SpearChargeSkill2 : IChargeSkill
{
    private const int   QUICK_THRUSTS      = 3;
    private const float THRUST_INTERVAL    = 0.1f;
    private const float FINISH_DELAY       = 0.5f;
    private const float FINISH_DAMAGE_MULT = 2.0f;
    private const float FINISH_SIZE_MULT   = 1.5f;

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

        Debug.Log($"[SpearCharge2] 3연속 찌르기 시작");

        PlayerWeaponController ctrl = ctx.PlayerTransform.GetComponent<PlayerWeaponController>();
        SpearBehaviour spear = ctx.WeaponBehaviour as SpearBehaviour;

        // ── 3회 연속 찌르기 (완료 후 0.1초 간격) ──
        for (int i = 0; i < QUICK_THRUSTS; i++)
        {
            ctx.WeaponBehaviour.ChargeSizeMultiplier = 1.0f;
            ctx.WeaponBehaviour.ChargeDamageMultiplier = 1.0f;
            ctx.WeaponBehaviour.ChargeSpeedMultiplier = 1.0f;

            if (ctrl != null) ctrl.ForceBeginAttack(1);
            else ctx.WeaponBehaviour.BeginAttack(1);

            yield return new WaitWhile(() => ctx.WeaponBehaviour.IsAttacking);
            yield return new WaitForSeconds(THRUST_INTERVAL);
        }

        // ── 0.5초 기 모으기 ──
        yield return new WaitForSeconds(FINISH_DELAY);

        // ── 피니시 찌르기 (끝사거리 강제 판정) ──
        Debug.Log($"[SpearCharge2] 피니시 찌르기 발동!");
        ctx.WeaponBehaviour.ChargeSizeMultiplier = FINISH_SIZE_MULT;
        ctx.WeaponBehaviour.ChargeDamageMultiplier = FINISH_DAMAGE_MULT;
        ctx.WeaponBehaviour.ChargeSpeedMultiplier = 0.8f;

        if (spear != null) spear.ForceIsTip = true;

        if (ctrl != null) ctrl.ForceBeginAttack(1);
        else ctx.WeaponBehaviour.BeginAttack(1);

        yield return new WaitWhile(() => ctx.WeaponBehaviour.IsAttacking);

        if (spear != null) spear.ForceIsTip = false;
        ctx.WeaponBehaviour.ChargeSizeMultiplier = 1f;
        ctx.WeaponBehaviour.ChargeDamageMultiplier = 1f;
        ctx.WeaponBehaviour.ChargeSpeedMultiplier = 1f;
    }
}
