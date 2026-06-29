using UnityEngine;

/// <summary>
/// 채집물(나무/돌) 위에 표시하는 붉은 실루엣 하이라이트.
/// 원본 스프라이트 뒤에 빨간 확대 복사본을 배치해 테두리 링처럼 보이게 합니다.
/// </summary>
public class TutorialNodeOutline : MonoBehaviour
{
    private SpriteRenderer _src;
    private SpriteRenderer _bgSr;
    private Material       _matInstance; // 인스턴스별 머티리얼 (_Color 프로퍼티 독립 제어)

    /// <summary>
    /// src SpriteRenderer에 붉은 아웃라인 오버레이를 생성합니다.
    /// </summary>
    /// <param name="localScale">
    /// 0 이하면 월드 크기 기반으로 자동 계산합니다.
    /// (기본: 나무≈1.06, 돌≈1.5)
    /// </param>
    public static TutorialNodeOutline Create(SpriteRenderer src, float localScale = 0f)
    {
        if (src == null) return null;
        var go = new GameObject("TutorialOutline");
        var ol = go.AddComponent<TutorialNodeOutline>();
        ol.Init(src, localScale);
        return ol;
    }

    private void Init(SpriteRenderer src, float scaleOverride)
    {
        _src = src;

        transform.SetParent(src.transform, false);
        transform.localPosition = Vector3.zero;

        // ── 어댑티브 스케일 ──────────────────────────────────────────
        // 고정 localScale은 나무(World 8) 기준으로 너무 두껍고
        // 돌(World 0.4) 기준으로 너무 얇습니다.
        // 목표 테두리 두께를 0.1~0.3 world units로 고정하고 역산합니다.
        if (scaleOverride > 0f)
        {
            transform.localScale = Vector3.one * scaleOverride;
        }
        else
        {
            // 절대 두께(world units)로 강제하면 돌(0.4)처럼 작은 오브젝트에서 
            // 아웃라인이 원본의 1.5배 이상 폭주하는 심각한 문제가 발생합니다.
            // 따라서 크기와 무관하게 항상 원본 대비 일정한 비율(약 1.08배)로 커지도록 수정합니다.
            transform.localScale = Vector3.one * 1.08f;
        }

        // ── SpriteRenderer 설정 ──────────────────────────────────────
        _bgSr = gameObject.AddComponent<SpriteRenderer>();
        _bgSr.sprite         = src.sprite;
        _bgSr.flipX          = src.flipX;
        _bgSr.flipY          = src.flipY;
        _bgSr.sortingLayerID = src.sortingLayerID;
        _bgSr.sortingOrder   = src.sortingOrder - 1; // 원본 바로 뒤

        // ── 머티리얼: 인스턴스별 생성 ────────────────────────────────
        // static 공유 머티리얼은 Enter Play Mode 간 캐시가 남아 색상이 틀어집니다.
        // 인스턴스 머티리얼로 _Color를 독립 제어합니다.
        var shader = Shader.Find("Custom/SpriteSilhouette");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        _matInstance = new Material(shader);
        _matInstance.color = new Color(1f, 0.08f, 0.08f, 0f); // 시작: 완전 투명
        _bgSr.material     = _matInstance;
        _bgSr.color        = Color.white; // 버텍스 컬러는 중립으로 유지
    }

    /// <summary>아웃라인 투명도를 0~1로 설정합니다 (페이드 인/아웃용).</summary>
    public void SetAlpha(float alpha)
    {
        if (_matInstance == null) return;
        Color c = _matInstance.color;
        c.a = alpha;
        _matInstance.color = c;
    }

    private void LateUpdate()
    {
        if (_src == null) { Destroy(gameObject); return; }
        // 애니메이션 프레임(스프라이트 교체)·플립 동기화
        if (_bgSr.sprite != _src.sprite) _bgSr.sprite = _src.sprite;
        _bgSr.flipX = _src.flipX;
        _bgSr.flipY = _src.flipY;
    }

    private void OnDestroy()
    {
        // 인스턴스 머티리얼은 직접 해제해야 메모리 누수 방지
        if (_matInstance != null) Destroy(_matInstance);
    }
}
