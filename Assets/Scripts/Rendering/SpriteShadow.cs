using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 발밑에 깔리는 단순한 '블롭(blob)' 그림자.
/// 모든 스프라이트가 같은 평면의 빌보드라 실제 섀도 맵핑이 어색하므로,
/// 광원 방향과 무관하게 항상 일정한 반투명 원형(타원) 그림자를 발밑에 그립니다.
///
///  - 광원에 영향받지 않음: 파이프라인 기본 스프라이트 머티리얼(언릿)을 써서
///    낮·밤·화톳불 위치와 상관없이 모양과 진하기가 일정합니다.
///  - 낮/밤: DayNightManager.NightFactor에 맞춰 해질녘부터 서서히 옅어지고
///    밤에는 완전히 사라집니다. (매니저가 없으면 항상 표시)
///  - 크기: 스프라이트의 '보이는 불투명 폭'(Physics Shape 기반)에 맞춰 자동 조절,
///    발 위치도 중앙 50% 폭의 바닥 픽셀을 기준으로 잡아 지팡이 같은 돌출부 영향을 줄입니다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteShadow : MonoBehaviour
{
    [Header("모양")]
    [Tooltip("스프라이트의 보이는 폭 대비 그림자 가로 크기 배수")]
    [SerializeField, Range(0.1f, 2f)] private float _widthScale = 1.0f;
    [Tooltip("그림자 세로/가로 비율 (1=완전한 원, 작을수록 납작한 타원)")]
    [SerializeField, Range(0.1f, 1f)] private float _squash = 0.9f;
    [Tooltip("그림자 최대 진하기(알파)")]
    [SerializeField, Range(0f, 1f)] private float _alpha = 0.9f;
    [Tooltip("발 기준선에서의 세로 오프셋(월드 유닛). 음수면 발 아래로 내림")]
    [SerializeField] private float _yOffset = 0f;
    [Tooltip("그림자를 발에 붙이는 정도(세로 반지름 비율). 클수록 위로 올라가 발에 더 붙음")]
    [SerializeField, Range(-1f, 1f)] private float _footHug = 0.3f;
    [Tooltip("Idle이 아닌 동작(공격·이동 등) 중 그림자 확대 배수. 진입 시 한번에 커지고 Idle 복귀 시 한번에 복귀")]
    [SerializeField, Range(1f, 2f)] private float _activeScale = 1.2f;

    // ── 공유 원형 텍스처/스프라이트 (모든 인스턴스가 1개를 공유) ──
    private static Sprite _circleSprite;

    // ── 스프라이트별 메트릭(발 위치·중심·폭) 캐시 ─────────────────
    private struct SpriteMetrics
    {
        public float feetY;   // 보이는 발바닥 로컬 Y
        public float centerX; // 불투명 영역의 가로 중심
        public float width;   // 불투명 영역의 가로 폭
    }
    private static readonly Dictionary<Sprite, SpriteMetrics> MetricsCache = new Dictionary<Sprite, SpriteMetrics>();

    private SpriteRenderer _source;
    private SpriteRenderer _shadow;
    private Transform      _shadowTr;
    private Animator       _animator;

    // 발 위치·기본폭을 첫 프레임에 캡처해 고정 (Idle 중 흔들리지 않음)
    private float _anchorFeetY;
    private float _baseWidth;      // Idle 기준 폭
    private float _displayWidth;   // 실제 적용 폭 (비-Idle 동안 커진 최대치 유지)
    private int   _idleStateHash;  // 첫 프레임(=Idle 가정) 애니 상태 해시
    private bool  _initialized;

    private void Awake()
    {
        _source   = GetComponent<SpriteRenderer>();
        _animator = GetComponentInParent<Animator>();
    }

    // 현재 애니 상태 해시 (전환 중이면 들어갈 다음 상태 기준 → 반응 즉시)
    private int CurrentStateHash()
    {
        if (_animator == null || _animator.runtimeAnimatorController == null) return 0;
        if (_animator.IsInTransition(0)) return _animator.GetNextAnimatorStateInfo(0).fullPathHash;
        return _animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
    }

    private void EnsureShadow()
    {
        if (_shadow != null) return;

        var go = new GameObject("SpriteShadow");
        _shadowTr = go.transform;
        _shadowTr.SetParent(transform, false);

        _shadow = go.AddComponent<SpriteRenderer>();
        _shadow.sprite = GetCircleSprite();
        // 머티리얼은 파이프라인 기본 스프라이트 머티리얼(언릿) 사용
        // → 그림자가 라이팅의 영향을 받지 않고 항상 일정한 반투명 검은 원형 유지
    }

    private void LateUpdate()
    {
        var sprite = _source.sprite;
        if (sprite == null || !_source.enabled)
        {
            SetVisible(false);
            return;
        }

        // 1) 진하기: 낮밤(NightFactor)과 본체 알파에 따라
        float daylight = 1f;
        var dn = DayNightManager.Instance;
        if (dn != null) daylight = 1f - dn.NightFactor;

        float alpha = _alpha * daylight * _source.color.a;
        if (alpha <= 0.01f)
        {
            SetVisible(false);
            return;
        }

        // 2) 발 높이·기본폭은 첫 프레임(=Idle 가정)에 고정.
        //    Idle 여부는 '폭'이 아니라 애니메이터 상태로 판정 — 폭 기준이면 한 동작 안에서도
        //    프레임마다 불투명 폭이 달라 크기가 순차적으로 변함.
        //    크기는 2단계 스냅: Idle=기본 폭 / 비-Idle=기본×_activeScale (진입·복귀 모두 한번에).
        var m = GetMetrics(sprite);
        if (!_initialized)
        {
            _anchorFeetY   = m.feetY;
            _baseWidth     = m.width;
            _displayWidth  = m.width;
            _idleStateHash = CurrentStateHash();
            _initialized   = true;
        }

        bool isIdle = _animator == null || CurrentStateHash() == _idleStateHash;
        _displayWidth = isIdle ? _baseWidth : _baseWidth * _activeScale;

        // 3) 렌더 갱신
        EnsureShadow();
        SetVisible(true);

        _shadow.color = new Color(0f, 0f, 0f, alpha);

        // 본체 바로 뒤에 그려지도록 정렬 동기화
        _shadow.sortingLayerID = _source.sortingLayerID;
        _shadow.sortingOrder   = _source.sortingOrder - 1;

        // 원형 스프라이트는 지름 1유닛(중심 피벗)이므로 스케일 = 월드 크기.
        // 가로는 플레이어 transform 중심(x=0)에, 세로는 발 높이에서 살짝 올려 발에 붙임.
        float w  = _displayWidth * _widthScale;
        float vr = w * _squash * 0.5f; // 세로 반지름
        _shadowTr.localPosition = new Vector3(0f, _anchorFeetY + _yOffset + vr * _footHug, 0f);
        _shadowTr.localRotation = Quaternion.identity;
        _shadowTr.localScale    = new Vector3(w, w * _squash, 1f);
    }

    private void SetVisible(bool visible)
    {
        if (_shadow != null && _shadow.enabled != visible)
            _shadow.enabled = visible;
    }

    // ── 부드러운 원형 그림자 스프라이트 (런타임 1회 생성, 전 인스턴스 공유) ──
    private static Sprite GetCircleSprite()
    {
        if (_circleSprite != null) return _circleSprite;

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode   = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name       = "BlobShadowTex"
        };

        float r = size * 0.5f;
        var cols = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - r) / r;
                float dy = (y + 0.5f - r) / r;
                float d  = Mathf.Sqrt(dx * dx + dy * dy); // 중심 0 ~ 가장자리 1
                // 중심~0.8 반경은 꽉 찬 1.0, 0.8→1.0 구간만 부드럽게 0으로 페이드.
                // 주의: Mathf.SmoothStep(a,b,t)는 a→b 보간 함수(GLSL smoothstep 아님).
                // → 거리 d를 먼저 InverseLerp로 0~1 정규화한 뒤 스무딩해야 함.
                float t  = Mathf.InverseLerp(0.8f, 1f, d);   // 코어 안=0, 가장자리=1
                float a  = 1f - Mathf.SmoothStep(0f, 1f, t);  // 코어 솔리드, 림만 페이드
                byte ab  = (byte)(Mathf.Clamp01(a) * 255f);
                cols[y * size + x] = new Color32(255, 255, 255, ab);
            }
        }
        tex.SetPixels32(cols);
        tex.Apply(false, false);

        // pixelsPerUnit = size → 스프라이트 월드 지름 = 1유닛, 피벗 중앙
        _circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size),
                                      new Vector2(0.5f, 0.5f), size);
        _circleSprite.name = "BlobShadowCircle";
        return _circleSprite;
    }

    // ── 스프라이트의 보이는 발바닥·중심·폭 (투명 여백 제외, 캐시) ──
    private static SpriteMetrics GetMetrics(Sprite sprite)
    {
        SpriteMetrics m;
        if (MetricsCache.TryGetValue(sprite, out m))
            return m;

        var b = sprite.bounds;
        m.feetY   = b.min.y;     // 기본값: 렉트 바닥
        m.centerX = b.center.x;
        m.width   = b.size.x;

        // 발 위치는 '중앙 영역(몸통)'의 최저점을 사용 — 지팡이·무기처럼 옆으로
        // 뻗은 부분이 전체 최저점이면 몸통 아래에 틈이 생기므로 제외.
        // 폭/중심은 불투명 영역 전체 X 범위를 사용해 스프라이트 크기에 맞춥니다.
        float bandHalfWidth = b.size.x * 0.25f; // 중앙 50% 폭

        int shapeCount = sprite.GetPhysicsShapeCount();
        if (shapeCount > 0)
        {
            float minYCenter = float.MaxValue;
            float minYAll    = float.MaxValue;
            float minX       = float.MaxValue;
            float maxX       = float.MinValue;

            var points = new List<Vector2>(16);
            for (int i = 0; i < shapeCount; i++)
            {
                sprite.GetPhysicsShape(i, points);
                for (int p = 0; p < points.Count; p++)
                {
                    float px = points[p].x;
                    float py = points[p].y;
                    if (px < minX) minX = px;
                    if (px > maxX) maxX = px;
                    if (py < minYAll) minYAll = py;
                    if (Mathf.Abs(px - b.center.x) <= bandHalfWidth && py < minYCenter)
                        minYCenter = py;
                }
            }

            if (maxX > minX)
            {
                m.width   = maxX - minX;
                m.centerX = (minX + maxX) * 0.5f;
            }
            float minY = minYCenter < float.MaxValue ? minYCenter : minYAll;
            if (minY < float.MaxValue)
                m.feetY = minY;
        }

        MetricsCache[sprite] = m;
        return m;
    }
}
