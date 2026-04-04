using UnityEngine;

/// <summary>
/// 활(Bow) 무기의 공격 로직 및 투사체(화살) 생성을 담당합니다.
/// [좌클릭] 즉시 발사 (기존 동작 유지)
/// [우클릭] 장전(차징) → 놓을 때 발사
///   - 총 차징 시간: _maxChargeTime 초 (기본 2초)
///   - 차징 정도에 따라 애니메이션이 _fireDelayNormalizedTime 까지 진행
///   - 최대 차징 시 발사속도/데미지 최대, 이동속도 최대 둔화(50%), 활 떨림
/// </summary>
public class BowBehaviour : WeaponBehaviourBase
{
    [Header("애니메이터")]
    [Tooltip("활의 스프라이트 애니메이터를 연결하세요.")]
    [SerializeField] private Animator _weaponAnimator;

    [Header("투사체 (화살)")]
    [Tooltip("발사할 화살 프리팹을 연결하세요.")]
    [SerializeField] private GameObject _arrowPrefab;
    [Tooltip("화살이 생성될 위치(ArrowPos) 트랜스폼을 연결하세요.")]
    [SerializeField] private Transform  _arrowPos;

    [Header("발사 설정")]
    [Tooltip("좌클릭(일반 공격) 시 화살이 실제로 발사되는 애니메이션 진행 시점입니다. (0 = 즉시, 1 = 끝)")]
    [SerializeField] [Range(0f, 1f)] private float _fireDelayNormalizedTime = 0.4f;
    [Tooltip("차징 없이(0%) 발사할 때의 최소 화살 속도입니다.")]
    [SerializeField] private float _minArrowSpeed  = 8f;
    [Tooltip("100% 풀 차징 시 화살의 최대 속도입니다.")]
    [SerializeField] private float _maxArrowSpeed  = 20f;
    [Tooltip("차징 없이(0%) 발사할 때의 최소 화살 데미지입니다.")]
    [SerializeField] private int   _minArrowDamage = 5;
    [Tooltip("100% 풀 차징 시 화살의 최대 데미지입니다.")]
    [SerializeField] private int   _maxArrowDamage = 25;

    [Header("차징 설정 (우클릭)")]
    [Tooltip("우클릭을 이 시간(초)만큼 누르고 있으면 100% 풀 차징이 됩니다.")]
    [SerializeField] private float _maxChargeTime  = 1f;
    [Tooltip("100% 풀 차징 시 플레이어 이동속도 배율입니다. (0.5 = 원래 속도의 50%로 둔화)")]
    [SerializeField] [Range(0f, 1f)] private float _maxSlowRatio = 0.5f;

    [Header("최대 차징 떨림 설정")]
    [Tooltip("100% 풀 차징 시 활이 흔들리는 최대 거리(유닛)입니다. 클수록 더 심하게 떨립니다.")]
    [SerializeField] private float _shakeAmplitude = 0.05f;
    [Tooltip("떨림 주기입니다. 값이 높을수록 더 빠르게 떨립니다.")]
    [SerializeField] private float _shakeFrequency = 30f;

    [Header("공전 설정")]
    [Tooltip("캐릭터 중심으로부터 활이 떠있는 거리입니다. 0이면 캐릭터 중심에서 회전합니다.")]
    [SerializeField] private float _orbitRadius  = 0.5f;
    [Tooltip("활이 위/왼쪽을 향할 때 플레이어 뒤로 숨길지 여부입니다. 공전 시에는 보통 꺼두는 게 자연스럽습니다.")]
    [SerializeField] private bool  _useGoBehind  = false;

    [Header("글로우 설정 (Brackeys Glow)")]
    [Tooltip("발사 시점까지 차오를 최대 글로우 강도입니다.")]
    [SerializeField] private float _maxGlowIntensity = 5f;
    [Tooltip("최대 차징 시 맥동하는 강도 범위입니다.")]
    [SerializeField] private float _pulseAmplitude = 1.5f;
    [Tooltip("글로우 색상 (HDR)")]
    [ColorUsage(true, true)]
    [SerializeField] private Color _glowColor = new Color(0.7f, 0f, 1f, 1f); // 보랏빛 기본값


    // ── WeaponBehaviourBase 오버라이드 ───────────────────────────
    public override float PivotRotationOffset      => 0f;
    // 왼쪽을 향할 때 피봇 Y-scale을 -1로 반전해 스프라이트를 mirror 처리.
    // false(순수 회전)이면 180° 뒤집혀 비대칭하게 보여 좌측에서 크기가 달라 보임.
    public override bool  UseYScaleFlip            => true;
    public override float OrbitRadius              => _orbitRadius;
    public override bool  UseGoBehind             => _useGoBehind;
    public override bool  LockRotationDuringAttack => false;
    public override float ComboWindow              => 0f;
    public override int   MaxComboSteps            => 1;

    // ── 내부 상태 ────────────────────────────────────────────────
    private enum BowState { Idle, NormalAttack, Charging }
    private BowState _bowState = BowState.Idle;

