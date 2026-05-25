using UnityEngine;

/// <summary>
/// 몬스터 머리 위에 SpriteRenderer 기반 HP 바를 표시합니다 (SRP).
/// HealthSystem.OnHpChanged 이벤트 구독으로 폴링 없이 갱신합니다.
/// 
/// 구조:
///   HPBarRoot (이 컴포넌트)
///     ├ Background (검정 바)
///     └ Fill (체력 비율에 따라 X스케일 변경, 색상 보간)
///
/// 풀링 대응: OnEnable/OnDisable에서 이벤트 구독/해제.
/// </summary>
public sealed class MonsterHPBar : MonoBehaviour
{
    [Header("HP Bar Settings")]
    [Tooltip("HP 바의 Y 오프셋 (몬스터 상단 기준)")]
    [SerializeField] private float _yOffset = 1.0f;
    [Tooltip("HP 바의 전체 너비")]
    [SerializeField] private float _barWidth = 1.0f;
    [Tooltip("HP 바의 높이")]
    [SerializeField] private float _barHeight = 0.1f;

    // 색상 설정
    private static readonly Color COLOR_HIGH   = new Color(0.2f, 0.85f, 0.2f, 1f);  // 초록
    private static readonly Color COLOR_MID    = new Color(0.95f, 0.85f, 0.1f, 1f); // 노랑
    private static readonly Color COLOR_LOW    = new Color(0.9f, 0.15f, 0.1f, 1f);  // 빨강
    private static readonly Color COLOR_BG     = new Color(0.1f, 0.1f, 0.1f, 0.85f);
    private static readonly Color COLOR_BORDER = new Color(0.3f, 0.3f, 0.3f, 0.9f);

    // 런타임 참조
    private HealthSystem _health;
    private Transform _barRoot;
    private SpriteRenderer _bgRenderer;
    private SpriteRenderer _fillRenderer;
    private SpriteRenderer _borderRenderer;
    private MaterialPropertyBlock _fillMPB;

    // 화이트 1x1 텍스처 (공유)
    private static Texture2D _sharedWhiteTex;
    private static Sprite _sharedWhiteSprite;

    private void Awake()
    {
        _health = GetComponent<HealthSystem>();
        _fillMPB = new MaterialPropertyBlock();
        EnsureSharedSprite();
        BuildHPBar();
    }

    private void OnEnable()
    {
        if (_health != null)
            _health.OnHpChanged += UpdateBar;

        // 초기 상태 동기화
        if (_health != null)
            UpdateBar(_health.CurrentHp, _health.MaxHp);
    }

    private void OnDisable()
    {
        if (_health != null)
            _health.OnHpChanged -= UpdateBar;
    }

    private void LateUpdate()
    {
        // HP 바를 몬스터 상단에 고정 (회전 방지)
        if (_barRoot != null)
        {
            _barRoot.position = transform.position + new Vector3(0f, _yOffset, 0f);
            _barRoot.rotation = Quaternion.identity;
        }
    }

    /// <summary>HP 바의 루트 Transform을 반환합니다 (DebuffIconDisplay에서 참조).</summary>
    public Transform BarRoot => _barRoot;

    /// <summary>HP 바의 전체 너비를 반환합니다.</summary>
    public float BarWidth => _barWidth;

    /// <summary>HP 바의 높이를 반환합니다.</summary>
    public float BarHeight => _barHeight;

    // ── HP 바 구축 ──────────────────────────────────────────

