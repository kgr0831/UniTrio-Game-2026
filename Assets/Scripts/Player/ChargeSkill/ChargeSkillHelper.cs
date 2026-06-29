using UnityEngine;

/// <summary>
/// 검 차징 스킬의 공통 유틸리티.
/// Physics2D 기반 범위 공격, VFX 생성 등을 제공합니다.
/// </summary>
public static class ChargeSkillHelper
{
    // 물리 검출 캐싱 (GC 방지)
    private static readonly Collider2D[] _overlapResults = new Collider2D[64];

    /// <summary>
    /// 원형 범위 내의 적에게 데미지를 적용합니다.
    /// </summary>
    /// <param name="center">범위 중심 (월드 좌표)</param>
    /// <param name="radius">반경</param>
    /// <param name="damage">각 적에게 입힐 데미지</param>
    /// <param name="source">공격 주체 GameObject</param>
    /// <returns>타격한 적 수</returns>
    public static int ApplyAreaDamage(Vector2 center, float radius, float damage, GameObject source)
    {
        int count = Physics2D.OverlapCircleNonAlloc(center, radius, _overlapResults);
        int hitCount = 0;

        for (int i = 0; i < count; i++)
        {
            Collider2D col = _overlapResults[i];
            if (!col.CompareTag("Entity") && !col.CompareTag("Gatherable")) continue;

            IDamageable target = col.GetComponentInParent<IDamageable>();
            if (target == null || !target.IsAlive) continue;

            target.TakeDamage(damage, source);
            hitCount++;

            // 디버프 적용
            DebuffApplier.ApplyFromProjectile(col);

            // 히트 이벤트 알림
            HitEventManager.NotifyHit(center, col.bounds.center, hitCount <= 1);
        }

        return hitCount;
    }

    /// <summary>
    /// 반원(180도) 범위 내의 적에게 데미지를 적용합니다.
    /// </summary>
    /// <param name="center">범위 중심</param>
    /// <param name="radius">반경</param>
    /// <param name="direction">공격 방향 (정규화)</param>
    /// <param name="damage">데미지</param>
    /// <param name="source">공격 주체</param>
    /// <returns>타격한 적 수</returns>
    public static int ApplyHalfCircleDamage(Vector2 center, float radius, Vector2 direction, float damage, GameObject source)
    {
        int count = Physics2D.OverlapCircleNonAlloc(center, radius, _overlapResults);
        int hitCount = 0;

        for (int i = 0; i < count; i++)
        {
            Collider2D col = _overlapResults[i];
            if (!col.CompareTag("Entity") && !col.CompareTag("Gatherable")) continue;

            IDamageable target = col.GetComponentInParent<IDamageable>();
            if (target == null || !target.IsAlive) continue;

            // 180도 판정: 적이 공격 방향의 반원 내에 있는지
            Vector2 toTarget = ((Vector2)col.bounds.center - center).normalized;
            float dot = Vector2.Dot(direction, toTarget);
            if (dot < 0f) continue; // 반대편은 무시

            target.TakeDamage(damage, source);
            hitCount++;

            DebuffApplier.ApplyFromProjectile(col);
            HitEventManager.NotifyHit(center, col.bounds.center, hitCount <= 1);
        }

        return hitCount;
    }

