using UnityEngine;
using System.Collections;

/// <summary>
/// 지팡이 차징 3단계: 집중 폭발
/// - 4초간 힘을 모아 폭발 (반경 3칸 원형)
/// - 4초 동안: 이동 불가, 방어력 TotalDef × 2.0, 범위 내 적 75% 슬로우
/// - 데미지 700% (일반 1타 기준)
/// - 전용 차징 셰이더 연출
/// </summary>
public class WandChargeSkill3 : IChargeSkill
{
    private const float DAMAGE_MULTIPLIER       = 7.0f;
    private const float CHARGE_DURATION         = 4.0f;
    private const float EXPLOSION_RADIUS        = 4.5f;
    private const float DEFENSE_BUFF_MULTIPLIER = 1.0f; // +100% (TotalDef 기준)
    private const float SLOW_PERCENT            = 0.75f;
    private const float SLOW_RADIUS             = 4.5f;

    public void Execute(ChargeSkillContext ctx)
    {
        ChargeSkillRunner runner = ctx.PlayerTransform.GetComponent<ChargeSkillRunner>();
        if (runner == null)
            runner = ctx.PlayerTransform.gameObject.AddComponent<ChargeSkillRunner>();

        runner.StartCoroutine(ExecuteSequence(ctx));
    }

    private static readonly Collider2D[] _slowResults   = new Collider2D[32];
    private const float SLOW_MULTIPLIER = 0.25f; // 75% 감소 → 25% 속도 유지

