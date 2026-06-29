using UnityEngine;

/// <summary>
/// 곰: 적대적 동물. 플레이어를 공격받지 않아도 감지·추격하고,
/// 좌우 공격만 가능하므로 플레이어 "옆"으로 접근해 y좌표가 비슷해지면 멈춰서 공격한다.
///
/// - 평소에는 매우 느리게, 긴 멈춤(Idle)을 섞어 배회한다.
/// - 추격 목표는 플레이어 정중앙이 아니라, 곰이 위치한 쪽 옆(가로 사거리 안쪽)이다.
/// - 조금이라도 이동(추격/배회)하면 공격 쿨다운이 해제된다.
/// - 공격 시작 후 _attackLockDuration 동안 이동을 멈추고 공격 자세를 유지한다(미끄러짐 방지 + 후딜).
/// - 플레이어가 _chaseGiveUpDistance 이상 멀어지면 추격을 포기한다.
/// - 피격 시 공격이 중단되고 살짝 밀려나며 공격 애니메이션이 초기화된다.
/// - 공격 판정은 MonsterAttackHandler의 Animation Event 모드(+좌/우 히트박스 콜라이더)로 처리한다.
/// </summary>
[RequireComponent(typeof(DetectionSystem))]
[RequireComponent(typeof(MonsterNavigator))]
[RequireComponent(typeof(WanderSystem))]
[RequireComponent(typeof(MonsterAttackHandler))]
public sealed class BearMonster : MonsterBase
{
    [Header("Bear Attack Gating")]
    [Tooltip("이 값 이하로 플레이어와의 y좌표 차이가 좁혀지면 '정렬됨'으로 본다 (Unity 단위).")]
    [SerializeField] private float _yAlignThreshold = 0.8f;

    [Tooltip("플레이어와의 가로 거리(|Δx|)가 이 값 이하이면 공격 사거리 안으로 본다. " +
             "0 이하이면 AttackShape(Rectangle)의 RectWidth를 사용한다.")]
    [SerializeField] private float _horizontalAttackRange = 0f;

    [Header("Bear Attack Lock")]
    [Tooltip("공격을 시작한 뒤 이 시간 동안 이동을 멈추고 공격 자세를 유지(공격 애니메이션 + 후딜).")]
    [SerializeField] private float _attackLockDuration = 1.0f;

    [Header("Bear Chase")]
    [Tooltip("추격 시 플레이어 옆(가로 사거리 안쪽)으로 접근하기 위한 정지 거리. 0 이하이면 가로 사거리의 0.7배.")]
    [SerializeField] private float _sideStandoff = 0f;

    [Tooltip("플레이어가 이 거리 이상 멀어지면 추격을 포기한다 (Unity 단위). 감지 반경보다 커야 함.")]
    [SerializeField] private float _chaseGiveUpDistance = 12f;

    [Header("Bear Wander (느리게 + 긴 멈춤)")]
    [Tooltip("배회 이동속도 배율(기본 속도 대비). 작을수록 느리게 걷는다.")]
    [SerializeField] private float _wanderSpeedFactor = 0.18f;
    [SerializeField] private float _wanderMoveTimeMin = 0.6f;
    [SerializeField] private float _wanderMoveTimeMax = 1.5f;
    [SerializeField] private float _wanderIdleTimeMin = 2.5f;
    [SerializeField] private float _wanderIdleTimeMax = 5f;

    [Header("Bear Hit Reaction")]
    [Tooltip("피격 시 밀려나는 거리 (Unity 단위).")]
    [SerializeField] private float _knockbackDistance = 0.5f;

    private DetectionSystem      _detection;
    private MonsterNavigator     _navigator;
    private WanderSystem         _wander;
    private MonsterAttackHandler _attacker;
    private Animator             _animator;
    private Rigidbody2D          _rb;

    private float _attackLockTimer;
    private float _approachSide = -1f;   // 플레이어 기준 곰이 설 쪽(+1 오른쪽 / -1 왼쪽)

    private float _wanderPhaseTimer;
    private bool  _wanderIdling = true;   // 시작은 멈춤(Idle)

    private static readonly int HashAttack = Animator.StringToHash("Attack");

