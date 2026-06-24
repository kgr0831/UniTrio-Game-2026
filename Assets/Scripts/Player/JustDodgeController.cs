using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;

/// <summary>
/// 회피 저스트(위치타임/퍼펙트 닷지 카운터)의 총괄 컨트롤러. 플레이어 GameObject에 부착됩니다.
///
/// 흐름:
/// 1. 대시 무적 윈도우(<see cref="DashHandler.IsInJustDodgeWindow"/>) 중 적 공격이 닿으면
///    <see cref="PlayerEntity.TakeDamage"/>가 <see cref="TryConsumeDodge"/>를 호출 → 발동.
/// 2. Phase 1: 슬로우모션(timeScale↓) + 화면 흑백(GrayscaleRendererFeature) + 피한 지점 반짝임 + 플레이어 글로우.
///    실시간 _fInputWindow 초 동안 F 입력 대기.
/// 3. F 입력 시 Phase 2: timeScale 복원 + MonsterFreezeManager.Freeze()(몬스터만 정지) +
///    적 뒤로 충돌무시 직선 돌진(대시 잔상 재사용) + 무기 스윙 연출 + 발동시킨 적에게 기본공격×배수 직접 데미지.
/// 4. F 미입력(윈도우 경과): 슬로우·흑백 해제하고 일반 복귀(카운터 없음).
/// </summary>
public class JustDodgeController : MonoBehaviour
{
    [Header("Trigger")]
    [Tooltip("회피 저스트 카운터 발동 키")]
    [SerializeField] private KeyCode _fKey = KeyCode.F;
    [Tooltip("예측 발동: 대시 윈도우 중 이 반경 내에 '공격 중'인 적이 있으면 공격이 빗나가도 발동합니다(일찍 피해도 OK).")]
    [SerializeField] private float _predictRange = 2f;

    [Header("Phase 1 — 슬로우모션 / 흑백 / F 대기")]
    [Tooltip("슬로우모션 timeScale (낮을수록 느림)")]
    [SerializeField] private float _slowTimeScale   = 0.08f;
    [Tooltip("F 입력 대기 시간 = 슬로우모션 길이 (실시간 초). 인벤토리가 열려 있으면 이 타이머는 멈춥니다.")]
    [SerializeField] private float _fInputWindow     = 1.5f;
    [Tooltip("흑백이 플레이어 중앙에서 화면 전체로 퍼지는 시간 (실시간 초). 이 시간 동안 슬로우도 함께 걸림")]
    [SerializeField] private float _grayscaleRampIn  = 0.85f;
    [Tooltip("종료 시 흑백·시간배속이 원복되는 시간 (실시간 초)")]
    [SerializeField] private float _grayscaleRampOut = 0.25f;
    [Tooltip("흑백이 퍼지는 최대 반경(스크린 UV 기준). 화면 전체를 덮으려면 1.4 이상")]
    [SerializeField] private float _grayscaleMaxRadius = 1.1f;

    [Header("Phase 2 — 돌진 / 카운터")]
    [Tooltip("적 기준 뒤쪽으로 잡을 목표 거리 (유닛). 적 스프라이트와 겹치지 않게 충분히 크게.")]
    [SerializeField] private float _rushDistanceBehind = 4.5f;
    [Tooltip("적 뒤까지 직선 돌진하는 시간 (초)")]
    [SerializeField] private float _rushDuration        = 0.12f;
    [Tooltip("카운터 데미지 배수 (기본공격 대비)")]
    [SerializeField] private float _counterMultiplier   = 3f;
    [Tooltip("카운터 무기 스윙이 보이도록 하는 여운 시간 (실시간 초)")]
    [SerializeField] private float _counterHold         = 0.45f;
    [Tooltip("회피 저스트 성공 시 대시를 평소보다 몇 배 길게 이어갈지")]
    [SerializeField] private float _dashLengthMultiplier = 3f;
    [Tooltip("회피 카운터 성공 시 원소 게이지를 추가 충전하는 양 (평소 근접타격≈30 대비 크게)")]
    [SerializeField] private float _counterGaugeGain = 150f;