    private IEnumerator ExecuteSequence(ChargeSkillContext ctx)
    {
        float damage = ctx.BaseDamage * DAMAGE_MULTIPLIER;
        Color elementColor = ChargeSkillHelper.GetElementColor(ctx);

        Debug.Log($"[WandCharge3] 집중 폭발 시작 — 데미지: {damage:F0}, 반경: {EXPLOSION_RADIUS}, 집중: {CHARGE_DURATION}초");

        if (ctx.PlayerMovement != null)
            ctx.PlayerMovement.SpeedMultiplier = 0f;

        LivingEntity livingEntity = ctx.PlayerTransform.GetComponent<LivingEntity>();
        if (livingEntity != null)
            livingEntity.IsInvincible = true;

        DefenseBuffEffect defBuff = null;
        if (ctx.StatSystem != null)
        {
            float defBonus = ctx.StatSystem.TotalDef * DEFENSE_BUFF_MULTIPLIER;
            defBuff = ctx.PlayerTransform.gameObject.AddComponent<DefenseBuffEffect>();
            defBuff.Apply(ctx.StatSystem, defBonus, CHARGE_DURATION + 0.5f);
        }

        // ── 고유 차징 이펙트 (빨려 들어가는 불규칙 파티클) ──
        GameObject gatherVfx = new GameObject("Wand3_GatherVFX");
        gatherVfx.transform.position = ctx.PlayerTransform.position;
        gatherVfx.transform.SetParent(ctx.PlayerTransform); // 따라다님

        ParticleSystem ps = gatherVfx.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = CHARGE_DURATION;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(-10f, -4f); // 강하게 빨려들어옴
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
        main.startColor = elementColor;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.loop = true;

        var emission = ps.emission;
        emission.rateOverTime = 0f; // Update 루프에서 점진적 증가

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 5f;
        shape.radiusThickness = 0.2f;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f)));

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Custom/VFXLit2D"));
        renderer.material.SetFloat("_EmissionIntensity", 4f);
        renderer.material.SetColor("_EmissionColor", elementColor);
        renderer.material.SetFloat("_LightInfluence", 0.3f);
        renderer.sortingLayerName = "Weapons";
        renderer.sortingOrder = 25;

        ps.Play();

        // ── 4초간 집중 + 범위 내 적 슬로우 ──
        float elapsed = 0f;
        while (elapsed < CHARGE_DURATION)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / CHARGE_DURATION);

            // 파티클 생성량 증가 (불규칙하게 폭주)
            var em = ps.emission;
            em.rateOverTime = Mathf.Lerp(20f, 150f, progress) + Mathf.PingPong(Time.time * 50f, 50f);

            // 범위 내 적 슬로우 (남은 차징 시간만큼 지속)
            float remaining = CHARGE_DURATION - elapsed;
            ApplySlowToNearbyEnemies(ctx.PlayerTransform.position, SLOW_RADIUS, remaining + 0.1f);

            yield return null;
        }

        // 차징 파티클 정지
        ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        Object.Destroy(gatherVfx, 2f);

        // ── 폭발! ──
        Vector2 center = (Vector2)ctx.PlayerTransform.position;
        int hitCount = ChargeSkillHelper.ApplyAreaDamage(center, EXPLOSION_RADIUS, damage, ctx.PlayerTransform.gameObject);

        // ── 원형 쇼크웨이브 VFX ──
        SpawnCircleShockwave(center, EXPLOSION_RADIUS, elementColor);

        // 강력한 카메라 쉐이크 + 히트스탑
        if (CameraShakeController.Instance != null)
            CameraShakeController.Instance.Shake(0.5f, 0.7f);
        if (HitStopManager.Instance != null && hitCount > 0)
            HitStopManager.Instance.TriggerHitStop(0.15f);

        Debug.Log($"[WandCharge3] 집중 폭발 완료 — 데미지: {damage:F0}, 적중: {hitCount}");

        // ── 상태 복원 ──
        if (ctx.PlayerMovement != null)
            ctx.PlayerMovement.SpeedMultiplier = 1f;

        if (livingEntity != null)
            livingEntity.IsInvincible = false;
    }

    private void SpawnCircleShockwave(Vector2 center, float radius, Color color)
    {
        GameObject shockwave = new GameObject("Shockwave_Wand3");
        shockwave.transform.position = center;
        
        SpriteRenderer sr = shockwave.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite();
        sr.color = color;
        sr.sortingLayerName = "Weapons";
        sr.sortingOrder = 30;

        Material mat = new Material(Shader.Find("Custom/VFXLit2D"));
        mat.SetFloat("_EmissionIntensity", 5f);
        mat.SetColor("_EmissionColor", color);
        mat.SetFloat("_LightInfluence", 0.3f);
        sr.material = mat;

        // Shockwave animation coroutine
        ChargeSkillRunner runner = shockwave.AddComponent<ChargeSkillRunner>();
        runner.StartCoroutine(AnimateShockwave(sr, radius));
    }

    private IEnumerator AnimateShockwave(SpriteRenderer sr, float targetRadius)
    {
        float duration = 0.4f;
        float elapsed = 0f;
        Transform t = sr.transform;

        Color startColor = sr.color;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);

            // ease out (빠르게 커지다가 느려짐)
            float ease = 1f - Mathf.Pow(1f - progress, 3f);
            
            // targetRadius는 월드 유닛이므로, 스케일은 targetRadius * 2
            float scale = Mathf.Lerp(0.5f, targetRadius * 2.5f, ease); // 살짝 더 크게
            t.localScale = new Vector3(scale, scale, 1f);

            // 알파 페이드
            Color c = startColor;
            c.a = Mathf.Lerp(1f, 0f, Mathf.Pow(progress, 2f));
            sr.color = c;

            yield return null;
        }

        Object.Destroy(sr.gameObject);
    }

    private static Sprite _cachedCircle;
    private static Sprite CreateCircleSprite()
    {
        if (_cachedCircle != null) return _cachedCircle;

        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float radius = size / 2f;
        Color transparent = new Color(1f, 1f, 1f, 0f);
        Color solid = Color.white;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(radius, radius));
                if (dist <= radius)
                {
                    float alpha = Mathf.Clamp01((radius - dist) / 2f); // 2 픽셀 안티앨리어싱
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
                else
                {
                    tex.SetPixel(x, y, transparent);
                }
            }
        }
        tex.Apply();
        // 128픽셀을 1단위로 설정
        _cachedCircle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size / 2f);
        return _cachedCircle;
    }

    private void ApplySlowToNearbyEnemies(Vector3 center, float radius, float duration)
    {
        int count = Physics2D.OverlapCircleNonAlloc((Vector2)center, radius, _slowResults);

        for (int i = 0; i < count; i++)
        {
            Collider2D col = _slowResults[i];
            if (!col.CompareTag("Entity")) continue;

            GameObject root = col.GetComponentInParent<MonsterRuntimeData>()?.gameObject
                           ?? col.transform.root.gameObject;

            EnemySlowEffect slow = root.GetComponent<EnemySlowEffect>();
            if (slow == null) slow = root.AddComponent<EnemySlowEffect>();
            slow.Apply(SLOW_MULTIPLIER, duration);
        }
    }
}
