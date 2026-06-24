using System.Collections;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 사망/부활 컷신 전체 흐름을 제어하는 컨트롤러.
///
/// 흐름:
///   PlayerDeathEffect.OnDissolveComplete
///     → timeScale=0 확인 → DeathScreenUI.Show()
///     → [부활 버튼 클릭]
///     → Phase 1 : 충격(색상 반전 + 색수차 + Cinemachine 카메라 진동)
///     → Phase 2 : 역용해(아래→위) + 붉은 비네트
///     → Phase 3 : 채도 회복 + 필름 그레인 + 모션 트레일
///     → KarmaHandler.AddKarma(1) + 카르마 텍스트 출력
///     → timeScale=1 복원 + 입력 잠금 해제
/// </summary>
[DisallowMultipleComponent]
public class DeathCutsceneController : MonoBehaviour
{
    // ── References ────────────────────────────────────────────────────────

    [Header("References")]
    [SerializeField] private DeathScreenUI     _deathScreenUI;
    [SerializeField] private PlayerDeathEffect _playerDeathEffect;
    [SerializeField] private KarmaHandler      _karmaHandler;
    [SerializeField] private LivingEntity      _livingEntity;
    [SerializeField] private PlayerWeaponController _weaponController;
    [SerializeField] private PlayerStateMachine     _stateMachine;

    [Tooltip("플레이어 이동 입력을 차단할 컴포넌트. null이면 skip.")]
    [SerializeField] private MonoBehaviour     _playerInput;

    // ── Phase 1 ────────────────────────────────────────────────────────────

    [Header("Phase 1 — Shock")]
    [Tooltip("Cinemachine 3.x 임펄스 소스. CinemachineImpulseSource 컴포넌트.")]
    [SerializeField] private CinemachineImpulseSource _impulseSource;

    [Tooltip("부활 충격 카메라 진동 세기.")]
    [SerializeField] private float _impulseForce = 1.8f;

    [Tooltip("색상 반전이 최대치에 유지되는 시간(비스케일 초).")]
    [SerializeField] private float _invertHoldDuration = 1.2f;

    [Tooltip("색수차 최대 강도 (0~1).")]
    [SerializeField] private float _maxChromaticAberration = 0.9f;

    [Tooltip("부활 FX 전용 Volume (ChromaticAberration + Vignette 오버라이드 포함).")]
    [SerializeField] private Volume _fxVolume;

    // ── Phase 2 ────────────────────────────────────────────────────────────

    [Header("Phase 2 — Reverse Dissolve + Red Vignette")]
    [Tooltip("역용해를 담당하는 PlayerReverseDissolveController.")]
    [SerializeField] private PlayerReverseDissolveController _reverseDissolveCtrl;

    [Tooltip("비네트 최대 강도 (0~1).")]
    [SerializeField] private float _maxVignetteIntensity = 0.55f;

    // ── Phase 공통 Duration ────────────────────────────────────────────────

    [Header("Phase Durations (Unscaled Seconds)")]
    [SerializeField] private float _phase1Duration = 2.4f;
    [SerializeField] private float _phase2Duration = 1.8f;
    [SerializeField] private float _phase3Duration = 3.0f;

    [Header("Resurrection Rules")]
    [Tooltip("부활 지점. 비어있을 경우 월드 좌표 (0,0,0)에서 부활합니다.")]
    [SerializeField] private Transform _respawnPoint;
    
    [Tooltip("부활 후 무적 유지 시간(초).")]
    [SerializeField] private float _invulnDurationAfterResurrect = 1.2f;

    // ── 카르마 알림 텍스트 ──────────────────────────────────────────────────

    [Header("Karma Notification")]
    [SerializeField] private TextMeshProUGUI _karmaNotifyText;
    [SerializeField] private float           _karmaTextDuration = 2f;

    // ── Phase 2/3 용 참조 (이후 Step에서 연결) ────────────────────────────

    // ── Phase 3 ────────────────────────────────────────────────────────────

    [Header("Phase 3 — Saturation / Film Grain / Recovery")]
    [Tooltip("채도 드롭 최솟값 (-100~0). 기본 -80.")]
    [SerializeField] private float _saturationDrop = -80f;

    [Tooltip("필름 그레인 최대 강도 (0~1).")]
    [SerializeField] private float _maxFilmGrain = 0.55f;

    [Tooltip("부활 모션 트레일 풀 (Player에 부착).")]
    [SerializeField] private ResurrectionTrailPool _trailPool;

    // ── 캐시 ─────────────────────────────────────────────────────────────

    private ChromaticAberration _chromaticAberration;
    private Vignette            _vignette;
    private ColorAdjustments    _colorAdjustments;
    private FilmGrain           _filmGrain;
    private bool                _cutsceneActive = false;

