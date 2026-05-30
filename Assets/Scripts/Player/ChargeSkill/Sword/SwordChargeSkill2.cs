using UnityEngine;
using System.Collections;

/// <summary>
/// 검 차징 2단계: 360도 연속 2회 공격
/// - 데미지 150% 증가 (1.5배)
/// - 3초간 방어력 50% 증가
/// </summary>
public class SwordChargeSkill2 : IChargeSkill
{
    private const float DAMAGE_MULT = 1.5f;
    private const float DEFENSE_BUFF_MULT = 0.5f; // +50%
    private const float BUFF_DURATION = 3.0f;

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

        Debug.Log($"[SwordCharge2] 360도 2연속 스윙 — 데미지: {ctx.BaseDamage * DAMAGE_MULT:F0}");

        // 방어 버프
        if (ctx.StatSystem != null)
        {
            float defBonus = ctx.StatSystem.TotalDef * DEFENSE_BUFF_MULT;
            DefenseBuffEffect defBuff = ctx.PlayerTransform.gameObject.AddComponent<DefenseBuffEffect>();
            defBuff.Apply(ctx.StatSystem, defBonus, BUFF_DURATION);
        }

        // 1차 스윙
        ctx.WeaponBehaviour.ChargeDamageMultiplier = DAMAGE_MULT;
        ctx.WeaponBehaviour.ChargeSpeedMultiplier = 1.0f;

        PlayerWeaponController ctrl = ctx.PlayerTransform.GetComponent<PlayerWeaponController>();

        if (ctrl != null) ctrl.ForceBeginAttack(3);
        else ctx.WeaponBehaviour.BeginAttack(3);

        // 1차 스윙 완료 대기
        yield return new WaitWhile(() => ctx.WeaponBehaviour.IsAttacking);
        // FloatingWeaponMotion이 _wasAttacking을 false로 기록한 뒤
        // 새 공격(newAttack)을 정상 감지하도록 1프레임 대기
        yield return null;

        // 2차 스윙 (3타 = 360도 모션 재사용)
        if (ctrl != null) ctrl.ForceBeginAttack(3);
        else ctx.WeaponBehaviour.BeginAttack(3);
        yield return new WaitWhile(() => ctx.WeaponBehaviour.IsAttacking);

        // 배율 초기화
        ctx.WeaponBehaviour.ChargeSizeMultiplier = 1f;
        ctx.WeaponBehaviour.ChargeDamageMultiplier = 1f;
        ctx.WeaponBehaviour.ChargeSpeedMultiplier = 1f;

        // 스킬 종료 후 콤보 상태 리셋 (다음 일반공격이 1타부터 시작하도록)
        if (ctrl != null) ctrl.ResetComboStep();
    }
}