    private void BuildHPBar()
    {
        // 루트 오브젝트
        _barRoot = new GameObject("HPBar_Root").transform;
        _barRoot.SetParent(transform, false);
        _barRoot.localPosition = new Vector3(0f, _yOffset, 0f);

        // 테두리 (약간 크게)
        GameObject borderObj = new GameObject("HPBar_Border");
        borderObj.transform.SetParent(_barRoot, false);
        borderObj.transform.localPosition = Vector3.zero;
        borderObj.transform.localScale = new Vector3(_barWidth + 0.04f, _barHeight + 0.04f, 1f);
        _borderRenderer = borderObj.AddComponent<SpriteRenderer>();
        _borderRenderer.sprite = _sharedWhiteSprite;
        _borderRenderer.color = COLOR_BORDER;
        _borderRenderer.sortingLayerName = "UI";
        _borderRenderer.sortingOrder = 98;

        // 배경 (검정)
        GameObject bgObj = new GameObject("HPBar_BG");
        bgObj.transform.SetParent(_barRoot, false);
        bgObj.transform.localPosition = Vector3.zero;
        bgObj.transform.localScale = new Vector3(_barWidth, _barHeight, 1f);
        _bgRenderer = bgObj.AddComponent<SpriteRenderer>();
        _bgRenderer.sprite = _sharedWhiteSprite;
        _bgRenderer.color = COLOR_BG;
        _bgRenderer.sortingLayerName = "UI";
        _bgRenderer.sortingOrder = 99;

        // 채움 (초록 → 빨강)
        GameObject fillObj = new GameObject("HPBar_Fill");
        fillObj.transform.SetParent(_barRoot, false);
        // 왼쪽 정렬: 피봇이 중앙이므로 X를 0으로 설정 후 스케일로 조절
        fillObj.transform.localPosition = Vector3.zero;
        fillObj.transform.localScale = new Vector3(_barWidth, _barHeight, 1f);
        _fillRenderer = fillObj.AddComponent<SpriteRenderer>();
        _fillRenderer.sprite = _sharedWhiteSprite;
        _fillRenderer.color = COLOR_HIGH;
        _fillRenderer.sortingLayerName = "UI";
        _fillRenderer.sortingOrder = 100;
    }

    // ── HP 바 갱신 ──────────────────────────────────────────

    private void UpdateBar(float currentHp, float maxHp)
    {
        if (_fillRenderer == null) return;

        float ratio = maxHp > 0f ? Mathf.Clamp01(currentHp / maxHp) : 0f;

        // 스케일로 길이 조절 (왼쪽 정렬 보정)
        float fillWidth = _barWidth * ratio;
        _fillRenderer.transform.localScale = new Vector3(fillWidth, _barHeight, 1f);

        // 왼쪽 정렬: 전체 바의 왼쪽 끝에서 시작
        float leftEdge = -_barWidth * 0.5f;
        float fillCenter = leftEdge + fillWidth * 0.5f;
        _fillRenderer.transform.localPosition = new Vector3(fillCenter, 0f, 0f);

        // 색상 보간: 100%=초록, 50%=노랑, 0%=빨강
        Color barColor;
        if (ratio > 0.5f)
        {
            float t = (ratio - 0.5f) * 2f; // 0.5~1.0 → 0~1
            barColor = Color.Lerp(COLOR_MID, COLOR_HIGH, t);
        }
        else
        {
            float t = ratio * 2f; // 0~0.5 → 0~1
            barColor = Color.Lerp(COLOR_LOW, COLOR_MID, t);
        }
        _fillRenderer.color = barColor;

        // 사망 시 HP 바 숨김
        _barRoot.gameObject.SetActive(currentHp > 0f);
    }

    // ── 공유 텍스처 ─────────────────────────────────────────

    private static void EnsureSharedSprite()
    {
        if (_sharedWhiteSprite != null) return;

        _sharedWhiteTex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        _sharedWhiteTex.filterMode = FilterMode.Point;
        Color[] pixels = new Color[16];
        for (int i = 0; i < 16; i++) pixels[i] = Color.white;
        _sharedWhiteTex.SetPixels(pixels);
        _sharedWhiteTex.Apply(false, true);

        _sharedWhiteSprite = Sprite.Create(
            _sharedWhiteTex,
            new Rect(0, 0, 4, 4),
            new Vector2(0.5f, 0.5f),
            4f // pixelsPerUnit
        );
    }

    private void OnDestroy()
    {
        if (_barRoot != null)
            Destroy(_barRoot.gameObject);
    }
}