    // ── 생명주기 ──────────────────────────────────────────────────────────

    private void Awake()
    {
        // 1. 인스펙터 할당이 누락된 경우 플레이어(LivingEntity) 오브젝트에서 자동으로 탐색
        if (_livingEntity != null)
        {
            if (_weaponController == null) _weaponController = _livingEntity.GetComponent<PlayerWeaponController>();
            if (_stateMachine     == null) _stateMachine     = _livingEntity.GetComponent<PlayerStateMachine>();
            if (_playerInput      == null) _playerInput      = _livingEntity.GetComponent<PlayerMovement>();
            if (_playerDeathEffect == null) _playerDeathEffect = _livingEntity.GetComponent<PlayerDeathEffect>();
            if (_trailPool        == null) _trailPool        = _livingEntity.GetComponent<ResurrectionTrailPool>();
        }

        // 2. Volume에서 포스트FX 컴포넌트 미리 캐싱
        if (_fxVolume != null)
        {
            _fxVolume.profile.TryGet(out _chromaticAberration);
            _fxVolume.profile.TryGet(out _vignette);
            _fxVolume.profile.TryGet(out _colorAdjustments);
            _fxVolume.profile.TryGet(out _filmGrain);

            // 평상시에는 weight=0으로 비활성 (2회차 부활에서 잔존 효과 방지)
            _fxVolume.weight = 0f;
        }
    }

    private void OnEnable()
    {
        if (_playerDeathEffect != null)
            _playerDeathEffect.OnDissolveComplete += HandleDissolveComplete;
    }

    private void OnDisable()
    {
        if (_playerDeathEffect != null)
            _playerDeathEffect.OnDissolveComplete -= HandleDissolveComplete;

        if (_deathScreenUI != null)
            _deathScreenUI.OnResurrectClicked -= HandleResurrectClicked;
    }

    // ── 이벤트 핸들러 ────────────────────────────────────────────────────

    private void HandleDissolveComplete()
    {
        if (_cutsceneActive) return;
        _cutsceneActive = true;

        Time.timeScale = 0f;

        if (_playerInput != null) _playerInput.enabled = false;

        if (_deathScreenUI != null)
        {
            _deathScreenUI.OnResurrectClicked += HandleResurrectClicked;
            _deathScreenUI.Show();
        }
    }

    private void HandleResurrectClicked()
    {
        if (_deathScreenUI != null)
        {
            _deathScreenUI.OnResurrectClicked -= HandleResurrectClicked;
            _deathScreenUI.Hide();
        }

        // ── 부활 좌표 이동 (Teleport) ───────────────────────────────
        Vector3 targetPos = (_respawnPoint != null) ? _respawnPoint.position : Vector3.zero;
        
        if (_livingEntity != null)
        {
            // 플레이어 본체 이동
            Vector3 oldPos = _livingEntity.transform.position;
            _livingEntity.transform.position = targetPos;
            
            // ── 카메라 강제 워프 (Teleport Bug 수정) ─────────────────
            // Cinemachine 카메라가 즉시 따라오도록 위치 변경 통보
            CinemachineCore.OnTargetObjectWarped(_livingEntity.transform, targetPos - oldPos);

            // 무적 상태 즉시 활성화 (부활 연출 중 데미지 방지)
            if (_livingEntity.Health != null)
                _livingEntity.Health.IsInvulnerable = true;
        }

        // ── 무기 상태 즉시 초기화 ──────────────────────────────
        _weaponController?.ResetState();

        StartCoroutine(ResurrectSequence());
    }

    // ── 부활 시퀀스 ──────────────────────────────────────────────────────

    private IEnumerator ResurrectSequence()
    {
        // FX Volume 활성화 + 이전 사이클 잔존 효과 초기화
        if (_fxVolume != null) _fxVolume.weight = 1f;
        SetInvertIntensity(0f);
        SetRedAfterimageIntensity(0f);
        SetChromaticAberration(0f);
        SetVignetteIntensity(0f);
        SetSaturation(0f);
        SetFilmGrain(0f);

        // ── Phase 0: 카메라 이동 및 안정화 ────────────────────────
        // 이미 HandleResurrectClicked에서 Warp를 호출했으므로 
        // 물리적인 이동 대기 대신 아주 짧은 안정화 시간만 갖습니다.
        yield return new WaitForSecondsDiscardingTimeScale(0.1f);

        yield return StartCoroutine(Phase1Shock(_phase1Duration));
        yield return StartCoroutine(Phase2ReverseDissolve(_phase2Duration));
        yield return StartCoroutine(Phase3Recovery(_phase3Duration));
        OnCutsceneEnd();
    }

