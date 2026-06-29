using UnityEngine;

/// <summary>
/// 적 강조용 붉은 아웃라인. 대상 SpriteRenderer를 살짝 확대 복제해 뒤에 깔아 테두리처럼 보이게 한다.
/// (URP 호환 문제를 피하기 위해 셰이더 대신 실루엣 복제 방식 사용.)
/// 매 프레임 원본 스프라이트/플립을 따라가므로 애니메이션 중에도 테두리가 유지된다.
/// </summary>
public class JustDodgeOutline : MonoBehaviour
{
    private SpriteRenderer _src;
    private SpriteRenderer _sr;
    private Material       _matInstance;
    private static Shader  _outlineShader;
    private static int     _fxLayer = -2;

    /// <summary>대상 SpriteRenderer를 강조하는 아웃라인을 생성한다.</summary>
    public static JustDodgeOutline Create(SpriteRenderer src, Color color, float scale)
    {
        if (src == null) return null;
        var go = new GameObject("JustDodgeOutline");
        var ol = go.AddComponent<JustDodgeOutline>();
        ol.Init(src, color, scale);
        return ol;
    }

    private void Init(SpriteRenderer src, Color color, float scale)
    {
        _src = src;
        transform.SetParent(src.transform, false);
        transform.localPosition = Vector3.zero;
        transform.localScale    = Vector3.one * scale;

        // 전용 레이어(JustDodgeFX) → 오버레이 카메라가 흑백 무시하고 단독 렌더 → 빨강 유지
        if (_fxLayer == -2) _fxLayer = LayerMask.NameToLayer("JustDodgeFX");
        if (_fxLayer >= 0) gameObject.layer = _fxLayer;

        _sr = gameObject.AddComponent<SpriteRenderer>();
        _sr.sortingLayerID = src.sortingLayerID;
        _sr.sortingOrder   = src.sortingOrder + 1;

        // 외곽선 전용 셰이더: 내부 투명 + 가장자리 링만 → 그레이된 적 위에 빨간 테두리만 보인다.
        if (_outlineShader == null) _outlineShader = Shader.Find("Custom/SpriteOutlineOnly");
        if (_outlineShader != null)
        {
            _matInstance = new Material(_outlineShader);
            _matInstance.SetColor("_OutlineColor", color);
            _matInstance.SetFloat("_OutlineWidth", 3f);
            _sr.material = _matInstance;
        }

        Sync();
    }

    /// <summary>아웃라인 투명도를 0~1 범위로 설정합니다 (페이드 인/아웃용).</summary>
    public void SetAlpha(float alpha)
    {
        if (_sr == null) return;
        Color c = _sr.color;
        c.a = alpha;
        _sr.color = c;
    }

    private void OnDestroy()
    {
        if (_matInstance != null) Destroy(_matInstance);
    }

    private void LateUpdate()
    {
        if (_src == null) { Destroy(gameObject); return; }
        Sync();
    }

    private void Sync()
    {
        _sr.sprite = _src.sprite;
        _sr.flipX  = _src.flipX;
        _sr.flipY  = _src.flipY;
    }
}
