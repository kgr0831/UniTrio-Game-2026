using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스킬 게이지 뷰 (플레이어 월드 캔버스). SkillGaugeSystem(모델) 이벤트를 구독해
/// 3개 칸(셀)을 갱신합니다. 모델-뷰를 분리해 SRP를 준수합니다.
///
/// - 셀 i 는 1/3 구간 [(i)*100, (i+1)*100] 을 표현 (Image Type=Filled, Horizontal).
/// - 셀 i fillAmount = clamp01((gauge - i*100) / 100)
/// - 게이지가 i*100 을 초과하면 셀 표시, 아니면 숨김(alpha 0)
/// - 새 칸 진입 순간 짧게 깜빡(밝기 펄스) 후 유지
/// - 색상: 현재 속성색을 셀0 연함 → 셀2 진함으로 틴트
/// </summary>
public class SkillGaugeUI : MonoBehaviour
{
    [Header("Dependencies (비우면 자동 탐색)")]
    [SerializeField] private SkillGaugeSystem      _skillGauge;
    [SerializeField] private ElementalWeaponSystem _elementSystem;

    [Header("Cells (왼→오른쪽 = 1→3단계)")]
    [Tooltip("1/3 구간을 표현할 3개의 Filled 이미지. Index 0=1단계, 1=2단계, 2=3단계")]
    [SerializeField] private Image[] _cells = new Image[3];

    [Header("Blink (새 칸 등장 연출)")]
    [SerializeField] private float _blinkPeak     = 1.6f;   // 깜빡 시 최대 밝기 배수
    [SerializeField] private float _blinkDuration = 0.28f;  // 깜빡 지속 시간(초)

    // 속성별 기준색 (ElementalWeaponSystem aura 팔레트와 동일 계열)
    private static readonly Color[] _elementBase = new Color[]
    {
        new Color(0.85f, 0.7f,  0.25f), // Earth: 황금
        new Color(1.0f,  0.35f, 0.05f), // Fire:  주황빨강
        new Color(0.15f, 0.85f, 1.0f)   // Ice:   시안
    };

    private const float STAGE = 100f;

    // 현재 각 셀의 기준 RGB (속성·연/진 반영). alpha는 표시 여부로 별도 제어.
    private readonly Color[] _cellRGB = new Color[3];
    private readonly Coroutine[] _blinkCo = new Coroutine[3];

    private CanvasGroup _canvasGroup;
    private Coroutine _fadeCoroutine;
    private int _lastFilledThirds = 0;
    private bool _isInitialized = false;

    private void Awake()
    {
        if (_skillGauge == null)
            _skillGauge = GetComponentInParent<SkillGaugeSystem>();
        if (_skillGauge == null)
            _skillGauge = FindObjectOfType<SkillGaugeSystem>();

        if (_elementSystem == null)
            _elementSystem = ElementalWeaponSystem.Instance;
        if (_elementSystem == null)
            _elementSystem = FindObjectOfType<ElementalWeaponSystem>();

        // 셀이 인스펙터에서 비어 있으면 자식에서 자동 수집 ("Cell"로 시작하는 자식, 형제순)
        if (_cells == null || _cells.Length == 0 || _cells[0] == null)
            AutoCollectCells();

        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f; // 기본적으로 안보임
    }

