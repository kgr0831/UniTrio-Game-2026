using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 지팡이 차징 2단계: 전이 마법구
/// - 발사체 적중 시 근처 적에게 1회당 3개씩 총 3회 전이
/// - 같은 적 재전이 불가
/// - 전이 탐색 반경: 3칸
/// </summary>
public class WandChargeSkill2 : IChargeSkill
{
    private const float PROJECTILE_SPEED  = 12f;
    private const int   CHAIN_COUNT       = 3;  // 총 전이 횟수
    private const int   CHAINS_PER_HIT    = 3;  // 1회 전이 시 투사체 수
    private const float CHAIN_RADIUS      = 3f; // 전이 탐색 반경

    public void Execute(ChargeSkillContext ctx)
    {
        float damage = ctx.BaseDamage;
        Color elementColor = ChargeSkillHelper.GetElementColor(ctx);

        SpawnChainProjectile(ctx, damage, elementColor);

        Debug.Log($"[WandCharge2] 전이 마법구 — 데미지: {damage:F0}, 전이: {CHAIN_COUNT}회 × {CHAINS_PER_HIT}개");
    }

    private void SpawnChainProjectile(ChargeSkillContext ctx, float damage, Color elementColor)
    {
        var wand = ctx.WeaponBehaviour as WandBehaviour;
        MagicProjectile template = wand?.ProjectileTemplate;

        // 비주얼 + 폭발 VFX 정보 수집
        Sprite chainSprite = null;
        Material chainMaterial = null;
        GameObject explosionPrefab = template?.ExplosionVfxPrefab;
        GameObject damageTextPrefab = template?.DamageTextPrefab;

        if (wand != null && wand.ProjectilePrefab != null)
        {
            SpriteRenderer prefabSr = wand.ProjectilePrefab.GetComponentInChildren<SpriteRenderer>();
            if (prefabSr != null)
            {
                chainSprite   = prefabSr.sprite;
                chainMaterial = new Material(prefabSr.sharedMaterial);
            }
        }

        // 프리팹 비주얼을 가져오지 못했을 경우 Fallback
        if (chainSprite == null)
            chainSprite = CreateSoftCircleSprite();
        if (chainMaterial == null)
        {
            chainMaterial = new Material(Shader.Find("Custom/VFXLit2D"));
            chainMaterial.SetFloat("_EmissionIntensity", 4f);
            chainMaterial.SetColor("_EmissionColor", elementColor);
            chainMaterial.SetFloat("_LightInfluence", 0.3f);
        }

        // 프리팹 원본 스케일 사용 (하드코딩된 1.5f 대신)
        Vector3 prefabScale = wand?.ProjectilePrefab != null
            ? wand.ProjectilePrefab.transform.localScale
            : Vector3.one;

        GameObject projObj = new GameObject("ChainProjectile_Initial");
        projObj.transform.position = ctx.PlayerTransform.position;
        projObj.transform.localScale = prefabScale;

        SpriteRenderer sr = projObj.AddComponent<SpriteRenderer>();
        sr.sprite = chainSprite;
        sr.material = chainMaterial;
        sr.sortingLayerName = "Weapons";
        sr.sortingOrder = 15;
        sr.color = elementColor;

        CircleCollider2D col = projObj.AddComponent<CircleCollider2D>();
        col.radius = 0.3f;
        col.isTrigger = true;

        Rigidbody2D rb = projObj.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        HashSet<int> globalHitList = new HashSet<int>();

        ChainProjectile chain = projObj.AddComponent<ChainProjectile>();
        chain.SetStats(PROJECTILE_SPEED, damage, ctx.CursorDirection, elementColor,
                       CHAIN_COUNT, CHAINS_PER_HIT, CHAIN_RADIUS, globalHitList);
        chain.SetVisual(chainSprite, chainMaterial, explosionPrefab, damageTextPrefab, prefabScale);
    }

    private static Sprite _cachedCircleSprite;
    private static Sprite CreateSoftCircleSprite()
    {
        if (_cachedCircleSprite != null) return _cachedCircleSprite;

        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float center = (size - 1) * 0.5f;
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - dist);
                a *= a;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        _cachedCircleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return _cachedCircleSprite;
    }
}
