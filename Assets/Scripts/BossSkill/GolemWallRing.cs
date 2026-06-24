using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 골렘 구역을 둘러싸는 가시 바위 벽 링.
/// 중심 기준으로 원형으로 가시 세그먼트(SpriteRenderer)를 배치하고,
/// 닫힌 EdgeCollider2D 루프로 플레이어의 탈출을 막는다(경계만 차단, 내부는 자유).
///
/// 비주얼은 커스텀 쉐이더 Custom/GolemSpikeWall(절차적 픽셀 가시)로 그리고,
/// 솟음/가라앉음은 쉐이더 _Reveal(0↔1)을 가시별로 순차(stagger) 구동해 연출한다.
/// </summary>
[RequireComponent(typeof(EdgeCollider2D))]
public class GolemWallRing : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private float _radius = 10f;
    [SerializeField] private int _segments = 24;
    [Tooltip("가시 한 개의 기본 높이(월드 유닛)")]
    [SerializeField] private float _spikeHeight = 3.2f;
    [Tooltip("가시 한 개의 기본 폭(월드 유닛) — 기존 2배, 이웃과 겹치도록")]
    [SerializeField] private float _spikeWidth = 2.8f;
    [Tooltip("세그먼트별 높이 랜덤 편차 비율(0~1). 클수록 실루엣이 불규칙")]
    [Range(0f, 0.6f)] [SerializeField] private float _heightJitter = 0.45f;

    [Header("Look (Custom/GolemSpikeWall 쉐이더)")]
    [Tooltip("가시 바위 기본 색 (붉은 바위 골렘 색)")]
    [SerializeField] private Color _rockColor = new Color(0.48f, 0.26f, 0.20f, 1f);
    [SerializeField] private Color _shadowColor = new Color(0.24f, 0.11f, 0.08f, 1f);
    [SerializeField] private Color _highlightColor = new Color(0.68f, 0.42f, 0.32f, 1f);
    [Tooltip("가시 윤곽 림 발광 색(HDR, 골렘 에너지)")]
    [ColorUsage(true, true)] [SerializeField] private Color _glowColor = new Color(1.1f, 0.45f, 0.25f, 1f);
    [Range(0f, 4f)] [SerializeField] private float _glowIntensity = 0.5f;
    [Tooltip("픽셀 그리드 크기(클수록 더 잘게)")]
    [Range(8f, 128f)] [SerializeField] private float _pixelSize = 36f;
    [Tooltip("실루엣 거칠기(클수록 가장자리가 불규칙)")]
    [Range(0f, 1f)] [SerializeField] private float _edgeRoughness = 0.7f;
    [Tooltip("끝 뾰족함(작을수록 위쪽이 두툼하게 채워짐)")]
    [Range(0.3f, 2.5f)] [SerializeField] private float _taper = 0.5f;
    [Tooltip("색 불규칙 정도")]
    [Range(0f, 1f)] [SerializeField] private float _colorVariation = 0.5f;
    [Tooltip("2.5D 패싯(각진 면) 입체 음영 강도")]
    [Range(0f, 1f)] [SerializeField] private float _facetStrength = 0.75f;

    [Header("Ground Shadow (접지 그림자)")]
    [Tooltip("base 아래 접지 그림자의 진하기(0=없음)")]
    [Range(0f, 1f)] [SerializeField] private float _groundShadowAlpha = 0.5f;
    [Tooltip("접지 그림자 크기(x=가로배율, y=세로배율). 가시폭 기준")]
    [SerializeField] private Vector2 _groundShadowScale = new Vector2(1.15f, 0.5f);

    [Header("Perspective (앞뒤 원근)")]
    [Tooltip("앞(화면 아래) 가시의 크기 배율")]
    [Range(1f, 1.6f)] [SerializeField] private float _frontScale = 1.18f;
    [Tooltip("뒤(화면 위) 가시의 크기 배율")]
    [Range(0.5f, 1f)] [SerializeField] private float _backScale = 0.78f;
    [Tooltip("앞 가시 밝기 배율")]
    [Range(0.6f, 1.5f)] [SerializeField] private float _frontBrightness = 1.1f;
    [Tooltip("뒤 가시 밝기 배율(멀수록 어둡게)")]
    [Range(0.3f, 1f)] [SerializeField] private float _backBrightness = 0.68f;

    [Header("Physics")]
    [Tooltip("벽 콜라이더의 두께(EdgeRadius)")]
    [SerializeField] private float _edgeThickness = 0.3f;

    [Header("Player Occlusion Fade (플레이어 가림 시 반투명)")]
    [Tooltip("플레이어를 앞에서 가리는 가시의 알파(0=완전 투명, 1=불투명)")]
    [Range(0f, 1f)] [SerializeField] private float _occludedAlpha = 0.35f;
    [Tooltip("알파가 변하는 속도(초당)")]
    [SerializeField] private float _fadeSpeed = 6f;

    [Header("Emerge / Sink Animation")]
    [Tooltip("솟아오르는 데 걸리는 시간(초)")]
    [SerializeField] private float _riseDuration = 0.8f;
    [Tooltip("가라앉는 데 걸리는 시간(초)")]
    [SerializeField] private float _sinkDuration = 0.7f;
    [Tooltip("가시들이 순차적으로 솟는 총 확산 시간(0=동시)")]
    [SerializeField] private float _stagger = 0.45f;
    [Tooltip("솟을 때 카메라 흔들림 강도")]
    [SerializeField] private float _shakeMagnitude = 0.35f;

    private static readonly int IdSeed   = Shader.PropertyToID("_Seed");
    private static readonly int IdReveal = Shader.PropertyToID("_Reveal");

    private readonly List<SpriteRenderer> _spikes = new List<SpriteRenderer>();
    private readonly List<float> _alpha = new List<float>(); // 가시별 현재 알파(가림 페이드용)
    private Transform _player;                                // 가림 판정 대상
    private EdgeCollider2D _edge;
    private Material _sharedMaterial;     // Custom/GolemSpikeWall 머티리얼 (런타임 생성 또는 주입)
    private MaterialPropertyBlock _mpb;
    private Sprite _spikeSprite;
    private Sprite _shadowSprite;     // 접지 그림자용 부드러운 타원

    /// <summary>중심/반지름/세그먼트 수로 벽 링을 동적으로 생성한다.</summary>
    public static GolemWallRing Create(Vector3 center, float radius, int segments,
                                       Material sharedMaterial = null)
    {
        var go = new GameObject("GolemWallRing");
        go.transform.position = center;

        // 벽은 물리적으로는 막되(충돌 매트릭스상 Player/Enemy와 계속 충돌), 몹 AI 시야(LoS,
        // DetectionSystem._obstacleMask=Default) 레이캐스트에는 걸리지 않도록 'Ignore Raycast' 레이어에 둔다.
        // → 보스전 중 벽 바깥 몹이 벽 안 플레이어를 감지할 수 있게 됨. 레이어가 없으면 Default 유지.
        int wallLayer = LayerMask.NameToLayer("Ignore Raycast");
        if (wallLayer >= 0) go.layer = wallLayer;

        var ring = go.AddComponent<GolemWallRing>();
        ring._radius = radius;
        ring._segments = Mathf.Max(3, segments);
        ring._sharedMaterial = sharedMaterial;
        ring.Build();
        return ring;
    }

    private void Build()
    {
        _edge = GetComponent<EdgeCollider2D>();

        // 반지름이 커져도 이웃 가시가 항상 겹치도록 세그먼트 수를 자동 보정(둘레/가시폭 기준)
        int minSeg = Mathf.CeilToInt((2f * Mathf.PI * _radius) / Mathf.Max(0.1f, _spikeWidth * 0.6f));
        _segments = Mathf.Max(_segments, minSeg);

        BuildColliderLoop();

        _spikeSprite = CreateQuadSprite();
        _shadowSprite = CreateSoftEllipseSprite();
        _mpb = new MaterialPropertyBlock();
        EnsureMaterial();

        for (int i = 0; i < _segments; i++)
        {
            float ang = (Mathf.PI * 2f) * i / _segments;
            float cos = Mathf.Cos(ang);
            float sin = Mathf.Sin(ang);
            Vector3 localPos = new Vector3(cos * _radius, sin * _radius, 0f);

            // 앞뒤 원근: 화면 아래(앞, y=-radius)일수록 크고 밝게, 위(뒤, y=+radius)일수록 작고 어둡게
            float depth01 = (localPos.y + _radius) / (2f * _radius); // 0=앞 ~ 1=뒤
            float persp     = Mathf.Lerp(_frontScale, _backScale, depth01);
            float brightness = Mathf.Lerp(_frontBrightness, _backBrightness, depth01);

            // ── 접지 그림자(가시 base 아래 부드러운 타원) ──
            var shadowGO = new GameObject($"Shadow_{i}");
            shadowGO.transform.SetParent(transform, false);
            shadowGO.transform.localPosition = new Vector3(localPos.x, localPos.y, -0.05f); // 바닥보다 살짝 카메라쪽
            var shr = shadowGO.AddComponent<SpriteRenderer>();
            shr.sprite = _shadowSprite;
            shr.color = new Color(_shadowColor.r, _shadowColor.g, _shadowColor.b, _groundShadowAlpha);
            shr.sortingOrder = 2; // 바닥 타일맵과 같은 층(카메라쪽 z로 위에 그려짐), 모든 가시(order≥3)보다 아래
            float sw = _spikeWidth * persp;
            shadowGO.transform.localScale = new Vector3(sw * _groundShadowScale.x, sw * _groundShadowScale.y, 1f);

            var spikeGO = new GameObject($"Spike_{i}");
            spikeGO.transform.SetParent(transform, false);
            spikeGO.transform.localPosition = localPos;

            var sr = spikeGO.AddComponent<SpriteRenderer>();
            sr.sprite = _spikeSprite;
            // 원근 밝기를 베이스 색에 미리 반영(알파는 가림 페이드가 따로 제어)
            sr.color = new Color(_rockColor.r * brightness, _rockColor.g * brightness, _rockColor.b * brightness, 1f);
            sr.sharedMaterial = _sharedMaterial; // 쉐이더가 가시 모양을 절차적으로 그림

            // 높이 랜덤 편차 + 폭/높이를 월드 유닛에 맞춰 스케일 (원근 배율 적용)
            float h = _spikeHeight * (1f + UnityEngine.Random.Range(-_heightJitter, _heightJitter)) * persp;
            sr.transform.localScale = new Vector3(_spikeWidth * persp, h, 1f);

            // 앞/뒤 깊이 정렬: 바닥 타일맵(order 2) 위에서 시작하도록 base 3을 더하고,
            // y가 낮을수록(화면 앞) 큰 order. 상단(뒤) 가시가 바닥 뒤로 숨어 '갭'이 생기던 문제 해결.
            sr.sortingOrder = 3 + Mathf.RoundToInt((_radius - localPos.y) / (2f * _radius) * 30f);

            // 가시별 난수 시드 + 초기 Reveal(완전 표시) — MPB로 배칭 유지하며 개별 주입
            sr.GetPropertyBlock(_mpb);
            _mpb.SetFloat(IdSeed, UnityEngine.Random.Range(0f, 1000f));
            _mpb.SetFloat(IdReveal, 1f);
            sr.SetPropertyBlock(_mpb);

            _spikes.Add(sr);
            _alpha.Add(1f);
        }
    }

    /// <summary>플레이어를 앞에서 가리는 가시를 반투명하게 만들기 위해 대상을 지정한다.</summary>
    public void SetPlayer(Transform player) => _player = player;

    // 플레이어를 앞에서(아래쪽에서) 가리는 가시만 반투명으로 페이드. 그 외엔 불투명 복귀.
    private void LateUpdate()
    {
        if (_player == null || _spikes.Count == 0) return;

        Vector2 pp = _player.position;
        for (int i = 0; i < _spikes.Count; i++)
        {
            var sr = _spikes[i];
            if (sr == null) continue;

            Vector3 sp = sr.transform.position;
            float dx = Mathf.Abs(sp.x - pp.x);
            float dy = pp.y - sp.y; // 플레이어가 가시 base보다 위에 있는 정도

            // 가시가 플레이어보다 화면 앞(아래)에서 그려지고, 플레이어가 가시 몸통 범위를 덮는가
            bool covering = (sp.y <= pp.y + 0.3f) && (dy < _spikeHeight) && (dx < _spikeWidth * 0.6f);

            float target = covering ? _occludedAlpha : 1f;
            _alpha[i] = Mathf.MoveTowards(_alpha[i], target, _fadeSpeed * Time.deltaTime);

            var c = sr.color;
            c.a = _alpha[i];
            sr.color = c;
        }
    }

    /// <summary>Custom/GolemSpikeWall 머티리얼을 준비하고 팔레트/픽셀 파라미터를 주입한다.</summary>
    private void EnsureMaterial()
    {
        if (_sharedMaterial == null)
        {
            Shader shader = Shader.Find("Custom/GolemSpikeWall");
            if (shader == null)
            {
                Debug.LogError("[GolemWallRing] 'Custom/GolemSpikeWall' 쉐이더를 찾지 못했습니다.");
                return;
            }
            _sharedMaterial = new Material(shader);
        }

        _sharedMaterial.SetColor("_BaseColor", _rockColor);
        _sharedMaterial.SetColor("_ShadowColor", _shadowColor);
        _sharedMaterial.SetColor("_HighlightColor", _highlightColor);
        _sharedMaterial.SetColor("_GlowColor", _glowColor);
        _sharedMaterial.SetFloat("_GlowIntensity", _glowIntensity);
        _sharedMaterial.SetFloat("_PixelSize", _pixelSize);
        _sharedMaterial.SetFloat("_EdgeRoughness", _edgeRoughness);
        _sharedMaterial.SetFloat("_Taper", _taper);
        _sharedMaterial.SetFloat("_ColorVariation", _colorVariation);
        _sharedMaterial.SetFloat("_FacetStrength", _facetStrength);
    }

    /// <summary>모든 가시의 솟음 정도(0=땅속, 1=완전)를 설정한다. (Step 4 애니메이션에서 호출)</summary>
    public void SetReveal(float reveal)
    {
        reveal = Mathf.Clamp01(reveal);
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        foreach (var sr in _spikes)
        {
            if (sr == null) continue;
            sr.GetPropertyBlock(_mpb);
            _mpb.SetFloat(IdReveal, reveal);
            sr.SetPropertyBlock(_mpb);
        }
    }

    /// <summary>원을 따라 닫힌 EdgeCollider2D 루프를 만들어 경계를 막는다.</summary>
    private void BuildColliderLoop()
    {
        var pts = new List<Vector2>(_segments + 1);
        for (int i = 0; i <= _segments; i++)
        {
            float ang = (Mathf.PI * 2f) * i / _segments; // 마지막(i==_segments)은 시작점으로 닫힘
            pts.Add(new Vector2(Mathf.Cos(ang) * _radius, Mathf.Sin(ang) * _radius));
        }
        _edge.edgeRadius = _edgeThickness;
        _edge.points = pts.ToArray();
        _edge.enabled = false; // 솟음 연출(Step 4) 후 켜질 예정. Step 2에서는 PlayRise가 즉시 켠다.
    }

    // ── 솟음 / 가라앉음 연출 ───────────────────────────────────────────────

    /// <summary>가시들이 땅을 뚫고 순차적으로 솟아오른다. 완료 후 경계 콜라이더 ON.</summary>
    public void PlayRise()
    {
        foreach (var sr in _spikes)
            if (sr != null) sr.enabled = true;
        SetReveal(0f); // 땅속(보이지 않음)에서 시작
        StartCoroutine(RiseRoutine());
    }

    /// <summary>가시들이 순차적으로 땅속으로 가라앉은 뒤 제거된다.</summary>
    public void PlaySink(Action onComplete = null)
    {
        if (_edge != null) _edge.enabled = false; // 가라앉기 시작하면 즉시 통과 가능
        StartCoroutine(SinkRoutine(onComplete));
    }

    private IEnumerator RiseRoutine()
    {
        CameraShakeController.Instance?.Shake(0.25f, _shakeMagnitude); // 솟는 진동

        int n = Mathf.Max(1, _spikes.Count);
        float total = _riseDuration + _stagger;
        float t = 0f;
        while (t < total)
        {
            t += Time.unscaledDeltaTime; // 진입 연출(timeScale=0) 중에도 솟아오르도록 실시간 기준
            for (int i = 0; i < n; i++)
            {
                float delay = (n > 1) ? _stagger * (i / (float)(n - 1)) : 0f;
                float p = Mathf.Clamp01((t - delay) / _riseDuration);
                SetSpikeReveal(i, EaseOutBack(p)); // 살짝 튀어 오르는 느낌
            }
            yield return null;
        }

        SetReveal(1f);
        if (_edge != null) _edge.enabled = true; // 완전히 솟은 뒤 탈출 차단
    }

    private IEnumerator SinkRoutine(Action onComplete)
    {
        int n = Mathf.Max(1, _spikes.Count);
        float total = _sinkDuration + _stagger;
        float t = 0f;
        while (t < total)
        {
            t += Time.unscaledDeltaTime; // 카메라 연출과 동일하게 실시간 기준
            for (int i = 0; i < n; i++)
            {
                float delay = (n > 1) ? _stagger * (i / (float)(n - 1)) : 0f;
                float p = Mathf.Clamp01((t - delay) / _sinkDuration);
                SetSpikeReveal(i, 1f - EaseInCubic(p)); // 1→0 으로 땅속으로
            }
            yield return null;
        }

        SetReveal(0f);
        onComplete?.Invoke();
        Destroy(gameObject);
    }

    /// <summary>가시 한 개의 솟음 정도를 설정한다.</summary>
    private void SetSpikeReveal(int index, float reveal)
    {
        if (index < 0 || index >= _spikes.Count) return;
        var sr = _spikes[index];
        if (sr == null) return;
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        sr.GetPropertyBlock(_mpb);
        _mpb.SetFloat(IdReveal, Mathf.Clamp01(reveal));
        sr.SetPropertyBlock(_mpb);
    }

    // 살짝 오버슈트하며 안착(솟음) / 가속하며 빨려 들어감(가라앉음)
    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float xm = x - 1f;
        return 1f + c3 * xm * xm * xm + c1 * xm * xm;
    }
    private static float EaseInCubic(float x) => x * x * x;

    // ── 가시용 쿼드 스프라이트 (모양은 Custom/GolemSpikeWall 쉐이더가 절차적으로 그림) ──
    // 피벗을 하단 중앙(0.5, 0)에 둬 base가 원 위에 놓이고 위로 자란다. FullRect로 쿼드 메시 보장.
    private static Sprite CreateQuadSprite()
    {
        const int w = 4, h = 4;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var px = new Color[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = Color.white;
        tex.SetPixels(px);
        tex.Apply(false, true);

        // pixelsPerUnit = h → localScale.y == 월드 높이(유닛). FullRect 메시로 UV 0~1 전체 확보.
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), h,
                             0, SpriteMeshType.FullRect);
    }

    // ── 접지 그림자용 부드러운 원형(타원은 스케일로) 스프라이트 ──
    // 중앙이 진하고 가장자리로 부드럽게 사라지는 라디얼 알파. pivot 중앙, ppu=size → localScale 1당 1유닛.
    private static Sprite CreateSoftEllipseSprite()
    {
        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp
        };
        var px = new Color[size * size];
        Vector2 c = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c) / r;
                float a = Mathf.Clamp01(1f - d);
                a *= a; // 가장자리를 더 부드럽게(제곱 폴오프)
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size,
                             0, SpriteMeshType.FullRect);
    }
}
