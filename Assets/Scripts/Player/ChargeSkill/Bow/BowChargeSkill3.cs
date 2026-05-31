using UnityEngine;
using System.Collections;

/// <summary>
/// 활 차징 3단계: 2줄 연사 (2발 동시 × 8번 = 총 16발)
/// - 0.1초 간격으로 8번 발사
/// - 각 발사 시 ±5도 각도 차이로 2줄 발사
/// </summary>
public class BowChargeSkill3 : IChargeSkill
{
    private const int   VOLLEY_COUNT     = 8;
    private const float VOLLEY_INTERVAL  = 0.1f;
    private const float ANGLE_SPREAD     = 5f;   // ±5도
    private const float ARROW_SPEED      = 18f;
    private const float SIZE_MULTIPLIER  = 1.0f;

    public void Execute(ChargeSkillContext ctx)
    {
        ChargeSkillRunner runner = ctx.PlayerTransform.GetComponent<ChargeSkillRunner>();
        if (runner == null)
            runner = ctx.PlayerTransform.gameObject.AddComponent<ChargeSkillRunner>();

        runner.StartCoroutine(FireVolley(ctx));
    }

    private IEnumerator FireVolley(ChargeSkillContext ctx)
    {
        float damage = ctx.BaseDamage; // 기본 1타 데미지
        Color elementColor = ChargeSkillHelper.GetElementColor(ctx);

        Debug.Log($"[BowCharge3] 2줄 연사 시작 — {VOLLEY_COUNT}번 × 2발 = 총 {VOLLEY_COUNT * 2}발");

        for (int i = 0; i < VOLLEY_COUNT; i++)
        {
            // 2줄: 위쪽과 아래쪽
            SpawnPiercingArrow(ctx, ARROW_SPEED, damage, ANGLE_SPREAD, elementColor);
            SpawnPiercingArrow(ctx, ARROW_SPEED, damage, -ANGLE_SPREAD, elementColor);

            if (i < VOLLEY_COUNT - 1)
                yield return new WaitForSeconds(VOLLEY_INTERVAL);
        }

        Debug.Log($"[BowCharge3] 2줄 연사 완료!");
    }

    private void SpawnPiercingArrow(ChargeSkillContext ctx, float speed, float damage, float angleOffset, Color elementColor)
    {
        // 방향 계산
        float baseAngle = Mathf.Atan2(ctx.CursorDirection.y, ctx.CursorDirection.x) * Mathf.Rad2Deg;
        float finalAngle = baseAngle + angleOffset;
        Vector2 dir = new Vector2(Mathf.Cos(finalAngle * Mathf.Deg2Rad), Mathf.Sin(finalAngle * Mathf.Deg2Rad));

        // 기존 ctx의 CursorDirection을 복사/수정해서 넘기기엔 Context가 struct가 아님. 
        // 간단히 임시 Context를 만들어서 보냅니다.
        ChargeSkillContext tempCtx = ctx;
        tempCtx.CursorDirection = dir;

        ChargeSkillHelper.SpawnPiercingArrow(tempCtx, speed, damage, SIZE_MULTIPLIER, elementColor);
    }
}