    /// <summary>
    /// 원형 범위 공격 VFX (파티클 기반) 를 생성합니다.
    /// </summary>
    public static void SpawnCircleSlashVFX(Vector3 center, float radius, Color elementColor, float duration = 0.4f)
    {
        GameObject vfxObj = new GameObject("ChargeSkill_CircleSlashVFX");
        vfxObj.transform.position = center;
        Object.Destroy(vfxObj, duration + 0.5f);

        // 충격파 파티클
        ParticleSystem ps = vfxObj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.3f;
        main.loop = false;
        main.startLifetime = duration;
        main.startSpeed = 0f;
        main.startSize = radius * 2f;
        main.startColor = new Color(elementColor.r, elementColor.g, elementColor.b, 0.6f);
        main.playOnAwake = false;

        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 1) });

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.3f, 1f), new Keyframe(1f, 1.2f)));

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(elementColor, 0.3f), new GradientColorKey(elementColor, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = grad;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        Material mat = new Material(Shader.Find("Custom/VFXLit2D"));
        mat.SetFloat("_EmissionIntensity", 3f);
        mat.SetColor("_EmissionColor", elementColor);
        mat.SetFloat("_LightInfluence", 0.3f);
        renderer.material = mat;
        renderer.sortingLayerName = "Weapons";
        renderer.sortingOrder = 20;

        ps.Play();

        // 파편 파티클
        GameObject debrisObj = new GameObject("SlashDebris");
        debrisObj.transform.SetParent(vfxObj.transform);
        debrisObj.transform.localPosition = Vector3.zero;

        ParticleSystem debrisPs = debrisObj.AddComponent<ParticleSystem>();
        debrisPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var dMain = debrisPs.main;
        dMain.duration = 0.3f;
        dMain.loop = false;
        dMain.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        dMain.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
        dMain.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
        dMain.startColor = elementColor;
        dMain.playOnAwake = false;

        var dEmission = debrisPs.emission;
        dEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 20) });

        var dShape = debrisPs.shape;
        dShape.shapeType = ParticleSystemShapeType.Circle;
        dShape.radius = radius * 0.5f;

        var dRenderer = debrisPs.GetComponent<ParticleSystemRenderer>();
        dRenderer.material = mat;
        dRenderer.sortingLayerName = "Weapons";
        dRenderer.sortingOrder = 21;

        debrisPs.Play();
    }

    /// <summary>속성 색상을 ElementalWeaponSystem에서 가져옵니다.</summary>
    public static Color GetElementColor(ChargeSkillContext ctx)
    {
        if (ctx.ElementSystem != null)
        {
            Color c = ctx.ElementSystem.GetCurrentAuraColor() * 1.5f;
            c.a = 1f;
            return c;
        }
        return new Color(1f, 0.9f, 0.4f, 1f); // 기본 황금색
    }

    /// <summary>
    /// 실제 화살 프리팹의 스프라이트를 복사하여 관통 화살(PiercingArrowProjectile)을 생성합니다.
    /// </summary>
    public static void SpawnPiercingArrow(ChargeSkillContext ctx, float speed, float damage, float sizeMult, Color elementColor)
    {
        GameObject arrowObj = new GameObject("PiercingArrow_Charge");
        arrowObj.transform.position = ctx.PlayerTransform.position;

        float angle = Mathf.Atan2(ctx.CursorDirection.y, ctx.CursorDirection.x) * Mathf.Rad2Deg;
        arrowObj.transform.rotation = Quaternion.Euler(0, 0, angle);

        SpriteRenderer sr = arrowObj.AddComponent<SpriteRenderer>();

        // 타격 피드백(이펙트/데미지 텍스트)을 기본 화살에서 가져와 동일하게 재현합니다.
        GameObject[] hitVfxPrefabs    = null;
        GameObject   damageTextPrefab = null;

        if (ctx.WeaponBehaviour != null && ctx.WeaponBehaviour.WeaponType == WeaponType.Bow)
        {
            // 리플렉션 없이 강제 캐스팅이 위험하면 PlayerWeaponController 등에서 가져올 수도 있으나,
            // 일단 구조상 BowBehaviour.cs에 추가된 프로퍼티를 이용하기 위해 GetComponent 사용
            var bow = ctx.WeaponBehaviour.GetComponent<BowBehaviour>();
            if (bow != null)
            {
                GameObject prefab = bow.PiercingArrowPrefab != null ? bow.PiercingArrowPrefab : bow.NormalArrowPrefab;
                if (prefab != null)
                {
                    SpriteRenderer prefabSr = prefab.GetComponentInChildren<SpriteRenderer>();
                    if (prefabSr != null)
                    {
                        sr.sprite = prefabSr.sprite;
                        sr.sharedMaterial = prefabSr.sharedMaterial;
                    }
                }

                // 타격 이펙트/데미지 텍스트는 기본 화살 프리팹(ArrowProjectile)에서 가져옵니다.
                GameObject vfxSource = bow.NormalArrowPrefab != null ? bow.NormalArrowPrefab : prefab;
                if (vfxSource != null)
                {
                    ArrowProjectile ap = vfxSource.GetComponentInChildren<ArrowProjectile>();
                    if (ap != null)
                    {
                        hitVfxPrefabs    = ap.HitVfxPrefabs;
                        damageTextPrefab = ap.DamageTextPrefab;
                    }
                }
            }
        }

        // 폴백
        if (sr.sprite == null)
        {
            sr.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0,0,1,1), new Vector2(0.5f, 0.5f));
            sr.color = Color.white;
        }

        sr.sortingLayerName = "Weapons";
        sr.sortingOrder = 15;

        // 화살은 가늘고 기니까 bounds에 맞춰 콜라이더 설정
        BoxCollider2D col = arrowObj.AddComponent<BoxCollider2D>();
        if (sr.sprite != null)
        {
            col.size = sr.sprite.bounds.size;
        }
        else
        {
            col.size = new Vector2(0.6f, 0.15f);
        }
        col.isTrigger = true;

        Rigidbody2D rb = arrowObj.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        PiercingArrowProjectile piercing = arrowObj.AddComponent<PiercingArrowProjectile>();
        piercing.SetStats(speed, damage);
        piercing.SetScale(sizeMult);
        piercing.SetElementColor(elementColor);
        piercing.SetHitFeedback(hitVfxPrefabs, damageTextPrefab);
    }
}
