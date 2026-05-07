using UnityEngine;
using System.Collections;

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

    [Header("Stats (플레이어 스탯 연동)")]
    [Tooltip("Player 루트 오브젝트의 PlayerEntity 컴포넌트를 인스펙터에서 연결하세요.")]
    [SerializeField] private PlayerEntity _playerEntity;

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

    [Header("조준 사격 (Aimed Shot) 스킬")]
    [Tooltip("스킬 차징 시 표시할 AimedShot_Bow 프리팹")]
    [SerializeField] private GameObject _aimedShotBowPrefab;
    [Tooltip("발사할 AimShootArrow(관통 화살) 프리팹")]
    [SerializeField] private GameObject _aimShootArrowPrefab;


    // ── WeaponBehaviourBase 오버라이드 ───────────────────────────
    public override WeaponType WeaponType          => WeaponType.Bow;
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
    private enum BowState { Idle, NormalAttack, Charging, AimedShot }
    private BowState _bowState = BowState.Idle;

    private bool  _hasFired;
    private float _chargeStartTime;
    private float _chargeRatio;   // 0 ~ 1

    // 참조
    private PlayerMovement _playerMovement;
    private Transform      _weaponTransform;        // 떨림용 (활 스프라이트 자신의 Transform)
    private Vector3        _weaponLocalOrigin;      // 떨림 원점
    private Vector3        _weaponAnimatorScale;    // 애니메이터 자식의 원본 localScale (키프레임 덮어쓰기 방지)
    private GameObject     _aimUpVfx;          // 100% 차징 시 켜지는 VFX (Player의 자식)
    private Animator       _aimUpVfxAnimator;  // Aim_UPVFX 애니메이터 (재시작용)
    private bool           _aimVfxTriggered;   // 이번 차징에서 이미 켰는지 방지용
    private Coroutine      _aimVfxCoroutine;   // 진행 중인 VFX 비활성화 코루틴 참조
    private WaitForSeconds _waitAimVfx;        // GC 방지용 캐시

    private SpriteRenderer       _spriteRenderer;
    private MaterialPropertyBlock _propBlock;
    private static readonly int  _glowIntensityId = Shader.PropertyToID("_GlowIntensity");
    private static readonly int  _glowColorId     = Shader.PropertyToID("_GlowColor");

    // ── 조준 사격 상태 ────────────────────────────────────────────
    private GameObject     _aimedShotBowInstance;
    private Animator       _aimedShotBowAnimator;
    private SpriteRenderer _aimedShotBowRendererMain; // AimedShot_Bow 자체의 렌더러
    private MaterialPropertyBlock _aimedShotBowPropBlock;
    private ParticleSystem _aimedShotGatherParticle;  // 기 모으는 파티클
    private SpriteRenderer _aimedShotArrowRenderer;
    private Transform      _aimedShotArrowPos;
    private float          _aimedShotChargeTime;
    private float          _aimedShotDamageMult;
    private float          _aimedShotChargeStartTime;
    private float          _aimedShotMinCoeff = 0.5f;
    private bool           _aimedShotReleased;       // ReleaseAimedShot 후 페이드 중 HandleAimedShot 갱신 방지
    private bool           _aimedShotArrowSpawned;   // AImArrow가 생성되었는지
    private bool           _aimedShotVfxTriggered;   // Aim_UPVFX가 트리거되었는지
    private GameObject     _aimedShotArrowInstance;  // 대기 중인 AImArrow 인스턴스
    private AimedShotArrow _aimedShotArrowScript;    // AImArrow의 스크립트 참조
    private float          _aimedShotArrowSpawnTime; // AImArrow 생성 시간 (블룸 램프용)


    private void Awake()
    {
        CurrentComboStep   = 1;
        _playerMovement      = GetComponentInParent<PlayerMovement>();
        
        // 🔮 수정: 루트(transform) 대신 자식(Animator) 트랜스폼을 흔들어 FloatingWeaponMotion과 충돌 방지
        _weaponTransform     = _weaponAnimator != null ? _weaponAnimator.transform : transform;
        _weaponLocalOrigin   = _weaponTransform.localPosition;
        
        // 애니메이터가 붙은 자식 오브젝트의 원본 scale 캐싱 (루트가 아닌 자식 기준)
        _weaponAnimatorScale = _weaponAnimator != null
                               ? _weaponAnimator.transform.localScale
                               : Vector3.one;

        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        _propBlock      = new MaterialPropertyBlock();
        _waitAimVfx     = new WaitForSeconds(1.1f); // 코루틴용 캐시 (GC 방지)

        // Player의 자식에서 Aim_UPVFX 오브젝트를 이름으로 찾아 참조합니다.
        Transform root = GetComponentInParent<Transform>().root;
        Transform found = FindDeep(root, "Aim_UPVFX");
        if (found != null)
        {
            _aimUpVfx         = found.gameObject;
            _aimUpVfxAnimator = _aimUpVfx.GetComponent<Animator>();
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
        HandleAimedShot();
    }

    private void LateUpdate()
    {
        // 애니메이션 클립에 Scale 키프레임이 있을 경우 자식(animator) 오브젝트의 scale을
        // 원본으로 강제 복원합니다. SwordBehaviour의 localEulerAngles 리셋과 동일한 패턴.
        if (_weaponAnimator != null)
            _weaponAnimator.transform.localScale = _weaponAnimatorScale;
    }

    // ── Skill Logic ─────────────────────────────────────────────
    private float _skillDamageMult = 1f;

    public void StartSkillCharge(float chargeTime, float damageMult)
    {
        if (_bowState != BowState.Idle) return;

        _bowState        = BowState.Charging;
        IsAttacking      = true;
        _chargeStartTime = Time.time;
        _hasFired        = false;
        _aimVfxTriggered = false;
        
        _maxChargeTime   = chargeTime;
        _skillDamageMult = damageMult;

        if (_weaponAnimator != null)
        {
            _weaponAnimator.Play("Attack", 0, 0f);
            _weaponAnimator.speed = 0f;
        }
    }

    /// <summary>
    /// 조준 사격 차징을 시작합니다. AimedShotSkillData.Execute()에서 호출됩니다.
    /// </summary>
    public void StartAimedShot(float chargeTime, float damageMult)
    {
        if (_bowState != BowState.Idle) return;

        _bowState                 = BowState.AimedShot;
        IsAttacking               = true;
        _aimedShotChargeTime      = chargeTime;
        _aimedShotDamageMult      = damageMult;
        _aimedShotChargeStartTime = Time.time;
        _aimedShotReleased        = false;
        _aimedShotArrowSpawned    = false;
        _aimedShotVfxTriggered    = false;
        _aimedShotArrowInstance   = null;
        _aimedShotArrowScript     = null;

        // 기존 활 스프라이트 숨기기
        if (_spriteRenderer != null) _spriteRenderer.enabled = false;

        // AimedShot_Bow를 WeaponPivot 자식으로 인스턴스화
        if (_aimedShotBowPrefab != null)
        {
            Transform pivot = transform.parent; // WeaponPivot
            _aimedShotBowInstance = Instantiate(_aimedShotBowPrefab, pivot);
            _aimedShotBowInstance.transform.localPosition = transform.localPosition;
            _aimedShotBowInstance.transform.localRotation = Quaternion.identity;
            _aimedShotBowInstance.transform.localScale    = Vector3.one;

            // 참조 캐싱
            _aimedShotBowAnimator     = _aimedShotBowInstance.GetComponent<Animator>();
            _aimedShotBowRendererMain = _aimedShotBowInstance.GetComponent<SpriteRenderer>();
            _aimedShotBowPropBlock    = new MaterialPropertyBlock();

            // AimedShot_Bow 자체에 보랏빛 블룸 셰이더 적용
            if (_aimedShotBowRendererMain != null)
            {
                Material glowMat = new Material(Shader.Find("Custom/SpriteGlow"));
                glowMat.EnableKeyword("_USE_MAIN_ALPHA_AS_GLOW");
                _aimedShotBowRendererMain.material = glowMat;
            }

            Transform arrowChild  = _aimedShotBowInstance.transform.Find("AimedShot_Arrow");
            if (arrowChild != null)
                _aimedShotArrowRenderer = arrowChild.GetComponent<SpriteRenderer>();
            Transform arrowPosChild = _aimedShotBowInstance.transform.Find("ArrowPos");
            if (arrowPosChild != null)
            {
                _aimedShotArrowPos = arrowPosChild;

                // ── 에너지가 모이는 (Gathering) 파티클 동적 생성 ──
                GameObject vfxObj = new GameObject("AimedShotGatherVFX");
                vfxObj.transform.SetParent(_aimedShotArrowPos);
                vfxObj.transform.localPosition = Vector3.zero;
                vfxObj.transform.localRotation = Quaternion.identity;

                _aimedShotGatherParticle = vfxObj.AddComponent<ParticleSystem>();
                var main = _aimedShotGatherParticle.main;
                main.duration = 3f;
                main.startLifetime = 0.35f;
                main.startSpeed = -8f; // 중앙으로 빠르게 수렴
                main.startSize = 0.15f;
                main.startColor = _glowColor;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.playOnAwake = false;

                var emission = _aimedShotGatherParticle.emission;
                emission.rateOverTime = 0f;

                var shape = _aimedShotGatherParticle.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 2.5f;
                shape.radiusThickness = 0.1f; // 표면에서 발생

                var psRenderer = _aimedShotGatherParticle.GetComponent<ParticleSystemRenderer>();
                Material gatherMat = new Material(Shader.Find("Custom/SpriteGlow"));
                gatherMat.EnableKeyword("_USE_MAIN_ALPHA_AS_GLOW");
                gatherMat.SetFloat("_GlowIntensity", 3f);
                gatherMat.SetColor("_GlowColor", _glowColor);
                psRenderer.material = gatherMat;
                psRenderer.sortingLayerName = "Weapons";
                psRenderer.sortingOrder = 15;
            }

            // 화살 alpha 0에서 시작
            if (_aimedShotArrowRenderer != null)
            {
                Color c = _aimedShotArrowRenderer.color;
                c.a = 0f;
                _aimedShotArrowRenderer.color = c;
            }

            // 차징 시작 애니메이션 재생
            if (_aimedShotBowAnimator != null)
            {
                _aimedShotBowAnimator.Play("AimedShotChargeStart", 0, 0f);
            }
        }

        Debug.Log($"[BowBehaviour] 조준 사격 차징 시작! (차징 시간: {chargeTime:F1}s, 최대 계수: {damageMult:F1}x)");
    }

    /// <summary>
    /// 매 프레임 조준 사격 상태를 갱신합니다 (alpha 페이드, 블룸, 이동속도 둔화, Aim_UPVFX).
    /// </summary>
    private void HandleAimedShot()
    {
        if (_bowState != BowState.AimedShot || _aimedShotReleased) return;

        float elapsed     = Time.time - _aimedShotChargeStartTime;
        float chargeRatio = Mathf.Clamp01(elapsed / _aimedShotChargeTime);

        // AimedShot_Arrow alpha: 0 → 1 (차징 비례)
        if (_aimedShotArrowRenderer != null)
        {
            Color c = _aimedShotArrowRenderer.color;
            c.a = chargeRatio;
            _aimedShotArrowRenderer.color = c;
        }

        // 이동속도 둔화: 차징 0% → 30% 감소(0.7배), 100% → 70% 감소(0.3배)
        if (_playerMovement != null)
        {
            float speedMult = Mathf.Lerp(0.7f, 0.3f, chargeRatio);
            _playerMovement.SpeedMultiplier = speedMult;
        }

        // ── AimedShot_Bow 블룸 효과 & 파티클 ──
        if (_aimedShotBowRendererMain != null && _aimedShotBowPropBlock != null)
        {
            float bowGlow = chargeRatio * _maxGlowIntensity * 0.7f; // 화살보다 살짝 낮게(0.7배)
            if (chargeRatio >= 1f)
            {
                bowGlow += Mathf.PingPong(Time.time * 5f, _pulseAmplitude * 0.7f);
            }
            
            _aimedShotBowRendererMain.GetPropertyBlock(_aimedShotBowPropBlock);
            _aimedShotBowPropBlock.SetFloat("_GlowIntensity", bowGlow);
            _aimedShotBowPropBlock.SetColor("_GlowColor", _glowColor);
            _aimedShotBowRendererMain.SetPropertyBlock(_aimedShotBowPropBlock);
        }

        if (_aimedShotGatherParticle != null)
        {
            var em = _aimedShotGatherParticle.emission;
            // 차징 0% -> rate 0, 100% -> rate 60
            em.rateOverTime = Mathf.Lerp(0f, 60f, chargeRatio);
            if (!_aimedShotGatherParticle.isPlaying && chargeRatio > 0.05f)
            {
                _aimedShotGatherParticle.Play();
            }
        }

        // ── ChargeStart 애니메이션 종료 감지 → AImArrow 생성 ──
        if (!_aimedShotArrowSpawned && _aimedShotBowAnimator != null)
        {
            AnimatorStateInfo stateInfo = _aimedShotBowAnimator.GetCurrentAnimatorStateInfo(0);
            // ChargeStart가 끝났거나, 차징이 100%에 도달하면 AImArrow 생성
            bool chargeStartFinished = stateInfo.IsName("AimedShotChargeStart") && stateInfo.normalizedTime >= 0.95f;
            bool chargeComplete      = chargeRatio >= 1f;
            if (chargeStartFinished || chargeComplete)
            {
                SpawnAimedShotArrowAtPos();
            }
        }

        // ── AImArrow 블룸 효과: 생성 시점부터 0에서 점진적으로 증가 ──
        if (_aimedShotArrowSpawned && _aimedShotArrowScript != null)
        {
            // 생성 시점부터 차징 완료까지의 남은 시간 비례로 블룸 증가
            float timeSinceSpawn = Time.time - _aimedShotArrowSpawnTime;
            float remainChargeTime = _aimedShotChargeTime * (1f - Mathf.Clamp01((_aimedShotArrowSpawnTime - _aimedShotChargeStartTime) / _aimedShotChargeTime));
            float bloomRatio = remainChargeTime > 0.01f ? Mathf.Clamp01(timeSinceSpawn / remainChargeTime) : 1f;

            float glowIntensity = bloomRatio * _maxGlowIntensity;
            // 최대 차징 시 맥동 효과
            if (chargeRatio >= 1f)
            {
                glowIntensity += Mathf.PingPong(Time.time * 5f, _pulseAmplitude);
            }
            _aimedShotArrowScript.SetGlowIntensity(glowIntensity, _glowColor);
        }

        // ── 100% 차징 시 Aim_UPVFX 활성화 (활 우클릭과 동일한 시스템) ──
        if (chargeRatio >= 1f && !_aimedShotVfxTriggered)
        {
            _aimedShotVfxTriggered = true;
            if (_aimUpVfx != null)
            {
                if (_aimVfxCoroutine != null)
                {
                    StopCoroutine(_aimVfxCoroutine);
                    _aimVfxCoroutine = null;
                }
                _aimUpVfx.SetActive(true);
                if (_aimUpVfxAnimator != null)
                {
                    _aimUpVfxAnimator.Rebind();
                    _aimUpVfxAnimator.Update(0f);
                }
                _aimVfxCoroutine = StartCoroutine(DisableAimVfxAfterDelay());
            }
        }

        // ── 최대 차징 시 떨림 효과 ──
        if (chargeRatio >= 1f && _aimedShotBowInstance != null)
        {
            float shakeX = Mathf.Sin(Time.time * _shakeFrequency)         * _shakeAmplitude;
            float shakeY = Mathf.Sin(Time.time * _shakeFrequency * 1.3f)  * _shakeAmplitude;
            _aimedShotBowInstance.transform.localPosition = transform.localPosition + new Vector3(shakeX, shakeY, 0f);
        }
    }

    /// <summary>
    /// AimedShotChargeStart 종료 시 AImArrow를 ArrowPos에 생성합니다 (발사 대기 상태).
    /// </summary>
    private void SpawnAimedShotArrowAtPos()
    {
        if (_aimShootArrowPrefab == null || _aimedShotArrowPos == null) return;

        _aimedShotArrowSpawned = true;
        _aimedShotArrowSpawnTime = Time.time;

        _aimedShotArrowInstance = Instantiate(
            _aimShootArrowPrefab,
            _aimedShotArrowPos.position,
            _aimedShotArrowPos.rotation,
            _aimedShotArrowPos);  // ArrowPos의 자식으로 부착 (위치/회전 자동 추적)

        _aimedShotArrowInstance.transform.localPosition = Vector3.zero;
        _aimedShotArrowInstance.transform.localRotation = Quaternion.identity;

        _aimedShotArrowScript = _aimedShotArrowInstance.GetComponent<AimedShotArrow>();

        // SpriteGlow 셸이더 적용 (투명 배경 제외 부분에 블룸)
        SpriteRenderer arrowRenderer = _aimedShotArrowInstance.GetComponent<SpriteRenderer>();
        if (arrowRenderer != null)
        {
            Material glowMat = new Material(Shader.Find("Custom/SpriteGlow"));
            glowMat.EnableKeyword("_USE_MAIN_ALPHA_AS_GLOW");
            arrowRenderer.material = glowMat;
        }

        Debug.Log("[BowBehaviour] AImArrow 생성 완료! (발사 대기 중)");
    }

    /// <summary>
    /// 조준 사격 차징을 종료하고 화살을 발사합니다. QuickSlotManager에서 키 릴리즈 시 호출됩니다.
    /// </summary>
    public void ReleaseAimedShot()
    {
        if (_bowState != BowState.AimedShot) return;

        _aimedShotReleased = true;

        float elapsed     = Time.time - _aimedShotChargeStartTime;
        float chargeRatio = Mathf.Clamp01(elapsed / _aimedShotChargeTime);

        // AimedShot_Arrow alpha 즉시 0
        if (_aimedShotArrowRenderer != null)
        {
            Color c = _aimedShotArrowRenderer.color;
            c.a = 0f;
            _aimedShotArrowRenderer.color = c;
        }

        // AimedShotChargeEnd 애니메이션 재생
        if (_aimedShotBowAnimator != null)
        {
            _aimedShotBowAnimator.Play("AimedShotChargeEnd", 0, 0f);
        }

        // 이동속도 즉시 복원
        if (_playerMovement != null) _playerMovement.SpeedMultiplier = 1f;

        // Aim_UPVFX 즉시 비활성화
        if (_aimVfxCoroutine != null)
        {
            StopCoroutine(_aimVfxCoroutine);
            _aimVfxCoroutine = null;
        }
        if (_aimUpVfx != null) _aimUpVfx.SetActive(false);

        // 관통 화살 발사 (AImArrow가 이미 생성된 경우 Launch(), 아니면 새로 생성)
        LaunchAimedShotArrow(chargeRatio);

        // 카메라 쉐이킹: 차징 비례 강도 (0.1 ~ 0.35)
        if (CameraShakeController.Instance != null)
        {
            float shakeForce = Mathf.Lerp(0.1f, 0.35f, chargeRatio);
            CameraShakeController.Instance.Shake(0.2f, shakeForce);
        }

        // 파티클 즉시 제거
        if (_aimedShotGatherParticle != null)
            _aimedShotGatherParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // 플레이어 넉백 (발사 반동) - 강도를 대폭 상향
        if (_playerMovement != null)
        {
            float recoilForce = Mathf.Lerp(5f, 35f, chargeRatio);
            Vector2 dir = _playerMovement.FacingDirection; // 커서 방향 재활용
            _playerMovement.ApplyRecoil(-dir * recoilForce);
        }

        // 찰나의 타임 슬로우 (풀 차징에 가까울수록 효과, 50% 이상부터)
        if (chargeRatio >= 0.5f)
        {
            StartCoroutine(HitStopRoutine(0.1f, 0.2f)); // 0.1초 동안 0.2배속
        }

        Debug.Log($"[BowBehaviour] 조준 사격 발사! (차징: {chargeRatio * 100f:F0}%)");

        // AimedShot_Bow를 0.3초 동안 alpha 페이드 후 정리
        StartCoroutine(CleanupAimedShot());
    }

    /// <summary>대기 중인 AImArrow를 발사합니다. 아직 생성되지 않았다면 새로 만들어 발사합니다.</summary>
    private void LaunchAimedShotArrow(float chargeRatio)
    {
        float speed     = Mathf.Lerp(_minArrowSpeed, _maxArrowSpeed, chargeRatio);
        float statAtk   = _playerEntity != null ? _playerEntity.TotalAtk : 0f;
        float damageMul = Mathf.Lerp(_aimedShotMinCoeff, _aimedShotDamageMult, chargeRatio);
        float damage    = statAtk * damageMul;

        if (_aimedShotArrowSpawned && _aimedShotArrowInstance != null)
        {
            // 이미 생성된 AImArrow를 Launch
            _aimedShotArrowScript.SetStats(speed, damage);
            _aimedShotArrowScript.Launch();
        }
        else if (_aimShootArrowPrefab != null && _aimedShotArrowPos != null)
        {
            // ChargeStart가 아직 끝나기 전에 키를 놓은 경우: 새로 생성 후 즉시 발사
            GameObject arrowObj = Instantiate(
                _aimShootArrowPrefab,
                _aimedShotArrowPos.position,
                _aimedShotArrowPos.rotation);

            AimedShotArrow arrow = arrowObj.GetComponent<AimedShotArrow>();
            if (arrow != null)
            {
                arrow.SetStats(speed, damage);
                arrow.Launch();
            }
        }

        // 인스턴스 참조 해제 (발사 후에는 AImArrow가 독립적으로 검)
        _aimedShotArrowInstance = null;
        _aimedShotArrowScript  = null;
    }

    /// <summary>AimedShot_Bow의 alpha를 0.3초에 걸쳐 페이드아웃한 뒤 정리합니다.</summary>
    private IEnumerator CleanupAimedShot()
    {
        SpriteRenderer bowRenderer = _aimedShotBowInstance != null
            ? _aimedShotBowInstance.GetComponent<SpriteRenderer>()
            : null;

        float fadeDuration = 0.3f;
        float elapsed = 0f;

        if (bowRenderer != null)
        {
            Color startColor = bowRenderer.color;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                Color c = bowRenderer.color;
                c.a = Mathf.Lerp(startColor.a, 0f, t);
                bowRenderer.color = c;
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(fadeDuration);
        }

        FinishAimedShot();
    }

    /// <summary>조준 사격 상태를 완전히 정리합니다.</summary>
    private void FinishAimedShot()
    {
        // 대기 중인 AImArrow가 남아있으면 파괴
        if (_aimedShotArrowInstance != null)
        {
            Destroy(_aimedShotArrowInstance);
            _aimedShotArrowInstance = null;
            _aimedShotArrowScript  = null;
        }

        // AimedShot_Bow 인스턴스 파괴
        if (_aimedShotBowInstance != null)
        {
            Destroy(_aimedShotBowInstance);
            _aimedShotBowInstance = null;
        }

        _aimedShotBowAnimator   = null;
        _aimedShotArrowRenderer = null;
        _aimedShotArrowPos      = null;

        // 기존 활 스프라이트 복원
        if (_spriteRenderer != null) _spriteRenderer.enabled = true;

        // 이동속도 복원
        if (_playerMovement != null) _playerMovement.SpeedMultiplier = 1f;

        // Aim_UPVFX 정리
        if (_aimVfxCoroutine != null)
        {
            StopCoroutine(_aimVfxCoroutine);
            _aimVfxCoroutine = null;
        }
        if (_aimUpVfx != null) _aimUpVfx.SetActive(false);

        _bowState   = BowState.Idle;
        IsAttacking = false;
    }

    // ─────────────────────────────────────────────────────────────
    // 차징 처리 (우클릭) - 매 프레임 직접 Update에서 폴링
    // ─────────────────────────────────────────────────────────────
    private void HandleCharge()
    {
        // 우클릭 시작
        if (Input.GetMouseButtonDown(1) && _bowState == BowState.Idle && _bowState != BowState.AimedShot)
        {
            _bowState        = BowState.Charging;
            IsAttacking      = true;  // 차징 중 무기 교체 큐잉 차단
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
                    // 이전 코루틴이 남아있으면 먼저 취소
                    if (_aimVfxCoroutine != null)
                    {
                        StopCoroutine(_aimVfxCoroutine);
                        _aimVfxCoroutine = null;
                    }
                    _aimUpVfx.SetActive(true);
                    // Animator를 초기 상태로 리셋 → 매번 0프레임부터 재생 보장
                    if (_aimUpVfxAnimator != null)
                    {
                        _aimUpVfxAnimator.Rebind();
                        _aimUpVfxAnimator.Update(0f);
                    }
                    _aimVfxCoroutine = StartCoroutine(DisableAimVfxAfterDelay());
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

        // 발사 후 애니메이션이 현재 차징 지점에서 자연스럽게 나머지(발사 모션)를 재생하도록 속도만 복구
        if (_weaponAnimator != null)
        {
            _weaponAnimator.speed = 1f;
        }

        // 자동 종료를 위해 "NormalAttack" 상태처럼 잠시 IsAttacking 켜기
        // (PollFinished에서 Idle 복귀 처리)
        IsAttacking = true;
        _bowState   = BowState.NormalAttack;
        _hasFired   = true;
    }

    /// <summary>차징 관련 부작용(속도 둔화, 떨림, 애니메이터 속도, 글로우, VFX)을 모두 초기화합니다.</summary>
    private void ResetChargeEffects()
    {
        _skillDamageMult = 1.0f;
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

        // VFX 즉시 비활성화 + 대기 중인 코루틴 취소 (차징 해제 시 즉시 숨김)
        if (_aimVfxCoroutine != null)
        {
            StopCoroutine(_aimVfxCoroutine);
            _aimVfxCoroutine = null;
        }
        if (_aimUpVfx != null) _aimUpVfx.SetActive(false);
    }

    private System.Collections.IEnumerator DisableAimVfxAfterDelay()
    {
        yield return _waitAimVfx; // 캐시된 WaitForSeconds 재사용
        if (_aimUpVfx != null) _aimUpVfx.SetActive(false);
        _aimVfxCoroutine = null;
    }

    // ─────────────────────────────────────────────────────────────
    // WeaponBehaviourBase 구현 (좌클릭 일반 공격)
    // ─────────────────────────────────────────────────────────────
    public override void BeginAttack(int comboStep)
    {
        if (_bowState == BowState.Charging || _bowState == BowState.AimedShot) return; // 차징/조준사격 중 좌클릭 무시

        IsAttacking      = true;
        _hasFired        = false;
        CurrentComboStep = 1;
        _bowState        = BowState.NormalAttack;

        if (_weaponAnimator != null)
        {
            _weaponAnimator.speed = 1f;
            // 이전 공격에서 큐에 남은 트리거를 먼저 비워 중복 발화 방지
            _weaponAnimator.ResetTrigger("Attack");
            _weaponAnimator.SetTrigger("Attack");
        }
    }

    public override bool PollFinished(float attackStartTime)
    {
        if (_bowState == BowState.Charging) return false;
        if (_bowState == BowState.AimedShot) return false;  // 조준 사격 중에는 PollFinished 무시
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

            if (_weaponAnimator != null)
            {
                _weaponAnimator.speed = 1f;
                _weaponAnimator.ResetTrigger("Attack"); // 큐 잔류 트리거 제거
                _weaponAnimator.Play("Idle", 0, 0f);
                _weaponAnimator.Update(0f);
            }

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

        // 속도, 데미지 보간 (무기 데미지 + 플레이어 Atk 보너스)
        float speed       = Mathf.Lerp(_minArrowSpeed, _maxArrowSpeed, chargeRatio);
        float weaponDmg   = Mathf.Lerp(_minArrowDamage, _maxArrowDamage, chargeRatio);
        float statAtk     = _playerEntity != null ? _playerEntity.TotalAtk : 0f;
        float damage      = DamageCalculator.CalcOutgoingDamage(statAtk, weaponDmg) * _skillDamageMult;

        // 최적화: Instantiate 대신 SimpleObjectPool에서 가져옵니다.
        GameObject arrowObj = SimpleObjectPool.Instance.Get(_arrowPrefab, _arrowPos.position, _arrowPos.rotation);
        
        // ArrowProjectile에 차징 값 전달
        ArrowProjectile ap = arrowObj.GetComponent<ArrowProjectile>();
        if (ap != null) ap.SetStats(speed, damage);
    }

    public override void OnDeactivated()
    {
        // 조준 사격 중이면 즉시 정리
        if (_bowState == BowState.AimedShot)
        {
            StopAllCoroutines();
            FinishAimedShot();
        }

        _bowState   = BowState.Idle;
        IsAttacking = false;
        _hasFired   = false;
        ResetChargeEffects();

        if (_weaponAnimator != null)
        {
            // speed 복원 → ResetTrigger → Play 순서를 지켜야 Idle 0프레임이 확실히 평가됨
            _weaponAnimator.speed = 1f;
            _weaponAnimator.ResetTrigger("Attack");
            _weaponAnimator.Play("Idle", 0, 0f);
            _weaponAnimator.Update(0f);
        }
    }

    /// <summary>현재 조준 사격 차징 중인지 반환합니다.</summary>
    public bool IsAimedShotCharging => _bowState == BowState.AimedShot;

    /// <summary>
    /// 매우 짧은 시간 동안 게임 내 시간을 강제로 늦춰(Hit Stop) 타격감을 극대화합니다.
    /// </summary>
    private System.Collections.IEnumerator HitStopRoutine(float durationSec, float timeScale)
    {
        Time.timeScale = timeScale;
        yield return new WaitForSecondsRealtime(durationSec);
        Time.timeScale = 1f;
    }
}