    [Header("VFX")]
    [SerializeField] private Color _sparkleColor    = new Color(0.65f, 0.97f, 1f, 1f);
    [SerializeField] private float _sparkleScale    = 2.6f;
    [SerializeField] private float _sparkleDuration = 0.5f;
    [SerializeField] private Color _glowColor       = new Color(0.6f, 0.95f, 1f, 1f);
    [SerializeField] private float _glowScale       = 3.2f;
    [SerializeField] private float _glowYOffset     = 0.7f;
    [Tooltip("발동 순간 카메라 흔들림 세기")]
    [SerializeField] private float _activationShake = 0.35f;
    [Tooltip("카운터 타격 순간 카메라 흔들림 세기")]
    [SerializeField] private float _counterShake    = 0.55f;
    [Tooltip("카운터 임팩트(슬래시+버스트) 색/크기")]
    [SerializeField] private Color _impactColor     = new Color(0.85f, 0.97f, 1f, 1f);
    [SerializeField] private float _impactScale     = 3.2f;

    [Header("Focus (적 강조 / 카메라)")]
    [Tooltip("발동 적의 붉은 아웃라인 색")]
    [SerializeField] private Color _outlineColor = new Color(1f, 0.12f, 0.12f, 1f);
    [Tooltip("아웃라인(외곽선) 스케일 — 1.0이면 적 실루엣 가장자리에 딱 맞음")]
    [SerializeField] private float _outlineScale = 1.0f;
    [Tooltip("흑백 중에도 적 주변을 컬러로 유지하는 반경(스크린 UV). 0이면 비활성(적은 회색, 외곽선만 컬러).")]
    [SerializeField] private float _focusColorRadius = 0f;
    [Tooltip("카메라를 적 쪽으로 살짝 팬하는 양(유닛)")]
    [SerializeField] private float _camPanAmount = 2.2f;
    [Tooltip("카메라 줌인 배율(FollowOffset.z에 곱함, <1=줌인). 약하게=0.85~0.9")]
    [SerializeField] private float _camZoomFactor = 0.95f;

    // ── 참조 ──────────────────────────────────────────────────────
    private DashHandler           _dash;
    private PlayerStateMachine    _machine;
    private PlayerEntity          _entity;
    private PlayerWeaponController _weaponCtrl;
    private DashAfterimagePool    _afterimage;
    private PlayerMovement        _movement;
    private Animator              _animator;
    private Rigidbody2D           _rb;
    private SpriteRenderer        _sprite;

    private SpriteRenderer _glow;
    private JustDodgeUI    _ui;
    private Camera         _cam;

    // 카메라 포커스 / 아웃라인
    private CinemachineCamera _vcam;
    private CinemachineFollow _camFollow;
    private Vector3           _camOrigOffset;
    private Vector3           _camFocusOffset;
    private bool              _camActive;
    private JustDodgeOutline  _outline;
    private SpriteRenderer    _enemyBodySr;   // 포커스(컬러 유지) 중심 추적용 적 몸 렌더러
    private Vector2           _dashStartPos;  // 대시 윈도우 진입 시점 위치 (막판 회피 판정용)
    private bool              _wasInDodgeWindow;
    private Camera            _outlineOverlayCam; // 빨간 외곽선을 흑백 위에 그리는 오버레이 카메라

    // 상태
    private bool _active;             // 시퀀스 진행 중
    private bool _triggeredThisDash;  // 한 번의 대시에서 중복 발동 방지

    private void Awake()
    {
        _dash       = GetComponent<DashHandler>();
        _machine    = GetComponent<PlayerStateMachine>();
        _entity     = GetComponent<PlayerEntity>();
        _weaponCtrl = GetComponent<PlayerWeaponController>();
        _afterimage = GetComponent<DashAfterimagePool>();
        _movement   = GetComponent<PlayerMovement>();
        _animator   = GetComponent<Animator>();
        _rb         = GetComponent<Rigidbody2D>();
        _sprite     = GetComponent<SpriteRenderer>();
        _ui         = JustDodgeUI.GetOrCreate();
        _cam        = Camera.main;
    }

    private bool HasWeapon() => _weaponCtrl != null && _weaponCtrl.ActiveBehaviour != null;

