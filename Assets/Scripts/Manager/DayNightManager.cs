using UnityEngine;

/// <summary>
/// 낮/밤 사이클 매니저.
/// 게임 내 시간(0~24시)을 진행시키며 시간대에 따라
///  - 태양(Directional Light)의 색·강도
///  - 스프라이트 셰이더(UniTrio/SpriteLitFlash)의 글로벌 주변광 색
///  - RenderSettings의 주변광
/// 을 Gradient/Curve로 부드럽게 보간합니다.
/// 새벽(분홍) → 낮(따뜻한 흰색) → 석양(주황) → 밤(어두운 청색, 낮의 약 40%) 순환.
/// </summary>
public class DayNightManager : MonoBehaviour
{
    public static DayNightManager Instance { get; private set; }

    [Header("연결")]
    [Tooltip("태양 역할의 Directional Light. 비워두면 씬에서 자동 탐색합니다.")]
    [SerializeField] private Light _sunLight;

    [Header("시간 설정")]
    [Tooltip("게임 내 하루(24시간)가 현실 시간으로 몇 분인지")]
    [SerializeField, Min(0.1f)] private float _dayLengthMinutes = 10f;
    [Tooltip("시작 시각 (0~24시)")]
    [SerializeField, Range(0f, 24f)] private float _startHour = 9f;

    [Header("시간대별 연출 (가로축 = 0~24시를 0~1로 정규화)")]
    [SerializeField] private Gradient _sunColor = DefaultSunColor();
    [Tooltip("가로축이 '시간(0~24)' 단위인 강도 커브")]
    [SerializeField] private AnimationCurve _sunIntensity = DefaultSunIntensity();
    [SerializeField] private Gradient _ambientColor = DefaultAmbientColor();
    [Tooltip("NightFactor 계산 기준이 되는 한낮 태양 강도")]
    [SerializeField, Min(0.01f)] private float _maxSunIntensity = 0.6f;

    /// <summary>현재 게임 내 시각 (0~24)</summary>
    public float TimeOfDay { get; private set; }

    /// <summary>0 = 완전한 낮, 1 = 완전한 밤. 새벽/석양은 중간값.</summary>
    public float NightFactor { get; private set; }

    public bool IsNight => NightFactor > 0.5f;

    private static readonly int AmbientColorId = Shader.PropertyToID("_DayNightAmbientColor");
    private static readonly int EnabledId      = Shader.PropertyToID("_DayNightEnabled");

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (_sunLight == null)
        {
            // 씬에서 Directional Light 자동 탐색 (폴백)
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional)
                {
                    _sunLight = light;
                    break;
                }
            }
            if (_sunLight == null)
                Debug.LogWarning("[DayNightManager] Directional Light를 찾지 못했습니다. 태양광 제어가 비활성화됩니다.");
        }

        TimeOfDay = _startHour;
        ApplyLighting();
    }

    private void Update()
    {
        // 하루(24h)를 _dayLengthMinutes(현실 분)에 맞춰 진행
        TimeOfDay += (24f / (_dayLengthMinutes * 60f)) * Time.deltaTime;
        if (TimeOfDay >= 24f)
            TimeOfDay -= 24f;

        ApplyLighting();
    }

    private void ApplyLighting()
    {
        float t = TimeOfDay / 24f;

        float intensity = _sunIntensity.Evaluate(TimeOfDay);
        if (_sunLight != null)
        {
            _sunLight.color     = _sunColor.Evaluate(t);
            _sunLight.intensity = intensity;
        }

        Color ambient = _ambientColor.Evaluate(t);
        Shader.SetGlobalColor(AmbientColorId, ambient);
        Shader.SetGlobalFloat(EnabledId, 1f);

        // 3D 메시(골렘 바위 파편 등)도 같은 분위기를 따르도록 플랫 주변광 동기화
        RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = ambient;

        NightFactor = 1f - Mathf.Clamp01(intensity / _maxSunIntensity);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            // 매니저가 사라지면 셰이더가 머티리얼 폴백 주변광으로 복귀
            Shader.SetGlobalFloat(EnabledId, 0f);
        }
    }

    // ── 기본 연출 값 ─────────────────────────────────────────────
    // 시각(시) → Gradient time 변환: hour / 24

    private static Gradient DefaultSunColor()
    {
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.55f, 0.60f, 0.95f), 0f),            // 0시: 밤 (푸른 달빛)
                new GradientColorKey(new Color(0.55f, 0.60f, 0.95f), 4.5f / 24f),    // 4시반: 밤 끝
                new GradientColorKey(new Color(1f, 0.62f, 0.48f),    6f / 24f),      // 6시: 새벽 (분홍·주황)
                new GradientColorKey(new Color(1f, 0.90f, 0.78f),    7.5f / 24f),    // 7시반: 아침
                new GradientColorKey(new Color(1f, 0.96f, 0.88f),    12f / 24f),     // 정오: 따뜻한 흰색
                new GradientColorKey(new Color(1f, 0.85f, 0.65f),    17f / 24f),     // 17시: 늦은 오후
                new GradientColorKey(new Color(1f, 0.50f, 0.30f),    18.75f / 24f),  // 18시45분: 석양 (진한 주황)
                new GradientColorKey(new Color(0.55f, 0.60f, 0.95f), 20.25f / 24f),  // 20시15분: 밤 시작
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }

    private static AnimationCurve DefaultSunIntensity()
    {
        // 가로축 = 시간(0~24), 세로축 = 라이트 강도
        return new AnimationCurve(
            new Keyframe(0f,     0.02f),
            new Keyframe(4.5f,   0.02f),
            new Keyframe(6f,     0.25f),
            new Keyframe(7.5f,   0.55f),
            new Keyframe(12f,    0.60f),
            new Keyframe(17f,    0.50f),
            new Keyframe(18.75f, 0.25f),
            new Keyframe(20.25f, 0.02f),
            new Keyframe(24f,    0.02f));
    }

    private static Gradient DefaultAmbientColor()
    {
        // 리니어 색공간 기준 값. 감마 보정 후 화면에서 밤은 낮의 약 40% 밝기로 보임.
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.10f, 0.13f, 0.22f), 0f),            // 0시: 밤 (어두운 청색)
                new GradientColorKey(new Color(0.10f, 0.13f, 0.22f), 4.5f / 24f),    // 4시반
                new GradientColorKey(new Color(0.45f, 0.34f, 0.38f), 6f / 24f),      // 6시: 새벽 (분홍빛)
                new GradientColorKey(new Color(0.50f, 0.50f, 0.53f), 7.5f / 24f),    // 7시반: 아침
                new GradientColorKey(new Color(0.55f, 0.55f, 0.57f), 12f / 24f),     // 정오
                new GradientColorKey(new Color(0.55f, 0.45f, 0.38f), 17f / 24f),     // 17시: 늦은 오후 (따뜻함)
                new GradientColorKey(new Color(0.35f, 0.25f, 0.30f), 18.75f / 24f),  // 18시45분: 석양 (자줏빛)
                new GradientColorKey(new Color(0.10f, 0.13f, 0.22f), 20.25f / 24f),  // 20시15분: 밤
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }
}