    private bool  _hasFired;
    private float _chargeStartTime;
    private float _chargeRatio;   // 0 ~ 1

    // 참조
    private PlayerMovement _playerMovement;
    private Transform      _weaponTransform;   // 떨림용 (활 스프라이트 자신의 Transform)
    private Vector3        _weaponLocalOrigin; // 떨림 원점
    private GameObject     _aimUpVfx;          // 100% 차징 시 켜지는 VFX (Player의 자식)
    private bool           _aimVfxTriggered;   // 이번 차징에서 이미 켰는지 방지용

    private SpriteRenderer       _spriteRenderer;
    private MaterialPropertyBlock _propBlock;
    private static readonly int  _glowIntensityId = Shader.PropertyToID("_GlowIntensity");
    private static readonly int  _glowColorId     = Shader.PropertyToID("_GlowColor");


    private void Awake()
    {
        CurrentComboStep   = 1;
        _playerMovement    = GetComponentInParent<PlayerMovement>();
        _weaponTransform   = transform;
        _weaponLocalOrigin = _weaponTransform.localPosition;

        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        _propBlock      = new MaterialPropertyBlock();

        // Player의 자식에서 Aim_UPVFX 오브젝트를 이름으로 찾아 참조합니다.
        Transform root = GetComponentInParent<Transform>().root;
        Transform found = FindDeep(root, "Aim_UPVFX");
        if (found != null)
        {
            _aimUpVfx = found.gameObject;
            _aimUpVfx.SetActive(false); // 시작 시 꺼둡니다.
        }
    }

    /// <summary>이름으로 자식 Transform을 재귀 탐색합니다.</summary>
    private Transform FindDeep(Transform parent, string targetName)
    {
        if (parent.name == targetName) return parent;
        foreach (Transform child in parent)
        {
            Transform result = FindDeep(child, targetName);
            if (result != null) return result;
        }
        return null;
    }

    private void Update()
    {
        HandleCharge();
    }

    // ─────────────────────────────────────────────────────────────
    // 차징 처리 (우클릭) - 매 프레임 직접 Update에서 폴링
    // ─────────────────────────────────────────────────────────────
    private void HandleCharge()
    {
        // 우클릭 시작
        if (Input.GetMouseButtonDown(1) && _bowState == BowState.Idle)
        {
            _bowState        = BowState.Charging;
            _chargeStartTime = Time.time;
            _hasFired        = false;
            _aimVfxTriggered = false; // 새 차징 시작 시 초기화

            // 차징 시작 시 애니메이션 0프레임부터 재생하되 즉시 일시정지
            if (_weaponAnimator != null)
            {
                _weaponAnimator.Play("Attack", 0, 0f);
                _weaponAnimator.speed = 0f; // 수동 제어
            }
        }

        // 우클릭 유지 중
        if (_bowState == BowState.Charging)
        {
            float elapsed = Time.time - _chargeStartTime;
            _chargeRatio  = Mathf.Clamp01(elapsed / _maxChargeTime);

            // 차징 정도에 따라 애니메이션 normalized time을 0 ~ _fireDelayNormalizedTime 사이로 수동 이동
            if (_weaponAnimator != null)
            {
                _weaponAnimator.speed = 0f;
                _weaponAnimator.Play("Attack", 0, _chargeRatio * _fireDelayNormalizedTime);
            }

            // 이동속도 둔화: 차징이 0%면 그대로, 100%면 _maxSlowRatio 배
            if (_playerMovement != null)
            {
                float speedMult = Mathf.Lerp(1f, _maxSlowRatio, _chargeRatio);
                _playerMovement.SpeedMultiplier = speedMult;
            }

            // 100% 달성 순간 딱 한 번 Aim_UPVFX를 켭니다.
            if (_chargeRatio >= 1f && !_aimVfxTriggered)
            {
                _aimVfxTriggered = true;
                if (_aimUpVfx != null)
                {
                    _aimUpVfx.SetActive(true);
                    StartCoroutine(DisableAimVfxAfterDelay(1.1f));
                }
            }

            // 최대 차징 시 떨림 효과
            if (_chargeRatio >= 1f)
            {
                float shakeX = Mathf.Sin(Time.time * _shakeFrequency)         * _shakeAmplitude;
                float shakeY = Mathf.Sin(Time.time * _shakeFrequency * 1.3f)  * _shakeAmplitude;
                _weaponTransform.localPosition = _weaponLocalOrigin + new Vector3(shakeX, shakeY, 0f);
            }
            else
            {
                _weaponTransform.localPosition = _weaponLocalOrigin;
            }

            // 우클릭을 놓으면 발사
            if (Input.GetMouseButtonUp(1))
            {
                ReleaseCharge();
            }

            UpdateGlowEffect();
        }
    }

