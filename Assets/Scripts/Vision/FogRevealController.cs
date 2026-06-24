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
    [SerializeField] private bool _enableTrailReveal = true;
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
        FindComponents();
        ApplySettings();
    }

#if UNITY_EDITOR
    // 에디터에서 인스펙터 값을 바꿀 때 즉시 미리보기
    private void OnValidate()
    {
        FindComponents();
        ApplySettings();
    }
#endif

    private void FindComponents()
    {
        if (_spriteRenderer == null) _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (_trailRenderer  == null) _trailRenderer  = GetComponentInChildren<TrailRenderer>();
    }

    private void ApplySettings()
    {
        // ── 레이어 강제 (Vision Layer = 6) ────────────────────────────────
        // 가끔 부모만 바뀔 수 있으므로 자식들도 함께 바꿉니다.
        gameObject.layer = 6;
        foreach (Transform child in transform) { child.gameObject.layer = 6; }

        // ── 원형 시야 스케일 적용 ────────────────────────────────────────
        if (_visionRadius > 0f)
        {
            float s = _visionRadius / SPRITE_BASE_RADIUS;
            transform.localScale = new Vector3(s, s, 1f);
        }

        // ── 원형 SpriteRenderer 켜고 끄기 ───────────────────────────────
        if (_spriteRenderer != null)
        {
            _spriteRenderer.enabled = _enableCircleReveal;
            // 미니맵 전용 시야 오브젝트는 하얀색이어야 안개를 뚫습니다 (RT 캡처용)
            _spriteRenderer.color = Color.white;
        }

        // ── Trail 켜고 끄기 및 파라미터 강제 ──────────────────────────────
        if (_trailRenderer != null)
        {
            _trailRenderer.enabled = _enableTrailReveal;
            if (_enableTrailReveal)
            {
                _trailRenderer.startWidth = 25f; 
                _trailRenderer.endWidth   = 25f;
                _trailRenderer.time       = 10000f; // 영구 기록
                _trailRenderer.minVertexDistance = 0.5f; 
                
                // 트레일도 하얀색이어야 안개를 뚫습니다
                _trailRenderer.startColor = Color.white;
                _trailRenderer.endColor   = Color.white;

                // 머티리얼이 없는 경우 기본 스프라이트 머티리얼이라도 할당 (안그럼 분홍색)
                if (_trailRenderer.sharedMaterial == null)
                {
                    _trailRenderer.material = new Material(Shader.Find("Sprites/Default"));
                }
            }
        }
    }
}
