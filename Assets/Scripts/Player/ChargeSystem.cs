using UnityEngine;

/// <summary>
/// 우클릭(Mouse1) 홀드 차징 시스템.
///
/// [동작 흐름]
/// 1. 우클릭 꾹 누르면 차징 시작
/// 2. 차징 중 속성 게이지가 비선형 속도로 감소:
///    - 게이지 200~300 구간: 25/초 (100을 4초에 소모)
///    - 게이지 100~200 구간: 33.33/초 (100을 3초에 소모)
///    - 게이지 0~100 구간: 50/초 (100을 2초에 소모)
/// 3. 차징 중 이동속도 30% 감소
/// 4. 우클릭 릴리즈 시 누적 소모량에 따라 차징 단계 판정:
///    - 소모량 ≥ 100 → 1단계
///    - 소모량 ≥ 200 → 2단계
///    - 소모량 ≥ 300 → 3단계
///    - 소모량 < 100 → 0단계 (차징 실패)
/// 5. 게이지가 0이 되면 자동 종료
///
/// [차단 조건]
/// - 무기 미장착
/// - 공격 중
/// - 대시 중 (차징 중이면 캔슬)
/// - 게이지가 0
/// - InventoryToggle 패널 열림
/// </summary>
public class ChargeSystem : MonoBehaviour
{
    // ── 참조 ────────────────────────────────────────────────
    private ElementalWeaponSystem  _elementSystem;
    private PlayerWeaponController _weaponController;
    private PlayerMovement         _playerMovement;
    private ChargeVFXController    _chargeVFX;

    // ── 차징 상태 ────────────────────────────────────────────
    private bool  _isCharging;
    private float _totalConsumed;      // 이번 차징에서 누적 소모량
    private float _chargeStartTime;

    /// <summary>현재 차징 중인지 여부. 외부에서 읽을 수 있습니다.</summary>
    public bool IsCharging => _isCharging;

    // ── 게이지 소모 속도 테이블 ──────────────────────────────
    // 현재 게이지 값에 따라 소모 속도가 다릅니다.
    // gauge 200~300 → 25/s, 100~200 → 33.33/s, 0~100 → 50/s
    private float GetDrainRate(float currentGauge)
    {
        if (currentGauge > 200f)  return 25f;       // 100 / 4초
        if (currentGauge > 100f)  return 33.333f;   // 100 / 3초
        return 50f;                                  // 100 / 2초
    }

    // ── 이동속도 감소 상수 ──────────────────────────────────
    private const float CHARGE_SPEED_MULTIPLIER = 0.7f; // 30% 감소

    // ── 초기화 ──────────────────────────────────────────────

    private void Start()
    {
        _elementSystem   = GetComponent<ElementalWeaponSystem>();
        _weaponController = GetComponent<PlayerWeaponController>();
        _playerMovement  = GetComponent<PlayerMovement>();
        _chargeVFX       = GetComponent<ChargeVFXController>();

        // 찾지 못하면 씬에서 탐색
        if (_elementSystem == null)
            _elementSystem = FindObjectOfType<ElementalWeaponSystem>();
        if (_weaponController == null)
            _weaponController = FindObjectOfType<PlayerWeaponController>();
        if (_playerMovement == null)
            _playerMovement = FindObjectOfType<PlayerMovement>();

        // ChargeVFXController가 없으면 동적 추가
        if (_chargeVFX == null)
            _chargeVFX = gameObject.AddComponent<ChargeVFXController>();
    }

    // ── 메인 루프 ────────────────────────────────────────────

    private void Update()
    {
        // 패널이 열려있으면 차단
        if (InventoryToggle.Instance != null && InventoryToggle.Instance.IsAnyPanelOpen())
        {
            if (_isCharging) CancelCharge();
            return;
        }

        // P 키: 게이지 즉시 300 만충 (테스트용)
        if (Input.GetKeyDown(KeyCode.P))
        {
            if (_elementSystem != null)
            {
                _elementSystem.SetGaugeToMax();
                Debug.Log("[ChargeSystem] P키 — 게이지 300 만충");
            }
        }

        if (_isCharging)
        {
            UpdateCharging();
        }
        else
        {
            TryStartCharge();
        }
    }

    // ── 차징 시작 시도 ──────────────────────────────────────

    private void TryStartCharge()
    {
        if (!Input.GetMouseButtonDown(1)) return;

        // 차단 조건들
        if (_weaponController == null || _weaponController.ActiveBehaviour == null)
            return;
        if (_weaponController.IsAttacking)
            return;
        if (_elementSystem == null || _elementSystem.CurrentGauge <= 0f)
            return;

        // UI 위에서 클릭 무시
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            return;

        // 차징 시작
        _isCharging     = true;
        _totalConsumed  = 0f;
        _chargeStartTime = Time.time;

        // 이동속도 감소
        if (_playerMovement != null)
            _playerMovement.SpeedMultiplier = CHARGE_SPEED_MULTIPLIER;

        // VFX 시작
        if (_chargeVFX != null && _elementSystem != null)
            _chargeVFX.StartCharge(_elementSystem.GetCurrentAuraColor());

        Debug.Log("[ChargeSystem] 차징 시작");
    }

    // ── 차징 진행 중 ────────────────────────────────────────