    /// <summary>자식 중 이름이 "Cell"로 시작하는 Image를 형제 순서대로 수집합니다.</summary>
    private void AutoCollectCells()
    {
        var found = new System.Collections.Generic.List<Image>();
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (!child.name.StartsWith("Cell")) continue;
            var img = child.GetComponent<Image>();
            if (img != null) found.Add(img);
        }
        if (found.Count > 0)
            _cells = found.ToArray();
    }

    private void OnEnable()
    {
        if (_skillGauge != null)
        {
            _skillGauge.OnGaugeChanged += UpdateView;
            _skillGauge.OnThirdReached += BlinkCell;
        }
        if (_elementSystem != null)
            _elementSystem.OnGaugeChanged += OnElementGaugeChanged;
    }

    private void OnDisable()
    {
        if (_skillGauge != null)
        {
            _skillGauge.OnGaugeChanged -= UpdateView;
            _skillGauge.OnThirdReached -= BlinkCell;
        }
        if (_elementSystem != null)
            _elementSystem.OnGaugeChanged -= OnElementGaugeChanged;
    }

    private void Start()
    {
        RefreshColors();
        
        float initialGauge = _skillGauge != null ? _skillGauge.CurrentGauge : 0f;
        _lastFilledThirds = Mathf.FloorToInt((initialGauge + 0.001f) / STAGE);
        _isInitialized = true;

        UpdateView(initialGauge);
    }

    // 속성 게이지/속성 변경 이벤트 → 색상만 재계산 (ratio는 사용하지 않음)
    private void OnElementGaugeChanged(float ratio, ElementType element)
    {
        RefreshColors();
        UpdateView(_skillGauge != null ? _skillGauge.CurrentGauge : 0f);
    }

    /// <summary>현재 속성에 맞춰 셀0(연함)~셀2(진함) 기준색을 계산합니다.</summary>
    private void RefreshColors()
    {
        ElementType element = _elementSystem != null ? _elementSystem.CurrentElement : ElementType.Earth;
        Color dark  = _elementBase[(int)element];
        Color light = Color.Lerp(dark, Color.white, 0.55f); // 연함

        for (int i = 0; i < _cellRGB.Length; i++)
        {
            float t = _cellRGB.Length > 1 ? (float)i / (_cellRGB.Length - 1) : 0f;
            _cellRGB[i] = Color.Lerp(light, dark, t); // 0=연함 → 2=진함
        }
    }

    /// <summary>게이지 값에 맞춰 각 칸의 채움/표시를 갱신합니다.</summary>
    private void UpdateView(float gauge)
    {
        if (_isInitialized)
        {
            int currentFilledThirds = Mathf.FloorToInt((gauge + 0.001f) / STAGE);
            if (currentFilledThirds > _lastFilledThirds && currentFilledThirds <= 3 && currentFilledThirds > 0)
            {
                TriggerFadeInOut();
            }
            _lastFilledThirds = currentFilledThirds;
        }

        for (int i = 0; i < _cells.Length; i++)
        {
            Image cell = _cells[i];
            if (cell == null) continue;

            float fill = Mathf.Clamp01((gauge - i * STAGE) / STAGE);
            bool  on   = gauge > i * STAGE + 0.0001f;

            cell.fillAmount = fill;

            // 깜빡 중인 셀은 코루틴이 색을 제어하므로 건드리지 않는다.
            if (_blinkCo[i] == null)
                ApplyCellColor(i, on ? 1f : 0f, 1f);
        }
    }

    /// <summary>셀 색상 적용. alpha=표시여부, brightness=밝기 배수(깜빡용).</summary>
    private void ApplyCellColor(int index, float alpha, float brightness)
    {
        if (index < 0 || index >= _cells.Length || _cells[index] == null) return;
        Color rgb = _cellRGB[index] * brightness;
        _cells[index].color = new Color(rgb.r, rgb.g, rgb.b, alpha);
    }

    // 새 칸 진입(OnThirdReached, 1~3) → 해당 셀 깜빡
    private void BlinkCell(int thirdIndex)
    {
        int i = thirdIndex - 1; // 1~3 → 0~2
        if (i < 0 || i >= _cells.Length || _cells[i] == null) return;

        if (_blinkCo[i] != null) StopCoroutine(_blinkCo[i]);
        _blinkCo[i] = StartCoroutine(BlinkRoutine(i));
    }

    private IEnumerator BlinkRoutine(int index)
    {
        float half = _blinkDuration * 0.5f;
        float t = 0f;

        // 0 → peak (밝기 상승, 동시에 등장)
        while (t < half)
        {
            t += Time.deltaTime;
            float k = half > 0f ? t / half : 1f;
            ApplyCellColor(index, 1f, Mathf.Lerp(1f, _blinkPeak, k));
            yield return null;
        }
        // peak → 1 (원래 밝기로)
        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float k = half > 0f ? t / half : 1f;
            ApplyCellColor(index, 1f, Mathf.Lerp(_blinkPeak, 1f, k));
            yield return null;
        }

        _blinkCo[index] = null;
        // 깜빡 종료 후 현재 게이지 기준으로 표시 상태 재확정
        UpdateView(_skillGauge != null ? _skillGauge.CurrentGauge : 0f);
    }

    private void TriggerFadeInOut()
    {
        if (_fadeCoroutine != null)
            StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeInOutRoutine());
    }

    private IEnumerator FadeInOutRoutine()
    {
        // 페이드 인
        while (_canvasGroup.alpha < 1f)
        {
            _canvasGroup.alpha += Time.deltaTime * 4f; // 0.25초 만에 페이드인
            yield return null;
        }
        _canvasGroup.alpha = 1f;

        // 3초 대기
        yield return new WaitForSeconds(3f);

        // 페이드 아웃
        while (_canvasGroup.alpha > 0f)
        {
            _canvasGroup.alpha -= Time.deltaTime * 2f; // 0.5초 만에 페이드아웃
            yield return null;
        }
        _canvasGroup.alpha = 0f;
    }
}
