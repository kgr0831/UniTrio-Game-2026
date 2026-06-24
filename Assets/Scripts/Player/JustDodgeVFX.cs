using UnityEngine;

/// <summary>
/// 회피 저스트 절차적 VFX 유틸리티. 외부 아트 의존 없이 코드로 스프라이트를 생성한다.
/// - <see cref="SpawnSparkle"/>: 피한 지점에 "띵~" 반짝임(중심 플래시 + 퍼지는 링).
/// - <see cref="RadialSprite"/>/<see cref="RingSprite"/>: 글로우 후광 등에 재사용하는 절차적 스프라이트.
/// </summary>
public static class JustDodgeVFX
{
    private static Sprite _radial;
    private static Sprite _ring;
    private static Sprite _arc;

    /// <summary>중심 1 → 가장자리 0 의 부드러운 방사형 스프라이트.</summary>
    public static Sprite RadialSprite { get { EnsureSprites(); return _radial; } }

    /// <summary>가운데가 비고 테두리만 밝은 링 스프라이트.</summary>
    public static Sprite RingSprite { get { EnsureSprites(); return _ring; } }

    /// <summary>초승달 형태의 베기(슬래시) 호 스프라이트. +X 방향으로 열린 형태.</summary>
    public static Sprite ArcSprite { get { EnsureSprites(); return _arc; } }

    /// <summary>피한 지점에 반짝임 이펙트를 생성한다. (unscaled 시간으로 자가 재생·소멸)</summary>
    public static void SpawnSparkle(Vector3 pos, Color color, float scale = 1.5f, float duration = 0.4f)
    {
        EnsureSprites();
        var go = new GameObject("JustDodgeSparkle");
        go.transform.position = pos;
        var fx = go.AddComponent<JustDodgeSparkleFX>();
        fx.Init(color, scale, duration);
    }

    /// <summary>
    /// 카운터 명중 임팩트(슬래시 호 + 폭발 버스트)를 적 위치에 생성한다.
    /// </summary>
    /// <param name="pos">생성 위치(적).</param>
    /// <param name="dirAngleDeg">슬래시가 향할 방향(도). 보통 플레이어→적 방향.</param>
    public static void SpawnImpact(Vector3 pos, float dirAngleDeg, Color color, float scale = 3f)
    {
        EnsureSprites();
        var go = new GameObject("JustDodgeImpact");
        go.transform.position = pos;
        var fx = go.AddComponent<JustDodgeAttackFX>();
        fx.Init(color, scale, dirAngleDeg);
    }

    private static void EnsureSprites()
    {
        if (_radial == null) _radial = MakeRadial(128);
        if (_ring   == null) _ring   = MakeRing(128, 0.78f);
        if (_arc    == null) _arc    = MakeArc(128, 0.5f, 70f);
    }

    private static Sprite MakeRadial(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };

        var px = new Color[size * size];
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f - r) / r;
            float dy = (y + 0.5f - r) / r;
            float d = Mathf.Sqrt(dx * dx + dy * dy);   // 0(중심)~1(가장자리)
            float a = Mathf.Clamp01(1f - d);
            a = a * a;                                  // 부드러운 falloff
            px[y * size + x] = new Color(1f, 1f, 1f, a);
        }
        tex.SetPixels(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static Sprite MakeRing(int size, float innerRatio)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };

        var px = new Color[size * size];
        float r = size * 0.5f;
        float mid   = (innerRatio + 1f) * 0.5f;
        float halfW = (1f - innerRatio) * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f - r) / r;
            float dy = (y + 0.5f - r) / r;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float a = (d > 1f) ? 0f : Mathf.Clamp01(1f - Mathf.Abs(d - mid) / halfW);
            px[y * size + x] = new Color(1f, 1f, 1f, a);
        }
        tex.SetPixels(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    // 초승달(슬래시 호): +X 방향(angle 0)을 중심으로 ±arcHalfDeg 범위의 링 조각.
    private static Sprite MakeArc(int size, float innerRatio, float arcHalfDeg)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };

        var px = new Color[size * size];
        float r = size * 0.5f;
        float mid   = (innerRatio + 1f) * 0.5f;
        float halfW = (1f - innerRatio) * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f - r) / r;
            float dy = (y + 0.5f - r) / r;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float a = 0f;
            if (d <= 1f && d >= 0.01f)
            {
                float band = Mathf.Clamp01(1f - Mathf.Abs(d - mid) / halfW);     // 링 두께
                float ang  = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;                // +X = 0도
                float angT = Mathf.Clamp01(1f - Mathf.Abs(ang) / arcHalfDeg);    // 호 각도 내에서만
                a = band * angT * angT;
            }
            px[y * size + x] = new Color(1f, 1f, 1f, a);
        }
        tex.SetPixels(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