    private void Update()
    {
        // 대시가 끝나고 시퀀스도 아니면 다음 대시를 위해 가드 해제
        if (_triggeredThisDash && !_active && (_dash == null || !_dash.IsDashing))
            _triggeredThisDash = false;

        // 대시 윈도우 진입 순간의 플레이어 위치 기록 (대시로 멀어진 뒤에도 '적과 가까웠는지' 판정)
        bool inWindow = _dash != null && _dash.IsInJustDodgeWindow;
        if (inWindow && !_wasInDodgeWindow) _dashStartPos = transform.position;
        _wasInDodgeWindow = inWindow;

        // ★ 예측 발동: 대시 윈도우 중, (대시 시작 위치 기준) 근처에서 '방금 타격한' 적이 있으면 발동.
        //   → 적의 타격(스트라이크) 순간과 대시가 겹쳐야 하므로 막판 회피에 보상된다(너무 이른 회피는 X).
        if (!_active && !_triggeredThisDash && inWindow)
        {
            GameObject attacker = FindNearbyAttackingEnemy();
            if (attacker != null)
                BeginDodge(attacker);
        }
    }

    private GameObject FindNearbyAttackingEnemy()
    {
        var handlers = Object.FindObjectsByType<MonsterAttackHandler>(FindObjectsSortMode.None);
        GameObject best = null;
        float bestSqr = _predictRange * _predictRange;
        Vector2 p = _dashStartPos;
        foreach (var h in handlers)
        {
            if (h == null || !h.JustStruckRecently) continue;
            float d = ((Vector2)h.transform.position - p).sqrMagnitude;
            if (d <= bestSqr) { bestSqr = d; best = h.gameObject; }
        }
        return best;
    }

    private void BeginDodge(GameObject attacker)
    {
        _triggeredThisDash = true;
        StartCoroutine(JustDodgeRoutine(attacker));
    }

    /// <summary>
    /// 회피 저스트 발동 시도. PlayerEntity.TakeDamage에서, 대시 무적으로 막힐 적 공격이
    /// 닿는 순간 호출됩니다.
    /// </summary>
    /// <param name="attacker">공격을 가한 몬스터(또는 투사체 주인).</param>
    /// <returns>발동에 성공하면 true (호출 측은 해당 피해를 무시).</returns>
    public bool TryConsumeDodge(GameObject attacker)
    {
        if (_dash == null || _machine == null) return false;
        if (_active || _triggeredThisDash) return false;
        if (!_dash.IsInJustDodgeWindow) return false;

        BeginDodge(attacker);
        return true;
    }