    // ── Phase 1: 충격 ─────────────────────────────────────────────────────

    protected virtual IEnumerator Phase1Shock(float duration)
    {
        // 1. 카메라 진동 (timeScale=0에서도 동작: unscaledTime 기반 Impulse)
        _impulseSource?.GenerateImpulse(_impulseForce);

        float elapsed = 0f;
        float halfInvert = _invertHoldDuration * 0.5f;   // 페이드인/아웃 절반씩
        float caRampDur  = duration - _invertHoldDuration; // CA가 진행되는 구간

        // 2. 색상 반전 페이드인
        while (elapsed < halfInvert)
        {
            elapsed += Time.unscaledDeltaTime;
            SetInvertIntensity(Mathf.Clamp01(elapsed / halfInvert));
            yield return null;
        }

        // 3. 반전 유지 + CA 동시 진행
        elapsed = 0f;
        while (elapsed < _invertHoldDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // 4. 반전 페이드아웃 + CA 벨 곡선 (sin)
        elapsed = 0f;
        while (elapsed < caRampDur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / caRampDur);

            // 반전: 서서히 사라짐
            float invertT = 1f - Mathf.Clamp01(elapsed / halfInvert);
            SetInvertIntensity(invertT);

            // CA: 사인 곡선 — 올라갔다 내려옴
            float caT = Mathf.Sin(t * Mathf.PI);
            SetChromaticAberration(_maxChromaticAberration * caT);

            yield return null;
        }

        // 정리
        SetInvertIntensity(0f);
        SetChromaticAberration(0f);
    }

    // ── Phase 2: 역용해 + 붉은 비네트 ────────────────────────────────────

    protected virtual IEnumerator Phase2ReverseDissolve(float duration)
    {
        // 역용해와 비네트를 병렬 실행
        Coroutine dissolveCoroutine = null;
        if (_reverseDissolveCtrl != null)
            dissolveCoroutine = StartCoroutine(_reverseDissolveCtrl.PlayReverseDissolve(duration));

        // 비네트: 사인 곡선으로 올라갔다 내려옴 (붉은색, mid-phase에서 피크)
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetVignetteIntensity(_maxVignetteIntensity * Mathf.Sin(t * Mathf.PI));
            yield return null;
        }

        SetVignetteIntensity(0f);

