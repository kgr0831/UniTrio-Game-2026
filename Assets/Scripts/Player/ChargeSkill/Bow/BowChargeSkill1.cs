using UnityEngine;
using System.Collections;

/// <summary>
/// 활 차징 1단계: 적을 관통하는 화살 2연사
/// - 화살 사이즈 120% 증가
/// - 0.15초 간격으로 2발 연속 발사
/// </summary>
public class BowChargeSkill1 : IChargeSkill
{
    private const float SIZE_MULTIPLIER   = 1.2f;
    private const float ARROW_SPEED       = 15f;
    private const float SECOND_SHOT_DELAY = 0.15f;

    public void Execute(ChargeSkillContext ctx)
    {
        ChargeSkillRunner runner = ctx.PlayerTransform.GetComponent<ChargeSkillRunner>();
        if (runner == null)
            runner = ctx.PlayerTransform.gameObject.AddComponent<ChargeSkillRunner>();

        runner.StartCoroutine(FireSequence(ctx));
    }

    private IEnumerator FireSequence(ChargeSkillContext ctx)
    {
        float damage = ctx.BaseDamage; // 1단계는 데미지 배율 없음
        Color elementColor = ChargeSkillHelper.GetElementColor(ctx);

        // 1발째
        SpawnPiercingArrow(ctx, damage, elementColor);

        yield return new WaitForSeconds(SECOND_SHOT_DELAY);

        // 2발째
        SpawnPiercingArrow(ctx, damage, elementColor);

        Debug.Log($"[BowCharge1] 관통 화살 2연사 — 데미지: {damage:F0}/발, 사이즈: {SIZE_MULTIPLIER}x");
    }

    private void SpawnPiercingArrow(ChargeSkillContext ctx, float damage, Color elementColor)
    {
        ChargeSkillHelper.SpawnPiercingArrow(ctx, ARROW_SPEED, damage, SIZE_MULTIPLIER, elementColor);
    }
}