    private IEnumerator JustDodgeRoutine(GameObject attacker)
    {
        _active = true;
        try
        {
            // FSM 주차 (DashState.Exit → JustDodgeState.Enter : 이동/공격 잠금 + 무적)
            _machine.TransitionTo(_machine.JustDodge);

            // 회피 저스트 성공 → 대시를 평소보다 길게 이어간다 + 대시 잔상 켜기
            _dash?.ExtendDash(_dashLengthMultiplier);
            _afterimage?.StartSpawning();

            // 피한 지점 반짝임 + 플레이어 글로우 + 발동 펀치(카메라 흔들림) + F 프롬프트
            JustDodgeVFX.SpawnSparkle(transform.position, _sparkleColor, _sparkleScale, _sparkleDuration);
            AudioManager.Instance?.PlayJustDodgeSlowdown();
            SetGlow(true);
            CameraShakeController.Instance?.Shake(_activationShake, 0.25f);
            _ui?.ShowPrompt();

            // 적 붉은 아웃라인 강조 + 카메라 포커스(적 쪽으로 약하게 팬 + 줌인) 준비
            BeginFocus(attacker);

            // ── Phase 1: 플레이어 중앙에서 흑백이 퍼지며 슬로우모션 + F 대기 ──
            bool fPressed = false;
            float elapsed = 0f;
            while (elapsed < _fInputWindow)
            {
                // 인벤토리(무기 변경 등)가 열려 있으면 타이머 정지 — 시간이 흐르지 않음
                bool invOpen = InventoryToggle.Instance != null && InventoryToggle.Instance.IsAnyPanelOpen();
                if (!invOpen)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float gk = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, _grayscaleRampIn));

                    // 흑백이 플레이어 중앙에서 퍼지고, 그에 맞춰 timeScale도 1→slow로 느려짐
                    SetGrayscaleRadial(1f, PlayerViewport(), Mathf.Lerp(0f, _grayscaleMaxRadius, gk));
                    SetGrayscaleFocus(); // 적 주변은 컬러 유지 → 빨간 아웃라인이 회색에 안 묻힘
                    SetTimeScale(Mathf.Lerp(1f, _slowTimeScale, gk));
                    PulseGlow(elapsed);

                    // 카메라 포커스: 적 쪽으로 팬 + 줌인 (흑백과 함께 차오름)
                    if (_camActive && _camFollow != null)
                        _camFollow.FollowOffset = Vector3.Lerp(_camOrigOffset, _camFocusOffset, gk);

                    // 대시(잔상)가 끝나면 잔상 정지 — 멈춘 채 잔상이 쌓이지 않도록
                    if (_dash != null && !_dash.IsDashing) _afterimage?.StopSpawning();

                    if (Input.GetKeyDown(_fKey))
                    {
                        // 무기 미착용이면 공격 불가 → 경고만 띄우고 계속 대기(이때 무기 착용 후 다시 F 가능)
                        if (HasWeapon()) { fPressed = true; _ui?.FlashHidePrompt(); break; }
                        else { _ui?.ShowWarning(); }
                    }
                }
                yield return null;
            }

            // ── Phase 2: F 입력 시 카운터 ──
            if (fPressed)
                yield return CounterRoutine(attacker);

            // 흑백·시간배속 부드럽게 원복
            yield return RampOutAndRestore();
        }
        finally
        {
            // 안전망: 어떤 경로(예외 포함)로 끝나도 깨끗하게 복원
            SetTimeScale(1f);
            SetGrayscale(0f);
            ClearGrayscaleFocus();
            MonsterFreezeManager.Unfreeze();
            if (_afterimage != null) _afterimage.StopSpawning();
            SetGlow(false);
            _ui?.HidePromptImmediate();
            EndFocus();
            _active = false;

            // FSM 복귀
            bool moving = _movement != null && _movement.MoveInput.sqrMagnitude > 0.01f;
            _machine.TransitionTo(moving ? (PlayerState)_machine.Walk : _machine.Idle);
        }
    }

    private IEnumerator CounterRoutine(GameObject attacker)
    {
        // 플레이어는 일반 시간으로, 몬스터만 정지
        SetTimeScale(1f);
        MonsterFreezeManager.Freeze();
        SetGrayscaleFull(1f);
        ClearGrayscaleFocus(); // 돌진 중 카메라 이동으로 컬러 섬이 어긋나므로 카운터엔 미적용

        // 타겟 유효성 / 위치
        IDamageable target = (attacker != null) ? attacker.GetComponentInParent<IDamageable>() : null;
        bool targetAlive   = target != null && target.IsAlive;
        Vector3 attackerPos = (attacker != null) ? attacker.transform.position : transform.position;

        // "적 뒤" = 플레이어→적 방향 연장선(적 너머). flipX 규약이 몹마다 달라 안정적인 이 방식 사용.
        Vector3 start = transform.position;
        Vector3 toAttacker = attackerPos - start; toAttacker.z = 0f;
        Vector3 dir = (toAttacker.sqrMagnitude > 0.0001f)
            ? toAttacker.normalized
            : (Vector3)((_movement != null) ? _movement.FacingDirection : Vector2.right);

        Vector3 behind = targetAlive
            ? attackerPos + dir * _rushDistanceBehind
            : start + dir * (_rushDistanceBehind + 1f);
        behind.z = start.z;

        // 바라보기 + 대시 애니 + 잔상
        if (_movement != null) _movement.SetFacingDirection(dir);
        if (_animator != null) _animator.SetBool("IsDashing", true);
        if (_afterimage != null) _afterimage.StartSpawning();

        // 충돌 무시 직선 돌진 (transform 직접 이동)
        if (_rb != null) _rb.linearVelocity = Vector2.zero;
        float t = 0f;
        while (t < _rushDuration)
        {
            t += Time.deltaTime; // timeScale=1
            transform.position = Vector3.Lerp(start, behind, Mathf.Clamp01(t / _rushDuration));
            yield return null;
        }
        transform.position = behind;

        if (_afterimage != null) _afterimage.StopSpawning();
        if (_animator != null) _animator.SetBool("IsDashing", false);

        // 적 방향 + 무기 조준을 적 쪽으로 강제
        Vector3 faceBack = attackerPos - transform.position; faceBack.z = 0f;
        Vector2 aimDir = (faceBack.sqrMagnitude > 0.0001f)
            ? (Vector2)faceBack.normalized
            : ((_movement != null) ? _movement.FacingDirection : Vector2.right);
        if (_movement != null) _movement.SetFacingDirection(aimDir);

        // 실제 무기 공격 수행: WeaponCtrl을 켜서 정상 애니메이션/투사체/정리가 동작하게 하고,
        // 마우스 대신 적 방향으로 조준한다. 데미지는 무기의 실제 타격/투사체가 처리(기본×배수).
        WeaponBehaviourBase beh = (_weaponCtrl != null) ? _weaponCtrl.ActiveBehaviour : null;
        float savedMult = 1f;
        if (_weaponCtrl != null)
        {
            _weaponCtrl.enabled = true;
            _weaponCtrl.SetAimOverride(true, aimDir);
        }

        // 무기 소환 거리 설정(검은 바깥으로 띄움 / 창은 0) — 일반 공격(HandleAttackInput)과 동일.
        // 이걸 안 하면 검이 플레이어 피봇에 붙은 채로 회전한다.
        if (beh != null)
        {
            var floating = beh.GetComponent<FloatingWeaponMotion>();
            if (floating != null)
            {
                float dist = (beh.WeaponType == WeaponType.Sword) ? Mathf.Max(1.0f, 2.5f - 0.8f) : 0f;
                floating.SetBaseLocalPosition(new Vector3(dist, 0f, 0f));
            }
        }

        // 한 프레임 대기 → WeaponCtrl이 무기를 적 방향으로 조준/배치한 뒤 공격 시작
        yield return null;

        // 근접(검/창) vs 원거리(활/지팡이) 분기
        bool melee = (beh != null) && (beh.WeaponType == WeaponType.Sword || beh.WeaponType == WeaponType.Spear);

        // 원거리(투사체) 카운터: 머즐→적으로 정확히 재조준 (몸높이 오프셋으로 경로가 빗나가는 것 보정)
        if (!melee && beh != null && targetAlive && _weaponCtrl != null)
        {
            Vector3 toTgt = attackerPos - beh.MuzzleWorldPosition; toTgt.z = 0f;
            if (toTgt.sqrMagnitude > 0.0001f)
                _weaponCtrl.SetAimOverride(true, ((Vector2)toTgt).normalized);
        }

        if (beh != null)
        {
            savedMult = beh.ChargeDamageMultiplier;
            // 근접: 히트박스 데미지 억제(아래에서 직접 적용) / 원거리: 실제 투사체가 기본×배수 데미지
            beh.ChargeDamageMultiplier = melee ? 0f : _counterMultiplier;
            _weaponCtrl.ApplyAttackStartScale(1); // 검 등 LockRotation 무기 피봇/플립 정렬(회전 깨짐 방지)
            _weaponCtrl.ForceBeginAttack(1);
        }

        // 근접 무기는 적 뒤 먼 거리에서 스윙이 닿지 않으므로 데미지를 직접 보장(기본×배수)
        if (melee && targetAlive)
        {
            float atk = (_entity != null) ? _entity.TotalAtk : 0f;
            target.TakeDamage(DamageCalculator.CalcOutgoingDamage(atk, 0f, _counterMultiplier), gameObject);
            // 직접 데미지는 무기 히트박스를 거치지 않아 피격음이 빠지므로 여기서 재생
            AudioManager.Instance?.PlayHit();
        }

        // 회피 카운터 성공 보너스: 원소 게이지를 평소보다 크게 충전
        if (targetAlive)
            ElementalWeaponSystem.Instance?.AddGauge(_counterGaugeGain);

        // 타격감: 강한 카메라 흔들림 + 화면 섬광 + 적 스프라이트 강한 점멸
        CameraShakeController.Instance?.Shake(_counterShake, 0.3f);
        ScreenFlashEffect.Instance?.Flash(new Color(1f, 1f, 1f, 0.55f), 0.15f);
        if (attacker != null)
            attacker.GetComponentInParent<HealthSystem>()?.OverrideFlashTimer(0.25f);

        // 강력한 공격 임팩트(슬래시 호 + 폭발 버스트)를 적 위치에 생성
        float impactAngle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;
        JustDodgeVFX.SpawnImpact(attackerPos, impactAngle, _impactColor, _impactScale);

        // 공격이 진행·적중하도록 여운 후 배율 원복 + 조준 오버라이드 해제
        yield return WaitUnscaled(_counterHold);

        if (beh != null) beh.ChargeDamageMultiplier = savedMult;
        if (_weaponCtrl != null) _weaponCtrl.SetAimOverride(false, Vector2.right);
    }

    private IEnumerator RampOutAndRestore()
    {
        float fromG  = (GrayscaleRendererFeature.Instance != null) ? GrayscaleRendererFeature.Instance.Intensity : 0f;
        float fromTS = Time.timeScale;
        Vector3 camFrom = (_camActive && _camFollow != null) ? _camFollow.FollowOffset : Vector3.zero;
        float dur = Mathf.Max(0.0001f, _grayscaleRampOut);
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            SetGrayscale(Mathf.Lerp(fromG, 0f, k));
            SetTimeScale(Mathf.Lerp(fromTS, 1f, k));
            if (_camActive && _camFollow != null)
                _camFollow.FollowOffset = Vector3.Lerp(camFrom, _camOrigOffset, k);
            yield return null;
        }
        SetGrayscale(0f);
        SetTimeScale(1f);
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────

    private static void SetTimeScale(float s)
    {
        Time.timeScale = s;
        Time.fixedDeltaTime = 0.02f * Mathf.Max(s, 0.0001f);
    }

    private static void SetGrayscale(float v)
    {
        if (GrayscaleRendererFeature.Instance != null)
            GrayscaleRendererFeature.Instance.Intensity = Mathf.Clamp01(v);
    }

    private void SetGrayscaleRadial(float intensity, Vector2 center, float radius)
    {
        var f = GrayscaleRendererFeature.Instance;
        if (f == null) return;
        f.Intensity = Mathf.Clamp01(intensity);
        f.Center    = center;
        f.Radius    = radius;
    }

    private void SetGrayscaleFull(float intensity)
    {
        var f = GrayscaleRendererFeature.Instance;
        if (f == null) return;
        f.Intensity = Mathf.Clamp01(intensity);
        f.Radius    = 10f; // 화면 전체
    }

    private Vector2 PlayerViewport()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return new Vector2(0.5f, 0.5f);
        Vector3 vp = _cam.WorldToViewportPoint(transform.position + Vector3.up * 0.6f);
        return new Vector2(vp.x, vp.y);
    }

    /// <summary>흑백 중에도 적 주변을 컬러로 유지(빨간 아웃라인이 회색에 묻히지 않게). 매 프레임 적 위치를 추적.</summary>
    private void SetGrayscaleFocus()
    {
        var f = GrayscaleRendererFeature.Instance;
        if (f == null) return;
        if (_enemyBodySr != null && _focusColorRadius > 0f)
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam != null)
            {
                Vector3 vp = _cam.WorldToViewportPoint(_enemyBodySr.bounds.center);
                f.FocusCenter = new Vector2(vp.x, vp.y);
                f.FocusRadius = _focusColorRadius;
                return;
            }
        }
        f.FocusRadius = 0f;
    }

    private void ClearGrayscaleFocus()
    {
        var f = GrayscaleRendererFeature.Instance;
        if (f != null) f.FocusRadius = 0f;
    }

    private void SetGlow(bool on)
    {
        if (on && _glow == null) CreateGlow();
        if (_glow != null) _glow.gameObject.SetActive(on);
    }

    private void CreateGlow()
    {
        var go = new GameObject("JustDodgeGlow");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up * _glowYOffset;

        _glow = go.AddComponent<SpriteRenderer>();
        _glow.sprite = JustDodgeVFX.RadialSprite;
        if (_sprite != null)
        {
            _glow.sortingLayerID = _sprite.sortingLayerID;
            _glow.sortingOrder   = _sprite.sortingOrder - 1; // 플레이어 뒤
        }
        _glow.color = _glowColor;
        _glow.transform.localScale = Vector3.one * _glowScale;
    }

    private void PulseGlow(float t)
    {
        if (_glow == null) return;
        float pulse = 0.5f + 0.5f * Mathf.Sin(t * 18f);
        var c = _glowColor;
        c.a = _glowColor.a * (0.4f + 0.6f * pulse);
        _glow.color = c;
    }

    private static IEnumerator WaitUnscaled(float seconds)
    {
        float t = 0f;
        while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
    }

    // ── 적 강조 / 카메라 포커스 ────────────────────────────────────

    /// <summary>붉은 외곽선을 흑백 화면 위에 컬러로 그리기 위한 오버레이 카메라를 준비한다.
    /// 베이스 카메라는 JustDodgeFX 레이어를 제외하고, 이 오버레이 카메라가 그 레이어만 흑백 없이 렌더한다.</summary>
    private void EnsureOutlineOverlayCamera()
    {
        int fxLayer = LayerMask.NameToLayer("JustDodgeFX");
        if (fxLayer < 0) return;
        int fxMask = 1 << fxLayer;

        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        // 베이스 카메라는 FX 레이어를 그리지 않음 (흑백 패스에 외곽선이 안 섞이게)
        _cam.cullingMask &= ~fxMask;

        if (_outlineOverlayCam != null) return;

        var go = new GameObject("JustDodgeOutlineOverlayCam");
        go.transform.SetParent(_cam.transform, false);
        _outlineOverlayCam = go.AddComponent<Camera>();
        _outlineOverlayCam.CopyFrom(_cam);
        _outlineOverlayCam.cullingMask = fxMask;   // FX 레이어만
        _outlineOverlayCam.clearFlags  = CameraClearFlags.Nothing;
        _outlineOverlayCam.depth       = _cam.depth + 1;

        var data = _outlineOverlayCam.GetUniversalAdditionalCameraData();
        data.renderType = CameraRenderType.Overlay;

        var baseData = _cam.GetUniversalAdditionalCameraData();
        if (!baseData.cameraStack.Contains(_outlineOverlayCam))
            baseData.cameraStack.Add(_outlineOverlayCam);
    }

    private void BeginFocus(GameObject attacker)
    {
        EnsureOutlineOverlayCamera();

        // 적 붉은 아웃라인
        if (attacker != null)
        {
            var esr = attacker.GetComponentInChildren<SpriteRenderer>();
            if (esr != null)
            {
                _enemyBodySr = esr;
                _outline = JustDodgeOutline.Create(esr, _outlineColor, _outlineScale);
            }
        }

        // 카메라 포커스 준비 (Cinemachine FollowOffset 기반)
        if (_vcam == null) _vcam = FindFirstObjectByType<CinemachineCamera>();
        if (_vcam != null && _camFollow == null) _camFollow = _vcam.GetComponent<CinemachineFollow>();
        if (_camFollow != null)
        {
            _camOrigOffset = _camFollow.FollowOffset;
            Vector3 toEnemy = (attacker != null) ? (attacker.transform.position - transform.position) : Vector3.zero;
            toEnemy.z = 0f;
            Vector3 bias = (toEnemy.sqrMagnitude > 0.01f) ? toEnemy.normalized * _camPanAmount : Vector3.zero;
            _camFocusOffset = new Vector3(
                _camOrigOffset.x + bias.x,
                _camOrigOffset.y + bias.y,
                _camOrigOffset.z * _camZoomFactor); // z는 음수 → 배율<1이면 카메라가 가까워짐(줌인)
            _camActive = true;
        }
    }

    private void EndFocus()
    {
        if (_camActive && _camFollow != null) _camFollow.FollowOffset = _camOrigOffset;
        _camActive = false;
        if (_outline != null) { Destroy(_outline.gameObject); _outline = null; }
    }
}