    protected override void Awake()
    {
        base.Awake();
        _detection = GetComponent<DetectionSystem>();
        _navigator = GetComponent<MonsterNavigator>();
        _wander    = GetComponent<WanderSystem>();
        _attacker  = GetComponent<MonsterAttackHandler>();
        _animator  = GetComponent<Animator>();
        _rb        = GetComponent<Rigidbody2D>();
    }

    private float HorizontalAttackRange
    {
        get
        {
            if (_horizontalAttackRange > 0f) return _horizontalAttackRange;
            if (_runtime.Data != null &&
                _runtime.Data.AttackShape.ShapeType == AttackShapeType.Rectangle)
                return _runtime.Data.AttackShape.RectWidth;
            if (_runtime.Data != null &&
                _runtime.Data.AttackShape.ShapeType == AttackShapeType.Circle)
                return _runtime.Data.AttackShape.CircleRadius;
            return 1.2f;
        }
    }

    private float SideStandoff => _sideStandoff > 0f ? _sideStandoff : HorizontalAttackRange * 0.7f;

    protected override void Update()
    {
        if (_attackLockTimer > 0f)
            _attackLockTimer -= Time.deltaTime;
        base.Update();
    }

    protected override BTNode BuildBT()
    {
        // ── 0. 공격 락: 공격 직후 일정 시간 정지 + 공격 자세 유지 (미끄러짐 방지) ──
        var lockSeq = new BTSequence(new BTNode[]
        {
            new BTCondition(() => _attackLockTimer > 0f),
            new BTAction(() =>
            {
                _navigator.Stop();
                if (_detection.HasTarget) FaceTargetHorizontally(_detection.DetectedTarget);
                _runtime.CurrentState = MonsterState.Attack;
                return BTStatus.Running;
            })
        });

        // ── 1. 공격: y 정렬 + 가로 사거리 + 쿨다운 0 ──
        var checkAttack = new BTCondition(() =>
        {
            if (!_detection.HasTarget) return false;
            if (_runtime.AttackCooldownTimer > 0f) return false;
            return IsAlignedAndInRange();
        });

        var attackAction = new BTAction(() =>
        {
            Transform target = _detection.DetectedTarget;
            _navigator.Stop();
            FaceTargetHorizontally(target);

            _runtime.CurrentState = MonsterState.Attack;
            _attacker.ExecuteAttack(target.position);
            _attackLockTimer = _attackLockDuration;
            return BTStatus.Success;
        });

        var attackSeq = new BTSequence(new BTNode[] { checkAttack, attackAction });

        // ── 2. 사거리 안이지만 쿨다운 중: 멈춰서 대기 ──
        var holdSeq = new BTSequence(new BTNode[]
        {
            new BTCondition(() => _detection.HasTarget && IsAlignedAndInRange()),
            new BTAction(() =>
            {
                _navigator.Stop();
                FaceTargetHorizontally(_detection.DetectedTarget);
                _runtime.CurrentState = MonsterState.Idle;
                return BTStatus.Running;
            })
        });

        // ── 3. 추격: 플레이어 "옆"으로 접근. 이동하므로 공격 쿨다운 해제. 너무 멀면 포기 ──
        var chaseSeq = new BTSequence(new BTNode[]
        {
            new BTCondition(() => _detection.HasTarget),
            new BTAction(() =>
            {
                Transform target = _detection.DetectedTarget;

                float dist = Vector2.Distance(transform.position, target.position);
                if (dist > _chaseGiveUpDistance)
                {
                    _detection.ForceRelease();
                    return BTStatus.Failure;
                }

                _runtime.CurrentState = MonsterState.Chase;
                if (_runtime.Data != null)
                    _runtime.CurrentSpeed = _runtime.Data.Speed * 0.01f;

                // 조금이라도 이동하면 3초 공격 제한 해제
                _runtime.AttackCooldownTimer = 0f;

                _navigator.MoveToward(GetSideApproachPoint(target));
                return BTStatus.Running;
            })
        });

        // ── 4. 배회: 느리게 + 긴 멈춤(Idle) ──
        var wanderAction = new BTAction(ExecuteWander);

        // 우선순위: 공격 락 → 공격 → 사거리내 대기 → 추격 → 배회
        return new BTSelector(new BTNode[] { lockSeq, attackSeq, holdSeq, chaseSeq, wanderAction });
    }

