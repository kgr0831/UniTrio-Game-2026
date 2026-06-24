using UnityEngine;

/// <summary>
/// 피한 지점 "띵~" 반짝임 이펙트. <see cref="JustDodgeVFX.SpawnSparkle"/>가 생성한다.
/// - 중심 플래시: 초반에 번쩍 → 빠르게 사라짐
/// - 링: 작게 시작 → 크게 퍼지며 페이드
/// 슬로우모션(timeScale 낮음) 중에도 자연스럽게 보이도록 unscaledDeltaTime으로 동작한다.
/// </summary>
public class JustDodgeSparkleFX : MonoBehaviour
{
    private SpriteRenderer _core;
    private SpriteRenderer _ring;
    private Color _color;
    private float _scale;
    private float _duration;
    private float _t;

    public void Init(Color color, float scale, float duration)
    {
        _color    = color;
        _scale    = scale;
        _duration = Mathf.Max(0.05f, duration);

        _core = MakeChild("Core", JustDodgeVFX.RadialSprite);
        _ring = MakeChild("Ring", JustDodgeVFX.RingSprite);
    }

    private SpriteRenderer MakeChild(string childName, Sprite sprite)
    {
        var go = new GameObject(childName);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        // 기본 스프라이트 머티리얼(정점 색 사용) → sr.color로 색/알파 제어. 항상 위에 보이도록 큰 sortingOrder.
        sr.sortingOrder = 32000;
        return sr;
    }

    private void Update()
    {
        _t += Time.unscaledDeltaTime;
        float p = Mathf.Clamp01(_t / _duration);

        // 중심 플래시 (앞 35% 구간에 강하게 번쩍)
        float coreP     = Mathf.Clamp01(_t / (_duration * 0.35f));
        float coreScale = Mathf.Lerp(_scale * 0.25f, _scale * 1.1f, coreP);
        Apply(_core, coreScale, (1f - coreP) * 1.8f);

        // 퍼지는 링 (더 크게·또렷하게)
        float ringScale = Mathf.Lerp(_scale * 0.35f, _scale * 2.3f, p);
        Apply(_ring, ringScale, (1f - p) * 1.4f);

        if (p >= 1f) Destroy(gameObject);
    }

    private void Apply(SpriteRenderer sr, float scale, float alpha)
    {
        if (sr == null) return;
        sr.transform.localScale = Vector3.one * scale;
        var c = _color;
        c.a = Mathf.Clamp01(alpha) * _color.a;
        sr.color = c;
    }
}
