using UnityEngine;
using System.Collections;

/// <summary>
/// 창 차징 1단계: 찌르기 범위 2배 증가, 데미지 200% 증가 (3배)
/// </summary>
public class SpearChargeSkill1 : IChargeSkill
{
    private const float SIZE_MULT = 2.0f;
    private const float DAMAGE_MULT = 3.0f;

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

        Debug.Log($"[SpearCharge1] 강력한 찌르기 — 데미지: {ctx.BaseDamage * DAMAGE_MULT:F0}, 사이즈: {SIZE_MULT}x");

        ctx.WeaponBehaviour.ChargeSizeMultiplier = SIZE_MULT;
        ctx.WeaponBehaviour.ChargeDamageMultiplier = DAMAGE_MULT;
        ctx.WeaponBehaviour.ChargeSpeedMultiplier = 1.0f;

        SpearBehaviour spear = ctx.WeaponBehaviour as SpearBehaviour;
        if (spear != null) spear.ForceIsTip = true;

        PlayerWeaponController ctrl = ctx.PlayerTransform.GetComponent<PlayerWeaponController>();
        if (ctrl != null) ctrl.ForceBeginAttack(1);
        else ctx.WeaponBehaviour.BeginAttack(1);

        yield return new WaitWhile(() => ctx.WeaponBehaviour.IsAttacking);

        if (spear != null) spear.ForceIsTip = false;
        ctx.WeaponBehaviour.ChargeSizeMultiplier = 1f;
        ctx.WeaponBehaviour.ChargeDamageMultiplier = 1f;
        ctx.WeaponBehaviour.ChargeSpeedMultiplier = 1f;
    }
}
