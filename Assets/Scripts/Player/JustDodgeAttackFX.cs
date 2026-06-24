using UnityEngine;

/// <summary>
/// 회피 저스트 카운터 명중 임팩트. <see cref="JustDodgeVFX.SpawnImpact"/>가 생성한다.
/// - 슬래시 호: 공격 방향으로 크게 베는 초승달, 살짝 휘두르며 빠르게 사라짐.
/// - 임팩트 링 + 중심 섬광: 적 위치에서 터지듯 퍼짐.
/// timeScale 영향 없이 보이도록 unscaledDeltaTime으로 동작한다.
/// </summary>
public class JustDodgeAttackFX : MonoBehaviour
{
    private SpriteRenderer _slash;
    private SpriteRenderer _ring;
    private SpriteRenderer _core;

    private Color _color;
    private float _scale;
    private float _dirAngle;   // 슬래시가 향할 방향(도)
    private float _t;

    private const float SlashDur  = 0.22f;
    private const float ImpactDur = 0.34f;
    private const float CoreDur   = 0.16f;

    public void Init(Color color, float scale, float dirAngleDeg)
    {
        _color    = color;
        _scale    = Mathf.Max(0.1f, scale);
        _dirAngle = dirAngleDeg;

        _slash = MakeChild("Slash", JustDodgeVFX.ArcSprite);
        _ring  = MakeChild("Ring",  JustDodgeVFX.RingSprite);
        _core  = MakeChild("Core",  JustDodgeVFX.RadialSprite);

        // 슬래시는 공격 방향을 향하도록 회전
        _slash.transform.localRotation = Quaternion.Euler(0f, 0f, _dirAngle);
    }

    private SpriteRenderer MakeChild(string childName, Sprite sprite)
    {
        var go = new GameObject(childName);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 31000; // 적/플레이어 위
        return sr;
    }

    private void Update()
    {
        _t += Time.unscaledDeltaTime;

        // 슬래시 호: 크게 나타나 살짝 더 휘둘리며(회전) 페이드
        float sp = Mathf.Clamp01(_t / SlashDur);
        float slashScale = Mathf.Lerp(_scale * 0.9f, _scale * 1.35f, sp);
        _slash.transform.localScale = Vector3.one * slashScale;
        _slash.transform.localRotation = Quaternion.Euler(0f, 0f, _dirAngle + Mathf.Lerp(-18f, 18f, sp)); // 휘두름
        Apply(_slash, (1f - sp) * 1.6f);

        // 임팩트 링: 작게 → 크게 퍼지며 페이드
        float ip = Mathf.Clamp01(_t / ImpactDur);
        _ring.transform.localScale = Vector3.one * Mathf.Lerp(_scale * 0.3f, _scale * 1.7f, ip);
        Apply(_ring, (1f - ip) * 1.3f);

        // 중심 섬광: 짧고 강하게
        float cp = Mathf.Clamp01(_t / CoreDur);
        _core.transform.localScale = Vector3.one * Mathf.Lerp(_scale * 0.5f, _scale * 0.9f, cp);
        Apply(_core, (1f - cp) * 1.8f);

        if (_t >= Mathf.Max(SlashDur, Mathf.Max(ImpactDur, CoreDur)))
            Destroy(gameObject);
    }

    private void Apply(SpriteRenderer sr, float alpha)
    {
        if (sr == null) return;
        var c = _color;
        c.a = Mathf.Clamp01(alpha) * _color.a;
        sr.color = c;
    }
}
