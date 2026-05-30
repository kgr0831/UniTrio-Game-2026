using UnityEngine;

/// <summary>
/// 활 차징 2단계: 거대 관통 화살
/// - 사이즈 400% 증가
/// - 데미지 400% (일반 1타 기준)
/// </summary>
public class BowChargeSkill2 : IChargeSkill
{
    private const float DAMAGE_MULTIPLIER = 4.0f;
    private const float SIZE_MULTIPLIER   = 4.0f;
    private const float ARROW_SPEED       = 12f; // 크기가 큰 만큼 약간 느리게

    public void Execute(ChargeSkillContext ctx)
    {
        float damage = ctx.BaseDamage * DAMAGE_MULTIPLIER;
        Color elementColor = ChargeSkillHelper.GetElementColor(ctx);

        SpawnPiercingArrow(ctx, damage, elementColor);

        if (CameraShakeController.Instance != null)
            CameraShakeController.Instance.Shake(0.2f, 0.3f);

        Debug.Log($"[BowCharge2] 거대 관통 화살 — 데미지: {damage:F0}, 사이즈: {SIZE_MULTIPLIER}x");
    }

    private void SpawnPiercingArrow(ChargeSkillContext ctx, float damage, Color elementColor)
    {
        ChargeSkillHelper.SpawnPiercingArrow(ctx, ARROW_SPEED, damage, SIZE_MULTIPLIER, elementColor);
    }
}
