using UnityEngine;
using System.Collections;

/// <summary>
/// 검 차징 3단계: 거대한 180도 공격 (일반 공격 1타 모션 활용)
/// - 범위 및 VFX 300% 증가 (3.0배)
/// - 데미지 300% 증가 (3.0배)
/// </summary>
public class SwordChargeSkill3 : IChargeSkill
{
    private const float SIZE_MULT = 4.0f;
    private const float DAMAGE_MULT = 4.0f;

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

        Debug.Log($"[SwordCharge3] 거대 180도 스윙 — 데미지: {ctx.BaseDamage * DAMAGE_MULT:F0}, 사이즈: {SIZE_MULT}x");

        // 배율 설정
        ctx.WeaponBehaviour.ChargeSizeMultiplier = SIZE_MULT;
        ctx.WeaponBehaviour.ChargeDamageMultiplier = DAMAGE_MULT;
        
        // 매우 크고 묵직한 타격감
        ctx.WeaponBehaviour.ChargeSpeedMultiplier = 0.6f;
        
        if (CameraShakeController.Instance != null)
            CameraShakeController.Instance.Shake(0.1f, 0.2f); // 사전 진동

        // 1타(180도 스윙) 강제 실행
        PlayerWeaponController ctrl = ctx.PlayerTransform.GetComponent<PlayerWeaponController>();
        if (ctrl != null) ctrl.ForceBeginAttack(1);
        else ctx.WeaponBehaviour.BeginAttack(1);

        // 공격 중 추가 카메라 진동
        float duration = 0.5f;
        float elapsed = 0f;
        while (ctx.WeaponBehaviour.IsAttacking)
        {
            elapsed += Time.deltaTime;
            if (elapsed > 0.15f && CameraShakeController.Instance != null)
            {
                CameraShakeController.Instance.Shake(0.3f, 0.3f);
                elapsed = -999f; // 한번만 발동
            }
            yield return null;
        }

        ctx.WeaponBehaviour.ChargeSizeMultiplier = 1f;
        ctx.WeaponBehaviour.ChargeDamageMultiplier = 1f;
        ctx.WeaponBehaviour.ChargeSpeedMultiplier = 1f;

        PlayerWeaponController ctrl2 = ctx.PlayerTransform.GetComponent<PlayerWeaponController>();
        if (ctrl2 != null) ctrl2.ResetComboStep();
    }
}