        // 역용해가 아직 안 끝났으면 대기 (보통 동시에 끝남)
        if (dissolveCoroutine != null)
            yield return dissolveCoroutine;
    }

    // ── Phase 3: 채도 드롭 + 필름 그레인 + Volume 회복 ──────────────────

    protected virtual IEnumerator Phase3Recovery(float duration)
    {
        // 시작: 채도 드롭 + 필름 그레인 + 화면 전체 붉은 잔상 최대
        SetSaturation(_saturationDrop);
        SetFilmGrain(_maxFilmGrain);
        SetRedAfterimageIntensity(1f);

        // 모션 트레일: Phase 3 시작 시는 timeScale=0이므로 OnCutsceneEnd에서 활성화

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);  // 0→1
            float smoothT = Mathf.SmoothStep(0f, 1f, t);  // 부드러운 S-커브 보간

            // 채도: _saturationDrop → 0 (부드러운 회복)
            SetSaturation(Mathf.Lerp(_saturationDrop, 0f, smoothT));

            // 필름 그레인: 최대 → 0 (부드러운 회복)
            SetFilmGrain(Mathf.Lerp(_maxFilmGrain, 0f, smoothT));
            
            // 붉은 잔상 & 일렁임: 1 → 0 (부드러운 감쇠)
            SetRedAfterimageIntensity(1f - smoothT);

            // FX Volume weight: 1→0 (모든 남은 이펙트 클린업)
            if (_fxVolume != null)
                _fxVolume.weight = 1f - smoothT;

            yield return null;
        }

        // 완전 초기화
        SetSaturation(0f);
        SetFilmGrain(0f);
        if (_fxVolume != null) _fxVolume.weight = 0f;

        // 트레일은 자체 activeDuration 타이머로 자동 종료됨
    }

    // ── 컷신 종료 ─────────────────────────────────────────────────────────

    private void OnCutsceneEnd()
    {
        // HP 전체 회복 + IsAlive 복구
        _livingEntity?.Health.Resurrect();

        // PlayerDeathEffect 상태 리셋
        _playerDeathEffect?.ResetForResurrection();

        _karmaHandler?.AddKarma(1);

        if (_karmaNotifyText != null)
            StartCoroutine(ShowKarmaText());

        Time.timeScale = 1f;
        
        // ── 조작 복구 핵심 ──────────────────────────────────────
        // 1. FSM 상태를 Idle로 강제 전환 (DeathState에서 빠져나옴)
        if (_stateMachine != null)
        {
            _stateMachine.TransitionTo(_stateMachine.Idle);
        }

        // 2. 입력 컴포넌트들 명시적 재활성화 (FSM 전환 시 기본적으로 켜지나 이중 보장)
        if (_playerInput != null) _playerInput.enabled = true;
        if (_weaponController != null) _weaponController.enabled = true;

        // 트레일 활성화
        _trailPool?.Activate();

        // ── 무적 시간 유지 후 해제 ──────────────────────────────────
        if (_livingEntity != null && _livingEntity.Health != null)
        {
            StartCoroutine(ReleaseInvulnerability(_invulnDurationAfterResurrect));
        }

        // 모든 전체화면 효과 강제 클린업
        SetRedAfterimageIntensity(0f);
        SetInvertIntensity(0f);

        _cutsceneActive = false;
    }

    private IEnumerator ReleaseInvulnerability(float duration)
    {
        // timeScale=1 상태에서 흐름
        yield return new WaitForSeconds(duration);
        if (_livingEntity != null && _livingEntity.Health != null)
        {
            _livingEntity.Health.IsInvulnerable = false;
        }
    }

    // ── 카르마 텍스트 ─────────────────────────────────────────────────────

    private IEnumerator ShowKarmaText()
    {
        // KarmaNotifyText는 DeathScreenCanvas의 자식
        // Hide()에서 rootCanvasGroup.alpha가 0으로 리셋되어 있으므로 다시 1로 복구하되,
        // 부활 버튼과 "Dead" 텍스트는 보이지 않아야 함
        if (_deathScreenUI != null)
        {
            _deathScreenUI.gameObject.SetActive(true);
            _deathScreenUI.SetRootAlpha(1f);
            
            // 버튼과 텍스트 숨기기 (사용자 요청: 버튼은 누르는 순간 꺼져야 하며 카르마 텍스트 시점에 보이면 안 됨)
            _deathScreenUI.HideResurrectionElements();
        }

        _karmaNotifyText.gameObject.SetActive(true);
        _karmaNotifyText.text = "카르마가 쌓인것 같다";

        float fadeDur = 0.3f;
        float elapsed = 0f;
        while (elapsed < fadeDur)
        {
            elapsed += Time.unscaledDeltaTime;
            SetTMPAlpha(_karmaNotifyText, Mathf.Clamp01(elapsed / fadeDur));
            yield return null;
        }
        SetTMPAlpha(_karmaNotifyText, 1f);

        elapsed = 0f;
        while (elapsed < _karmaTextDuration) { elapsed += Time.unscaledDeltaTime; yield return null; }

        elapsed = 0f;
        while (elapsed < fadeDur)
        {
            elapsed += Time.unscaledDeltaTime;
            SetTMPAlpha(_karmaNotifyText, 1f - Mathf.Clamp01(elapsed / fadeDur));
            yield return null;
        }

        _karmaNotifyText.gameObject.SetActive(false);
        if (_deathScreenUI != null) _deathScreenUI.gameObject.SetActive(false);
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────────────

    private static void SetInvertIntensity(float v)
    {
        if (ColorInvertRendererFeature.Instance != null)
            ColorInvertRendererFeature.Instance.Intensity = v;
    }

    private static void SetRedAfterimageIntensity(float v)
    {
        if (RedScreenAfterimageRendererFeature.Instance != null)
            RedScreenAfterimageRendererFeature.Instance.Intensity = v;
    }

    private void SetChromaticAberration(float v)
    {
        if (_chromaticAberration != null)
            _chromaticAberration.intensity.Override(v);
    }

    private void SetVignetteIntensity(float v)
    {
        if (_vignette != null)
            _vignette.intensity.Override(v);
    }

    private void SetSaturation(float v)
    {
        if (_colorAdjustments != null)
            _colorAdjustments.saturation.Override(v);
    }

    private void SetFilmGrain(float v)
    {
        if (_filmGrain != null)
            _filmGrain.intensity.Override(v);
    }

    private static void SetTMPAlpha(TextMeshProUGUI tmp, float a)
    {
        Color c = tmp.color;
        c.a = a;
        tmp.color = c;
    }

    // unscaledDeltaTime을 사용하는 간단한 유틸리티 (WaitForSecondsRealtime 대응)
    private class WaitForSecondsDiscardingTimeScale : IEnumerator
    {
        private float _seconds;
        public WaitForSecondsDiscardingTimeScale(float seconds) => _seconds = seconds;
        public object Current => null;
        public bool MoveNext() => (_seconds -= Time.unscaledDeltaTime) > 0;
        public void Reset() { }
    }
}