    private void UpdateCharging()
    {
        // 우클릭 릴리즈 → 차징 종료
        if (Input.GetMouseButtonUp(1))
        {
            EndCharge();
            return;
        }

        // 차징 중에는 게이지 자동 감소(패시브) 타이머를 초기화하여,
        // 차징이 끝난 시점부터 다시 7초(GAUGE_DECAY_DELAY)를 세도록 연장합니다.
        if (_elementSystem != null)
        {
            _elementSystem.ResetDecayTimer();
        }

        // 대시 중이면 캔슬
        var stateMachine = GetComponent<PlayerStateMachine>();
        if (stateMachine != null && stateMachine.IsInState<DashState>())
        {
            CancelCharge();
            return;
        }

        // 게이지 소모
        float currentGauge = _elementSystem.CurrentGauge;
        if (currentGauge <= 0f)
        {
            // 게이지가 0이어도 1단계 이상 도달했으면 차징 강제 종료 안 함 (유지)
            if (GetChargeLevel(_totalConsumed) < 1)
            {
                EndCharge();
                return;
            }
        }
        else
        {
            float drainRate = GetDrainRate(currentGauge);
            float drainAmount = drainRate * Time.deltaTime;
            float actualConsumed = _elementSystem.ConsumeGauge(drainAmount);
            _totalConsumed += actualConsumed;
        }

        // VFX 진행도 업데이트 (소모량 0~300 → 0~1)
        float vfxProgress = Mathf.Clamp01(_totalConsumed / 300f);
        if (_chargeVFX != null)
            _chargeVFX.UpdateProgress(vfxProgress);
    }

    // ── 차징 종료 (릴리즈 또는 게이지 소진) ──────────────────

    private void EndCharge()
    {
        if (!_isCharging) return;

        int chargeLevel = GetChargeLevel(_totalConsumed);

        Debug.Log($"[ChargeSystem] 차징 종료 — 단계: {chargeLevel}, 소모량: {_totalConsumed:F1}, 소요시간: {Time.time - _chargeStartTime:F2}초");

        CleanupCharge(chargeLevel >= 1);

        // ── 차징 스킬 발동 ──
        if (chargeLevel >= 1)
        {
            ExecuteChargeSkill(chargeLevel);
        }
    }

    // ── 차징 캔슬 (대시 등) ──────────────────────────────────

    private void CancelCharge()
    {
        if (!_isCharging) return;

        Debug.Log($"[ChargeSystem] 차징 캔슬 — 소모량: {_totalConsumed:F1}");

        CleanupCharge(false);
    }

    // ── 공통 정리 ────────────────────────────────────────────

    private void CleanupCharge(bool isBurst)
    {
        _isCharging = false;

        // 이동속도 복원
        if (_playerMovement != null)
            _playerMovement.SpeedMultiplier = 1f;

        // VFX 종료
        if (_chargeVFX != null)
            _chargeVFX.StopCharge(isBurst);
    }

    // ── 차징 스킬 실행 ──────────────────────────────────────

    private void ExecuteChargeSkill(int chargeLevel)
    {
        if (_weaponController == null || _weaponController.ActiveBehaviour == null)
            return;

        WeaponType weaponType = _weaponController.ActiveBehaviour.WeaponType;
        IChargeSkill skill = ChargeSkillFactory.GetSkill(weaponType, chargeLevel);

        if (skill == null)
        {
            Debug.LogWarning($"[ChargeSystem] 스킬을 찾을 수 없음: {weaponType} Lv{chargeLevel}");
            return;
        }

        ChargeSkillContext context = BuildContext(chargeLevel, weaponType);
        skill.Execute(context);

        Debug.Log($"[ChargeSystem] 스킬 발동: {weaponType} {chargeLevel}단계");
    }

    private ChargeSkillContext BuildContext(int chargeLevel, WeaponType weaponType)
    {
        PlayerEntity playerEntity = GetComponent<PlayerEntity>();
        StatSystem statSystem = GetComponent<StatSystem>();
        Camera mainCamera = Camera.main;

        // 커서 방향 계산
        Vector2 cursorDir = Vector2.right;
        if (mainCamera != null)
        {
            float camZ = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
            Vector3 screenPos = Input.mousePosition;
            screenPos.z = camZ;
            Vector3 worldPos = mainCamera.ScreenToWorldPoint(screenPos);
            Vector2 dir = (Vector2)worldPos - (Vector2)transform.position;
            if (dir.sqrMagnitude > 0.001f)
                cursorDir = dir.normalized;
        }

        // 일반 공격 1타 기준 데미지 계산
        float totalAtk = playerEntity != null ? playerEntity.TotalAtk : 0f;
        float baseDamage = DamageCalculator.CalcOutgoingDamage(totalAtk, 0f);

        return new ChargeSkillContext
        {
            ChargeLevel      = chargeLevel,
            WeaponType       = weaponType,
            PlayerEntity     = playerEntity,
            WeaponBehaviour  = _weaponController.ActiveBehaviour,
            CursorDirection  = cursorDir,
            PlayerTransform  = transform,
            ElementSystem    = _elementSystem,
            BaseDamage       = baseDamage,
            WeaponController = _weaponController,
            PlayerMovement   = _playerMovement,
            StatSystem       = statSystem,
            MainCamera       = mainCamera
        };
    }

    // ── 차징 단계 판정 ──────────────────────────────────────

    /// <summary>
    /// 누적 소모량으로 차징 단계를 판정합니다.
    /// 0 = 실패, 1 = 1단계, 2 = 2단계, 3 = 3단계 (만충)
    /// </summary>
    private int GetChargeLevel(float consumed)
    {
        if (consumed >= 299.5f) return 3;
        if (consumed >= 199.5f) return 2;
        if (consumed >= 99.5f) return 1;
        return 0;
    }
}
