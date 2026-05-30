using UnityEngine;
using System.Collections;

/// <summary>
/// 검 차징 1단계: 360도 공격 (일반 공격 3타 모션 활용)
/// - 공격 크기/범위 120% 증가 (1.2배)
/// - 데미지 175% 증가 (1.75배)
/// </summary>
public class SwordChargeSkill1 : IChargeSkill
{
    private const float SIZE_MULT = 1.2f;
    private const float DAMAGE_MULT = 1.75f;

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

        Debug.Log($"[SwordCharge1] 360도 스윙 — 데미지: {ctx.BaseDamage * DAMAGE_MULT:F0}, 사이즈: {SIZE_MULT}x");

        // 배율 설정
        ctx.WeaponBehaviour.ChargeSizeMultiplier = SIZE_MULT;
        ctx.WeaponBehaviour.ChargeDamageMultiplier = DAMAGE_MULT;
        
        // 약간 무겁게 (속도 감소)
        ctx.WeaponBehaviour.ChargeSpeedMultiplier = 0.9f;

        // 3타(360도 스윙) 강제 실행
        PlayerWeaponController ctrl = ctx.PlayerTransform.GetComponent<PlayerWeaponController>();
        if (ctrl != null) ctrl.ForceBeginAttack(3);
        else ctx.WeaponBehaviour.BeginAttack(3);

        yield return new WaitWhile(() => ctx.WeaponBehaviour.IsAttacking);

        ctx.WeaponBehaviour.ChargeSizeMultiplier = 1f;
        ctx.WeaponBehaviour.ChargeDamageMultiplier = 1f;
        ctx.WeaponBehaviour.ChargeSpeedMultiplier = 1f;

        if (ctrl != null) ctrl.ResetComboStep();
    }
}