    private void UpdateGlowEffect()
    {
        if (_spriteRenderer == null) return;

        float intensity = _chargeRatio * _maxGlowIntensity;

        // 최대 차징 시 맥동(Pulse) 효과 추가
        if (_chargeRatio >= 1f)
        {
            intensity += Mathf.PingPong(Time.time * 5f, _pulseAmplitude);
        }

        _spriteRenderer.GetPropertyBlock(_propBlock);
        _propBlock.SetColor(_glowColorId, _glowColor);
        _propBlock.SetFloat(_glowIntensityId, intensity);
        _spriteRenderer.SetPropertyBlock(_propBlock);
    }


    /// <summary>우클릭을 놓았을 때 차징 상태를 종료하고 차징 화살을 발사합니다.</summary>
    private void ReleaseCharge()
    {
        if (_bowState != BowState.Charging) return;

        float ratio = _chargeRatio;

        // 상태 초기화
        _bowState = BowState.Idle;
        ResetChargeEffects();

        // 차징 화살 발사
        FireArrow(ratio);

        // 나머지 발사 후 애니메이션 후속 재생 (발사 후 복원 모션)
        if (_weaponAnimator != null)
        {
            _weaponAnimator.speed = 1f;
            _weaponAnimator.Play("Attack", 0, _fireDelayNormalizedTime);
        }

        // 자동 종료를 위해 "NormalAttack" 상태처럼 잠시 IsAttacking 켜기
        // (PollFinished에서 Idle 복귀 처리)
        IsAttacking = true;
        _bowState   = BowState.NormalAttack;
        _hasFired   = true;
    }

    /// <summary>차징 관련 부작용(속도 둔화, 떨림, 애니메이터 속도, 글로우)을 모두 초기화합니다.</summary>
    private void ResetChargeEffects()
    {
        if (_playerMovement != null) _playerMovement.SpeedMultiplier = 1f;
        _weaponTransform.localPosition = _weaponLocalOrigin;
        
        if (_weaponAnimator != null) _weaponAnimator.speed = 1f;

        // 글로우 초기화
        if (_spriteRenderer != null)
        {
            _spriteRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetFloat(_glowIntensityId, 0f);
            _spriteRenderer.SetPropertyBlock(_propBlock);
        }
    }

    private System.Collections.IEnumerator DisableAimVfxAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_aimUpVfx != null) _aimUpVfx.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────
    // WeaponBehaviourBase 구현 (좌클릭 일반 공격)
    // ─────────────────────────────────────────────────────────────
    public override void BeginAttack(int comboStep)
    {
        if (_bowState == BowState.Charging) return; // 차징 중 좌클릭은 무시

        IsAttacking      = true;
        _hasFired        = false;
        CurrentComboStep = 1;
        _bowState        = BowState.NormalAttack;

        if (_weaponAnimator != null)
        {
            _weaponAnimator.speed = 1f;
            _weaponAnimator.Play("Attack", 0, 0f);
        }
    }

    public override bool PollFinished(float attackStartTime)
    {
        if (_bowState == BowState.Charging) return false;
        if (Time.time - attackStartTime < 0.05f) return false;

        AnimatorStateInfo info = _weaponAnimator.GetCurrentAnimatorStateInfo(0);

        // 좌클릭 일반 발사: 지정 시점에 화살 발사
        if (_bowState == BowState.NormalAttack
            && info.IsName("Attack")
            && info.normalizedTime >= _fireDelayNormalizedTime
            && !_hasFired)
        {
            FireArrow(1f); // 일반 공격은 항상 100% 위력
        }

        // 95% 이상 재생 시 Idle로 귀환
        if (!info.IsName("Attack") || info.normalizedTime >= 0.95f)
        {
            if (!_hasFired) FireArrow(1f);

            IsAttacking = false;
            _bowState   = BowState.Idle;

            if (info.IsName("Attack"))
                _weaponAnimator.Play("Idle", 0, 0f);

            return true;
        }
        return false;
    }

    // ─────────────────────────────────────────────────────────────
    // 화살 발사
    // ─────────────────────────────────────────────────────────────
    /// <param name="chargeRatio">0~1. 차징 비율에 따라 속도/데미지 결정.</param>
    private void FireArrow(float chargeRatio)
    {
        _hasFired = true;
        if (_arrowPrefab == null || _arrowPos == null) return;

        // 속도, 데미지 보간
        float speed  = Mathf.Lerp(_minArrowSpeed,  _maxArrowSpeed,  chargeRatio);
        int   damage = Mathf.RoundToInt(Mathf.Lerp(_minArrowDamage, _maxArrowDamage, chargeRatio));

        GameObject arrowObj = Instantiate(_arrowPrefab, _arrowPos.position, _arrowPos.rotation);

        // ArrowProjectile에 차징 값 전달
        ArrowProjectile ap = arrowObj.GetComponent<ArrowProjectile>();
        if (ap != null)
        {
            ap.SetStats(speed, damage);
        }
    }

    public override void OnDeactivated()
    {
        _bowState = BowState.Idle;
        IsAttacking = false;
        _hasFired   = false;
        ResetChargeEffects();

        if (_weaponAnimator != null)
            _weaponAnimator.Play("Idle", 0, 0f);
    }
}
