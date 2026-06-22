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

        _sr = gameObject.AddComponent<SpriteRenderer>();
        _sr.color          = color;
        _sr.sortingLayerID = src.sortingLayerID;
        _sr.sortingOrder   = src.sortingOrder - 1; // 원본 뒤 → 가장자리만 보임
        Sync();
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
