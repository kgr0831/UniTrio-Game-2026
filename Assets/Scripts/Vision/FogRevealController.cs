using UnityEngine;

/// <summary>
/// 플레이어 자식에 배치된 VisionObject(layer 6)를 제어합니다.
///
/// 동작 방식:
/// - VisionCamera(ortho size 500)가 layer 6 오브젝트를 FogMap_RT에 캡처.
/// - FogOfWar 스프라이트(FogShader)가 FogMap_RT를 샘플링해 미니맵에 안개를 표시.
/// - FogOfWar는 layer 7(Vision_Trail)이므로 MainCamera(mask=311)에는 안 보이고
///   MinimapCamera(mask=183)에만 표시됩니다.
/// </summary>
public class FogRevealController : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════════════
    // [원형 시야 범위]
    // 기존값: 없음 (SpriteRenderer localScale=1,1,1 → 지름 2.56 유닛 고정)
    // ══════════════════════════════════════════════════════════════════════
    [Header("Vision Circle")]

    [Tooltip("플레이어 중심 시야 반경(월드 유닛). 값을 높일수록 미니맵에서 안개가 더 넓게 걷힙니다.\n" +
             "기본 스프라이트(VisionHole.png) 지름 = 2.56 유닛, 이 값이 1.28이면 원본 크기.")]
    [SerializeField] private float _visionRadius = 15f;
    // ── 되돌리기: _visionRadius = 1.28f (원본 스프라이트 반경) ──

    [Tooltip("원형 시야 활성 여부.\n" +
             "false 시 SpriteRenderer를 비활성화해 현재 위치 안개 제거를 끕니다.")]
    [SerializeField] private bool _enableCircleReveal = true;
    // ── 되돌리기: _enableCircleReveal = true ── (기본값 동일)

    // ══════════════════════════════════════════════════════════════════════
    // [이동 궤적(탐험 기록) Trail]
    // 기존값: TrailRenderer enabled=true, width=30, time=10000
    //         → 플레이어가 지나온 경로를 영구적으로 안개 제거
    // ══════════════════════════════════════════════════════════════════════
    [Header("Trail — Explored Path Record")]

    [Tooltip("이동 경로 Trail 활성 여부.\n" +
             "true : 지나간 경로가 미니맵에 영구 표시 (기존 동작).\n" +
             "false: 현재 위치 원형만 안개 제거 (기본값).")]
    [SerializeField] private bool _enableTrailReveal = false;
    // ── 되돌리기: _enableTrailReveal = true ── (기존 동작 복원)

    // ══════════════════════════════════════════════════════════════════════
    // Private
    // ══════════════════════════════════════════════════════════════════════

    // VisionHole.png 는 256px / 100 PPU = 2.56 유닛 지름.
    // localScale(1,1,1)일 때 반경 = 1.28 유닛.
    private const float SPRITE_BASE_RADIUS = 1.28f;

    private SpriteRenderer _spriteRenderer;
    private TrailRenderer  _trailRenderer;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _trailRenderer  = GetComponent<TrailRenderer>();
        ApplySettings();
    }

#if UNITY_EDITOR
    // 에디터에서 인스펙터 값을 바꿀 때 즉시 미리보기
    private void OnValidate()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _trailRenderer  = GetComponent<TrailRenderer>();
        ApplySettings();
    }
#endif

    private void ApplySettings()
    {
        // ── 원형 시야 스케일 적용 ────────────────────────────────────────
        // 반경 → 스프라이트 배율: visionRadius / SPRITE_BASE_RADIUS
        // 기존 복원: transform.localScale = Vector3.one (반경 1.28)
        if (_visionRadius > 0f)
        {
            float s = _visionRadius / SPRITE_BASE_RADIUS;
            transform.localScale = new Vector3(s, s, 1f);
        }

        // ── 원형 SpriteRenderer 켜고 끄기 ───────────────────────────────
        if (_spriteRenderer != null)
            _spriteRenderer.enabled = _enableCircleReveal;

        // ── Trail 켜고 끄기 ──────────────────────────────────────────────
        // 기존 Trail: width=30, time=10000s — 대단히 넓고 긴 궤적 기록.
        // false로 두면 현재 위치 원형만 안개가 걷힙니다.
        if (_trailRenderer != null)
            _trailRenderer.enabled = _enableTrailReveal;
    }
}
