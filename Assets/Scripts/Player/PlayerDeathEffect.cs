using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 플레이어 사망 시 실행되는 소울라이크 스타일 시각 효과:
///
///   1. 무기 즉시 숨기기
///   2. 카메라 흔들림 + 슬로우 모션(timeScale=0.2)
///   3. 0.2초 후 timeScale=0 → 적·환경 완전 동결
///   4. 플레이어 스프라이트: 노이즈 기반 위→아래 소멸 (PlayerDissolveV2 셰이더)
///      경계선에서 HDR 금색 Edge Glow 발생
///   5. 금색 픽셀 파티클이 사라진 영역에서 위로 서서히 떠오름
///   6. (선택) 포스트 프로세싱 채도 낮추기 → 금색 파티클 대비 강조
/// </summary>
[DisallowMultipleComponent]
public class PlayerDeathEffect : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════════════
    // Inspector — References
    // ══════════════════════════════════════════════════════════════════════

    [Header("References")]
    [Tooltip("플레이어 주 스프라이트 렌더러. 비워두면 GetComponent로 탐색합니다.")]
    [SerializeField] private SpriteRenderer _spriteRenderer;

    [Tooltip("HealthSystem 구독용 LivingEntity. 비워두면 GetComponent로 탐색합니다.")]
    [SerializeField] private LivingEntity _livingEntity;

    [Tooltip("사망 즉시 비활성화할 무기 피봇 오브젝트 (WeaponPivot).")]
    [SerializeField] private GameObject _weaponPivot;

    // ══════════════════════════════════════════════════════════════════════
    // Inspector — Dissolve Shader
    // ══════════════════════════════════════════════════════════════════════

    [Header("Dissolve Shader (PlayerDissolveV2)")]
    [Tooltip("Custom/PlayerDissolveV2 셰이더를 사용하는 머티리얼.")]
    [SerializeField] private Material _dissolveMaterial;

    // ══════════════════════════════════════════════════════════════════════
    // Inspector — Timing & Hit Stop
    // ══════════════════════════════════════════════════════════════════════

    [Header("Timing & Hit Stop")]
    [Tooltip("사망 직후 슬로우 모션 배율. 0.15~0.25 권장.")]
    [SerializeField] private float _slowMoScale = 0.2f;

    [Tooltip("슬로우 모션 유지 시간(초, 비스케일). 이후 timeScale=0으로 전환.")]
    [SerializeField] private float _slowMoDuration = 0.2f;

    [Tooltip("timeScale=0 상태에서 용해 시작까지 추가 대기 시간(초, 비스케일).")]
    [SerializeField] private float _freezeHoldDuration = 0.15f;

    [Tooltip("용해 완료까지 걸리는 시간(초, 비스케일).")]
    [SerializeField] private float _dissolveDuration = 2.2f;

    [Tooltip("용해 진행 커브. 위에서 아래로 서서히, 균일하게 내려가도록 선형에 가깝게 설정.")]
    [SerializeField] private AnimationCurve _dissolveCurve = new AnimationCurve(
        new Keyframe(0f,   0f,   0f,   1.2f),
        new Keyframe(0.5f, 0.5f, 1.2f, 1.2f),
        new Keyframe(1f,   1f,   1.2f, 0f)
    );

    // ══════════════════════════════════════════════════════════════════════
    // Inspector — Pixel Particles
    // ══════════════════════════════════════════════════════════════════════

    [Header("Gold Pixel Particles")]
    [Tooltip("파티클 렌더러에 쓸 머티리얼.\n" +
             "Universal Render Pipeline/Particles/Unlit (Blending=Additive) 권장.")]
    [SerializeField] private Material _particleMaterial;

    [Tooltip("초당 방출할 파티클 수. 버짓 누적 방식으로 1개씩 방출되므로 분수처럼 터지지 않음.")]
    [SerializeField] private float _emitRate = 18f;

    [Tooltip("파티클 수명 범위 (초, 비스케일). 길수록 더 높이 떠오릅니다.")]
    [SerializeField] private Vector2 _particleLifetime = new Vector2(1.8f, 4.0f);

    [Tooltip("픽셀 입자 크기 범위 (월드 유닛).")]
    [SerializeField] private Vector2 _particleSize = new Vector2(0.03f, 0.08f);

    [Tooltip("금색 A. Bloom 없이도 보이는 LDR 값. (1, 0.6, 0) = 진한 황금색)")]
    [SerializeField] private Color _particleColorA = new Color(1.0f, 0.55f, 0.0f, 1f);

    [Tooltip("금색 B. A와 B 사이에서 랜덤 혼합됩니다. (1, 0.85, 0.2) = 밝은 금색")]
    [SerializeField] private Color _particleColorB = new Color(1.0f, 0.85f, 0.2f, 1f);

    [Tooltip("수평 흔들림 범위.")]
    [SerializeField] private Vector2 _particleSpeedX = new Vector2(-0.3f, 0.3f);

    [Tooltip("위로 떠오르는 속도 범위 (양수 = 위쪽). 노이즈에 묻히지 않게 충분히 크게 설정.")]
    [SerializeField] private Vector2 _particleSpeedY = new Vector2(1.2f, 2.8f);

    [Tooltip("중력 배율. 음수 = 위로 가속. -0.25 = 가루가 서서히 더 빠르게 떠오름.")]
    [SerializeField] private float _gravityModifier = -0.25f;

    [Tooltip("방출 띠 높이 (스프라이트 높이 대비 비율). 0.02 = 경계선 바로 위 2%의 매우 좁은 띠.\n" +
             "작을수록 파티클이 용해 경계선에 딱 붙어 나옴.")]
    [SerializeField] private float _emitBandRatio = 0.02f;

    // ══════════════════════════════════════════════════════════════════════
    // Inspector — Post-Processing (Optional)
    // ══════════════════════════════════════════════════════════════════════

    [Header("Post-Processing (Optional — Color Grading)")]
    [Tooltip("씬의 Global Volume. ColorAdjustments 오버라이드가 포함되어 있어야 합니다.\n" +
             "비워두면 포스트 프로세싱 효과는 건너뜁니다.")]
    [SerializeField] private Volume _postProcessVolume;

    [Tooltip("채도 감소 목표값. -100=완전 흑백, -60=부분 감소 권장.")]
    [SerializeField] private float _targetSaturation = -65f;

    [Tooltip("채도 감소 진행 시간(초, 비스케일).")]
    [SerializeField] private float _desatDuration = 1.5f;

    // ══════════════════════════════════════════════════════════════════════
    // 완료 이벤트
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>용해 효과가 완전히 끝나면 발생합니다. GameManager 등에서 구독하세요.</summary>
    public event Action OnDissolveComplete;

    // ══════════════════════════════════════════════════════════════════════
    // Private
    // ══════════════════════════════════════════════════════════════════════

    // 셰이더 프로퍼티 ID — Shader.PropertyToID는 할당 비용이 있으므로 static으로 1회 캐싱
    private static readonly int _thresholdId = Shader.PropertyToID("_Threshold");

    private MaterialPropertyBlock    _mpb;
    private ParticleSystem            _ps;
    private ParticleSystem.EmitParams _ep;          // struct — 재사용으로 GC 방지
    private float                     _emitBudget;  // 누적 방출 버짓 (1 이상 시 1개 방출)
    private bool                      _effectStarted;

    // 포스트 프로세싱 ColorAdjustments 캐싱
    private UnityEngine.Rendering.Universal.ColorAdjustments _colorAdjustments;

    // 스프라이트 불투명 픽셀 좌표 캐시 (Y순 정렬 → 빠른 범위 조회)
    // Read/Write Enabled 텍스처에서 한 번만 빌드됨
    private struct EmitPoint { public float x, y; }
    private EmitPoint[] _emitCache;
    private int         _emitCacheCount;
    private bool        _useEmitCache;

    // ══════════════════════════════════════════════════════════════════════
    // Unity Lifecycle
    // ══════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_livingEntity   == null) _livingEntity   = GetComponent<LivingEntity>();

        _mpb = new MaterialPropertyBlock();
        _ep  = new ParticleSystem.EmitParams();

        SetupParticleSystem();
        TryCacheColorAdjustments();
    }

    private void Start()
    {
        if (_livingEntity == null)
        {
            Debug.LogWarning("[PlayerDeathEffect] LivingEntity를 찾을 수 없습니다.", this);
            return;
        }
        _livingEntity.Health.OnDied += OnPlayerDied;
    }

    private void OnDestroy()
    {
        if (_livingEntity != null)
            _livingEntity.Health.OnDied -= OnPlayerDied;

        if (_effectStarted)
            Time.timeScale = 1f;
    }

    /// <summary>
    /// 부활 시 이 컴포넌트의 상태를 초기화합니다. DeathCutsceneController.OnCutsceneEnd()에서 호출하세요.
    /// 스프라이트 재활성화는 PlayerReverseDissolveController.PlayReverseDissolve()에서 처리합니다.
    /// </summary>
    public void ResetForResurrection()
    {
        _effectStarted = false;
        if (_weaponPivot != null)
            _weaponPivot.SetActive(true);
    }

    // ══════════════════════════════════════════════════════════════════════
    // 포스트 프로세싱 초기화
    // ══════════════════════════════════════════════════════════════════════

    private void TryCacheColorAdjustments()
    {
        if (_postProcessVolume == null) return;
        _postProcessVolume.profile.TryGet(out _colorAdjustments);
    }

    // ══════════════════════════════════════════════════════════════════════
    // Particle System 초기 구성
    // ══════════════════════════════════════════════════════════════════════

    private void SetupParticleSystem()
    {
        var go = new GameObject("DeathPixelParticles");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;

        _ps = go.AddComponent<ParticleSystem>();

        // ── Main ────────────────────────────────────────────────────────
        var main = _ps.main;
        main.loop            = false;
        main.playOnAwake     = false;
        main.maxParticles    = 3000;
        // 월드 공간: 플레이어 이동·삭제와 무관하게 파티클 유지
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        // ★ timeScale=0 상태에서도 파티클 진행
        main.useUnscaledTime = true;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(_particleLifetime.x, _particleLifetime.y);
        main.startSize       = new ParticleSystem.MinMaxCurve(_particleSize.x, _particleSize.y);
        main.startColor      = Color.white;
        main.gravityModifier = _gravityModifier;

        // ── 방출: 스크립트에서 수동 Emit 사용 ──────────────────────────
        var emission = _ps.emission;
        emission.enabled = false;

        // ── Noise 모듈: 흩날리듯 불규칙하게 움직임 ─────────────────────
        // strength를 낮게 유지해 기본 위쪽 속도를 노이즈가 압도하지 않도록 함
        var noise = _ps.noise;
        noise.enabled          = true;
        noise.strength         = 0.25f;  // 낮게 → 위쪽 속도 유지, 약간의 흩날림만 추가
        noise.frequency        = 0.6f;
        noise.scrollSpeed      = 0.3f;
        noise.quality          = ParticleSystemNoiseQuality.Low;
        noise.octaveCount      = 2;
        noise.octaveScale      = 2f;
        noise.octaveMultiplier = 0.5f;

        // ── Color Over Lifetime: 밝게 시작 → 서서히 투명 ─────────────────
        var col = _ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.4f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);

        // ── Size Over Lifetime: 크다가 작아지며 사라짐 ─────────────────
        var sizeOverLifetime = _ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f, AnimationCurve.Linear(0f, 1f, 1f, 0.05f));

        // ── Renderer ────────────────────────────────────────────────────
        var renderer = _ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode   = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = 10;
        if (_particleMaterial != null)
            renderer.material = _particleMaterial;

        _ps.Play();
    }

    // ══════════════════════════════════════════════════════════════════════
    // 사망 이벤트 처리
    // ══════════════════════════════════════════════════════════════════════

    private void OnPlayerDied()
    {
        if (_effectStarted) return;
        _effectStarted = true;

        // 무기 즉시 비활성화
        if (_weaponPivot != null)
            _weaponPivot.SetActive(false);

        // ── 카메라 흔들림 ─────────────────────────────────────────────
        CameraShakeController.Instance?.Shake();

        // ── 슬로우 모션 시작 → 이후 코루틴에서 완전 동결 ──────────────
        Time.timeScale = _slowMoScale;

        StartCoroutine(DissolveRoutine());
    }

    // ══════════════════════════════════════════════════════════════════════
    // 메인 시퀀스 코루틴
    // ══════════════════════════════════════════════════════════════════════

    private IEnumerator DissolveRoutine()
    {
        if (_spriteRenderer == null || _dissolveMaterial == null) yield break;

        // ── Step 1: 슬로우 모션 구간 ────────────────────────────────────
        yield return WaitUnscaled(_slowMoDuration);

        // ── Step 2: 완전 동결 (적·환경 Animator 모두 정지) ──────────────
        Time.timeScale = 0f;
        yield return WaitUnscaled(_freezeHoldDuration);

        // ── Step 3: Dissolve 머티리얼 교체 + 채도 감소 시작 ─────────────
        _spriteRenderer.material = _dissolveMaterial;
        _mpb.SetFloat(_thresholdId, 0f);
        _spriteRenderer.SetPropertyBlock(_mpb);

        Bounds bounds = _spriteRenderer.bounds;

        // 스프라이트 불투명 픽셀 캐시 빌드 (Read/Write 텍스처에서만)
        BuildEmitCache(bounds);

        // 포스트 프로세싱 채도 감소 코루틴은 독립 실행
        if (_colorAdjustments != null)
            StartCoroutine(DesatRoutine());

        // ── Step 4: Threshold 0 → 1 애니메이션 + 파티클 1개씩 방출 ───────
        float elapsed = 0f;
        _emitBudget = 0f;

        while (elapsed < _dissolveDuration)
        {
            float dt = Time.unscaledDeltaTime;
            elapsed += dt;

            float t         = Mathf.Clamp01(elapsed / _dissolveDuration);
            float threshold = _dissolveCurve.Evaluate(t);

            // 셰이더 갱신 (매 프레임 dissolve 경계선 이동)
            _mpb.SetFloat(_thresholdId, threshold);
            _spriteRenderer.SetPropertyBlock(_mpb);

            // 버짓 누적: 초당 _emitRate 만큼 쌓임
            // while 루프로 1개씩 방출 → 분수처럼 한꺼번에 터지지 않음
            if (threshold > 0.01f)
            {
                _emitBudget += dt * _emitRate;
                while (_emitBudget >= 1f)
                {
                    _emitBudget -= 1f;
                    EmitOneParticle(threshold, bounds);
                }
            }

            yield return null;
        }

        // ── Step 5: 스프라이트 숨기기 ────────────────────────────────────
        _spriteRenderer.enabled = false;

        // 파티클이 자연 소멸할 때까지 대기
        yield return WaitUnscaled(_particleLifetime.y);

        OnDissolveComplete?.Invoke();
        // ※ timeScale 복원은 구독자(GameManager 등)에서 처리하세요.
    }

    // ══════════════════════════════════════════════════════════════════════
    // 포스트 프로세싱: 채도 서서히 낮추기
    // ══════════════════════════════════════════════════════════════════════

    private IEnumerator DesatRoutine()
    {
        float startSat = _colorAdjustments.saturation.value;
        float elapsed  = 0f;

        while (elapsed < _desatDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _colorAdjustments.saturation.value =
                Mathf.Lerp(startSat, _targetSaturation, elapsed / _desatDuration);
            yield return null;
        }
        _colorAdjustments.saturation.value = _targetSaturation;
    }

    // ══════════════════════════════════════════════════════════════════════
    // 스프라이트 불투명 픽셀 캐시 빌드
    // ══════════════════════════════════════════════════════════════════════

    private void BuildEmitCache(Bounds worldBounds)
    {
        _useEmitCache = false;
        Sprite spr = _spriteRenderer.sprite;
        if (spr == null) return;

        Texture2D tex = spr.texture;
        if (tex == null || !tex.isReadable)
        {
            Debug.LogWarning("[PlayerDeathEffect] 스프라이트 텍스처에 Read/Write가 꺼져 있습니다.\n" +
                             "Project > 텍스처 선택 > Texture Type: Sprite > Read/Write Enabled 체크 후 Apply.\n" +
                             "그때까지 렉트 전체에서 방출합니다.", this);
            return;
        }

        Rect   pRect = spr.textureRect;
        int    pw    = Mathf.Max(1, (int)pRect.width);
        int    ph    = Mathf.Max(1, (int)pRect.height);

        // 1픽셀 간격으로 샘플링 → 최대 pw*ph개 포인트 (가장 세밀한 방출 위치)
        const int step = 1;

        // 한 번의 GetPixels 호출로 전체 픽셀 가져오기 (GetPixel 반복보다 훨씬 빠름)
        Color[] pixels = tex.GetPixels((int)pRect.x, (int)pRect.y, pw, ph);

        int maxPts = pw * ph;
        _emitCache      = new EmitPoint[maxPts];
        _emitCacheCount = 0;

        float invW = 1f / pw;
        float invH = 1f / ph;
        float wW   = worldBounds.size.x;
        float wH   = worldBounds.size.y;

        for (int py = 0; py < ph; py += step)
        {
            for (int px = 0; px < pw; px += step)
            {
                if (pixels[py * pw + px].a > 0.15f)
                {
                    // 픽셀 중심 UV → 월드 좌표
                    float nx = (px + 0.5f) * invW;
                    float ny = (py + 0.5f) * invH;
                    _emitCache[_emitCacheCount++] = new EmitPoint
                    {
                        x = worldBounds.min.x + nx * wW,
                        y = worldBounds.min.y + ny * wH
                    };
                }
            }
        }

        // Y 기준 오름차순 정렬 → 이진 탐색으로 빠른 범위 조회
        // static delegate이므로 GC 없음
        System.Array.Sort(_emitCache, 0, _emitCacheCount, EmitPointYComparer.Instance);
        _useEmitCache = (_emitCacheCount > 0);
    }

    // IComparer 구현체 — Array.Sort(array, index, count, IComparer) 오버로드에 필요
    private sealed class EmitPointYComparer : System.Collections.Generic.IComparer<EmitPoint>
    {
        public static readonly EmitPointYComparer Instance = new EmitPointYComparer();
        public int Compare(EmitPoint a, EmitPoint b) => a.y < b.y ? -1 : a.y > b.y ? 1 : 0;
    }

    // lower_bound: _emitCache 에서 y >= targetY 인 첫 인덱스
    private int LowerBound(float targetY)
    {
        int lo = 0, hi = _emitCacheCount;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (_emitCache[mid].y < targetY) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    // upper_bound: _emitCache 에서 y > targetY 인 첫 인덱스
    private int UpperBound(float targetY)
    {
        int lo = 0, hi = _emitCacheCount;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (_emitCache[mid].y <= targetY) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    // ══════════════════════════════════════════════════════════════════════
    // 파티클 1개 방출 — 버짓 루프에서 1회씩 호출됨 (분수 방지)
    // ══════════════════════════════════════════════════════════════════════

    private void EmitOneParticle(float threshold, Bounds bounds)
    {
        if (_ps == null) return;

        // 현재 용해 경계선 Y
        float edgeY  = Mathf.Lerp(bounds.max.y, bounds.min.y, threshold);
        float bandH  = bounds.size.y * _emitBandRatio;
        float emitBottom = edgeY;
        float emitTop    = Mathf.Min(edgeY + bandH, bounds.max.y);

        float z = transform.position.z - 0.05f;

        if (_useEmitCache)
        {
            int lo = LowerBound(emitBottom);
            int hi = UpperBound(emitTop);
            if (hi <= lo) return;

            EmitPoint pt = _emitCache[UnityEngine.Random.Range(lo, hi)];
            EmitOne(pt.x, pt.y, z);
        }
        else
        {
            EmitOne(
                UnityEngine.Random.Range(bounds.min.x, bounds.max.x),
                UnityEngine.Random.Range(emitBottom, emitTop),
                z);
        }
    }

    private void EmitOne(float wx, float wy, float wz)
    {
        _ep.position = new Vector3(wx, wy, wz);
        _ep.velocity = new Vector3(
            UnityEngine.Random.Range(_particleSpeedX.x, _particleSpeedX.y),
            UnityEngine.Random.Range(_particleSpeedY.x, _particleSpeedY.y),   // 위(+Y)
            0f
        );
        _ep.startColor    = Color.Lerp(_particleColorA, _particleColorB, UnityEngine.Random.value);
        _ep.startSize     = UnityEngine.Random.Range(_particleSize.x, _particleSize.y);
        _ep.startLifetime = UnityEngine.Random.Range(_particleLifetime.x, _particleLifetime.y);
        _ps.Emit(_ep, 1);
    }

    // ══════════════════════════════════════════════════════════════════════
    // 유틸리티
    // ══════════════════════════════════════════════════════════════════════

    private static IEnumerator WaitUnscaled(float seconds)
    {
        float e = 0f;
        while (e < seconds) { e += Time.unscaledDeltaTime; yield return null; }
    }
}