    // ══════════════════════════════════════════════════════════════════
    //  Wander: 매우 느리게, 긴 멈춤(Idle)을 섞어서
    // ══════════════════════════════════════════════════════════════════
    private BTStatus ExecuteWander()
    {
        _wanderPhaseTimer -= Time.deltaTime;
        if (_wanderPhaseTimer <= 0f)
        {
            _wanderIdling = !_wanderIdling;
            _wanderPhaseTimer = _wanderIdling
                ? Random.Range(_wanderIdleTimeMin, _wanderIdleTimeMax)
                : Random.Range(_wanderMoveTimeMin, _wanderMoveTimeMax);
        }

        if (_wanderIdling)
        {
            _runtime.CurrentState = MonsterState.Idle; // 멈출 땐 Idle
            _navigator.Decelerate();
            return BTStatus.Running;
        }

        _runtime.CurrentState = MonsterState.Wander;
        if (_runtime.Data != null)
            _runtime.CurrentSpeed = _runtime.Data.Speed * 0.01f * _wanderSpeedFactor; // 매우 느림

        _runtime.AttackCooldownTimer = 0f; // 이동 시 공격 제한 해제
        Vector2 dir = _wander.GetWanderDirection();
        _navigator.MoveInDirection(dir);
        return BTStatus.Running;
    }

    // ══════════════════════════════════════════════════════════════════
    //  피격 반응: 공격 중단 + 넉백 + 공격 애니메이션 초기화
    // ══════════════════════════════════════════════════════════════════
    public override void TakeDamage(float damage, GameObject source = null)
    {
        // 튜토리얼 곰: 차징 스킬에 처음 맞기 전까지 무적 (TutorialBearGuard 부착 시에만 동작)
        if (TryGetComponent<TutorialBearGuard>(out var tutGuard) && tutGuard.ShouldBlock(source)) return;

        bool wasAlive = IsAlive;
        base.TakeDamage(damage, source); // 데미지 적용 → HealthSystem.OnHit → HitStaggerHandler 경직

        if (!wasAlive || !IsAlive) return; // 사망 시엔 반응 불필요

        InterruptAttack();
        ApplyKnockback(source);
    }

    private void InterruptAttack()
    {
        _attackLockTimer = 0f;
        _attacker.CancelAttack();

        // 진행 중인 공격 애니메이션을 Idle로 초기화 (경직 중 speed=0이라 프레임 고정)
        if (_animator != null)
        {
            _animator.ResetTrigger(HashAttack);
            _animator.Play("Idle", 0, 0f);
        }
    }

    private void ApplyKnockback(GameObject source)
    {
        Vector2 dir = Vector2.zero;
        if (source != null)
        {
            Vector2 d = (Vector2)transform.position - (Vector2)source.transform.position;
            if (d.sqrMagnitude > 0.0001f) dir = d.normalized;
        }

        if (dir.sqrMagnitude < 0.0001f)
        {
            // 공격자 정보가 없으면 바라보는 반대 방향으로
            float fx = _runtime.CurrentDirection.x;
            dir = new Vector2(fx >= 0f ? -1f : 1f, 0f);
        }

        if (_rb != null)
            _rb.MovePosition(_rb.position + dir * _knockbackDistance);
    }

    // ══════════════════════════════════════════════════════════════════
    //  헬퍼
    // ══════════════════════════════════════════════════════════════════
    private bool IsAlignedAndInRange()
    {
        Transform target = _detection.DetectedTarget;
        if (target == null) return false;

        float dx = target.position.x - transform.position.x;
        float dy = target.position.y - transform.position.y;

        bool yAligned = Mathf.Abs(dy) <= _yAlignThreshold;
        bool inXRange = Mathf.Abs(dx) <= HorizontalAttackRange;
        return yAligned && inXRange;
    }

    private Vector2 GetSideApproachPoint(Transform target)
    {
        float dx = target.position.x - transform.position.x;
        if (Mathf.Abs(dx) > 0.1f)
            _approachSide = dx >= 0f ? -1f : 1f;

        return new Vector2(
            target.position.x + _approachSide * SideStandoff,
            target.position.y);
    }

    private void FaceTargetHorizontally(Transform target)
    {
        if (target == null) return;
        float dx = target.position.x - transform.position.x;
        if (Mathf.Abs(dx) > 0.01f)
            _runtime.CurrentDirection = new Vector2(Mathf.Sign(dx), 0f);
    }
}
