using UnityEngine;

/// <summary>
/// 스켈레톤: 매복형 적대 몹.
///
/// 동작 흐름:
///  1) Hidden    : 평소에는 보이지 않고(렌더러/콜라이더 off) 제자리에 매복.
///                 Bear와 동일한 감지 범위 안에 플레이어가 들어오면 등장 시작.
///  2) Revealing : Trigger(등장) 애니메이션을 1회 재생하며 모습을 드러낸다.
///  3) IdleDelay : 등장 애니가 끝나면 Idle을 0.5초간 유지.
///  4) Active    : 이후 Bear와 동일한 로직으로 플레이어 "옆"으로 추격(Walk) → 정렬되면 공격.
///                 피격 반응(넉백 + 공격 중단)도 Bear와 동일.
///  5) Dead      : 사망 시 Death 애니메이션을 재생하고 충돌 처리를 끈 뒤 페이드아웃.
///
/// 스켈레톤 스프라이트는 좌우 구분 애니가 없으므로 SpriteRenderer.flipX로 방향을 전환한다.
/// 공격 판정은 MonsterAttackHandler의 AttackShape(전방 사각형) + AttackDelay 방식으로 처리한다.
/// </summary>
[RequireComponent(typeof(DetectionSystem))]
[RequireComponent(typeof(MonsterNavigator))]
[RequireComponent(typeof(MonsterAttackHandler))]
public sealed class SkeletonMonster : MonsterBase
{
    private enum Phase { Hidden, Revealing, IdleDelay, Active, Dead }

    [Header("Reveal (등장)")]
    [Tooltip("등장(Trigger) 애니메이션이 끝난 뒤 Idle을 유지하는 시간(초).")]
    [SerializeField] private float _idleDelayAfterReveal = 0.5f;
    [Tooltip("등장 애니메이션 길이(초). 0이면 Trigger 클립 길이를 자동 사용.")]
    [SerializeField] private float _revealDurationOverride = 0f;

    [Header("Attack Gating (Bear와 동일)")]
    [Tooltip("이 값 이하로 플레이어와의 y좌표 차이가 좁혀지면 '정렬됨'으로 본다.")]
    [SerializeField] private float _yAlignThreshold = 0.8f;
    [Tooltip("가로 거리(|Δx|)가 이 값 이하이면 사거리 안. 0 이하이면 AttackShape를 사용.")]
    [SerializeField] private float _horizontalAttackRange = 0f;
    [Tooltip("공격 시작 뒤 이 시간 동안 이동을 멈추고 공격 자세를 유지(후딜).")]
    [SerializeField] private float _attackLockDuration = 1.0f;

    [Header("Chase (Bear와 동일)")]
    [Tooltip("추격 시 플레이어 옆으로 접근하기 위한 정지 거리. 0 이하이면 가로 사거리의 0.7배.")]
    [SerializeField] private float _sideStandoff = 0f;
    [Tooltip("플레이어가 이 거리 이상 멀어지면 추격을 포기한다.")]
    [SerializeField] private float _chaseGiveUpDistance = 12f;

    [Tooltip("추격 이동속도 = 플레이어 이동속도 × 이 값.")]
    [SerializeField] private float _speedFactorVsPlayer = 1.2f;
    [Tooltip("플레이어 StatSystem을 못 찾을 때 사용할 기본 플레이어 이동속도(초당).")]
    [SerializeField] private float _fallbackPlayerSpeed = 5f;

    [Header("Hit Reaction (Bear와 동일)")]
    [Tooltip("피격 시 밀려나는 거리.")]
    [SerializeField] private float _knockbackDistance = 0.5f;

    [Header("Separation (몹 겹침 방지)")]
    [Tooltip("이 반경 안의 다른 몹으로부터 밀어내 겹침/교착을 방지한다.")]
    [SerializeField] private float _separationRadius = 1.1f;
    [Tooltip("분리 스티어링 가중치(클수록 강하게 벌어진다).")]
    [SerializeField] private float _separationWeight = 1.3f;

    [Header("Facing")]
    [Tooltip("스프라이트 기본 방향이 오른쪽을 향하면 true.")]
    [SerializeField] private bool _spriteFacesRight = true;

    [Header("Death")]
    [Tooltip("Death 애니메이션이 끝난 뒤 완전 투명까지 페이드아웃에 걸리는 시간(초).")]
    [SerializeField] private float _fadeDuration = 1.0f;

    private DetectionSystem      _detection;
    private MonsterNavigator     _navigator;
    private MonsterAttackHandler _attacker;
    private Animator             _animator;
    private Rigidbody2D          _rb;
    private SpriteRenderer       _renderer;
    private MonsterHPBar         _hpBar;
    private Collider2D[]         _colliders;

    private Phase  _phase;
    private float  _attackLockTimer;
    private float  _idleTimer;
    private float  _revealTimer;
    private float  _approachSide = -1f;
    private float  _revealDuration = 1.75f;
    private float  _deathDuration  = 1.75f;
    private string _currentAnim;
    private bool   _facingRight = true;   // 현재 바라보는 방향(데드존 히스테리시스로 유지)
    private const float FacingDeadzone = 0.35f; // 이 폭 안에서는 방향을 바꾸지 않아 깜빡임 방지

    private static readonly Collider2D[] _separationBuffer = new Collider2D[12];
    private const int EnemyLayerMask = 1 << 8; // "Enemy" 레이어

    private StatSystem _targetStats;   // 추격 대상(플레이어)의 스탯 — 이동속도 추적용 캐시

    private float  _deathTimer;
    private Color  _baseColor = Color.white;

    private static readonly int AnimIdle    = Animator.StringToHash("Idle");
    private static readonly int AnimWalk    = Animator.StringToHash("Walk");
    private static readonly int AnimAttack  = Animator.StringToHash("Attack");
    private static readonly int AnimTrigger = Animator.StringToHash("Trigger");
    private static readonly int AnimDeath   = Animator.StringToHash("Death");

    protected override void Awake()
    {
        base.Awake();
        _detection = GetComponent<DetectionSystem>();
        _navigator = GetComponent<MonsterNavigator>();
        _attacker  = GetComponent<MonsterAttackHandler>();
        _animator  = GetComponent<Animator>();
        _rb        = GetComponent<Rigidbody2D>();
        _renderer  = GetComponent<SpriteRenderer>();
        _hpBar     = GetComponent<MonsterHPBar>();
        _colliders = GetComponentsInChildren<Collider2D>(true);

        CacheClipDurations();
    }

    private void CacheClipDurations()
    {
        if (_animator != null && _animator.runtimeAnimatorController != null)
        {
            foreach (var clip in _animator.runtimeAnimatorController.animationClips)
            {
                if (clip == null) continue;
                if (clip.name == "Trigger")    _revealDuration = clip.length;
                else if (clip.name == "Death") _deathDuration  = clip.length;
            }
        }
        if (_revealDurationOverride > 0f) _revealDuration = _revealDurationOverride;
    }

    // 풀에서 꺼내질 때마다 매복(Hidden) 상태로 리셋
    protected override void OnEnable()
    {
        base.OnEnable();

        if (_health != null) _health.OnDied += HandleDeath;

        _phase           = Phase.Hidden;
        _attackLockTimer = 0f;
        _deathTimer      = 0f;
        _currentAnim     = null;
        _targetStats     = null;

        if (_animator != null) _animator.speed = 1f;
        if (_renderer != null)
        {
            _baseColor   = _renderer.color;
            _baseColor.a = 1f;
            _renderer.color = _baseColor;
        }

        SetVisible(false);            // 숨김: 렌더러/HP바 off
        SetCollidersEnabled(false);   // 콜라이더 off (Walk=Active 진입 전까지 무적 + 무충돌)
        ForceAnim(AnimIdle, "Idle");  // 보이지 않지만 기본 자세로 대기
    }

    private void OnDisable()
    {
        if (_health != null) _health.OnDied -= HandleDeath;
    }

    // MonsterBase의 BT는 사용하지 않고 단계 상태머신으로 직접 구동
    protected override BTNode BuildBT() => null;

    protected override void Update()
    {
        switch (_phase)
        {
            case Phase.Hidden:    TickHidden();    break;
            case Phase.Revealing: TickRevealing(); break;
            case Phase.IdleDelay: TickIdleDelay(); break;
            case Phase.Active:    TickActive();    break;
            case Phase.Dead:      TickDead();      break;
        }
    }

    // ── 1) Hidden: 플레이어가 감지 범위에 들어오면 등장 ──────────────
    private void TickHidden()
    {
        // 스폰 직후 MobSpawner의 SetMaxHp/Resurrect가 OnHpChanged로 HP바를 다시 켜므로
        // 숨김 단계에서는 매 프레임 렌더러/HP바 비표시를 강제 유지한다.
        if (_renderer != null && _renderer.enabled) _renderer.enabled = false;
        if (_hpBar != null && _hpBar.BarRoot != null && _hpBar.BarRoot.gameObject.activeSelf)
            _hpBar.BarRoot.gameObject.SetActive(false);

        if (_detection.HasTarget)
            EnterRevealing();
    }

    private void EnterRevealing()
    {
        _phase = Phase.Revealing;
        SetVisible(true);
        if (_hpBar != null) _hpBar.SetDisplayRatio(0f); // 등장 연출: 빈 HP바에서 시작
        _navigator.Stop();
        _runtime.CurrentState = MonsterState.Idle;
        FaceTarget();
        UpdateFacing();
        ForceAnim(AnimTrigger, "Trigger");
        _revealTimer = _revealDuration;
    }

    /// <summary>
    /// 외부 소환(네크로맨서 Spell2 등)용: 플레이어 감지 여부와 무관하게
    /// 숨김 단계를 건너뛰고 즉시 등장(Trigger) 연출부터 시작한다.
    /// </summary>
    public void SummonActivate()
    {
        if (_phase == Phase.Hidden)
            EnterRevealing();
    }

    // ── 2) Revealing: 등장 애니 1회 재생 ───────────────────────────
    private void TickRevealing()
    {
        _navigator.Stop();
        FaceTarget();
        UpdateFacing();

        _revealTimer -= Time.deltaTime;

        // 등장 연출: Trigger 재생 동안 HP바가 0 → 1로 차오른다.
        if (_hpBar != null)
        {
            float p = _revealDuration > 0f ? Mathf.Clamp01(1f - _revealTimer / _revealDuration) : 1f;
            _hpBar.SetDisplayRatio(p);
        }

        if (_revealTimer <= 0f)
        {
            if (_hpBar != null) _hpBar.SetDisplayRatio(1f); // 연출 종료: 가득 찬 상태로 마무리
            _phase     = Phase.IdleDelay;
            _idleTimer = _idleDelayAfterReveal;
            PlayAnim(AnimIdle, "Idle");
        }
    }

    // ── 3) IdleDelay: 0.5초 Idle 유지 ──────────────────────────────
    private void TickIdleDelay()
    {
        _navigator.Stop();
        FaceTarget();
        UpdateFacing();

        _idleTimer -= Time.deltaTime;
        if (_idleTimer <= 0f)
        {
            _phase = Phase.Active;
            SetCollidersEnabled(true); // Walk(Active) 진입 시점부터 피격/충돌 가능
        }
    }

    // ── 4) Active: Bear와 동일한 추격/공격 ─────────────────────────
    private void TickActive()
    {
        if (_runtime.IsStaggered) return; // 경직 중에는 행동 정지

        // 쿨다운은 공격 락(자세 유지) 중에도 함께 감소시켜, 락이 끝나면 곧바로 다음 행동으로
        // 이어지게 한다 — 공격 후 사거리 안에서 멍하니 서 있는 시간을 없앤다.
        if (_runtime.AttackCooldownTimer > 0f)
            _runtime.AttackCooldownTimer -= Time.deltaTime;

        if (_attackLockTimer > 0f)
        {
            _attackLockTimer -= Time.deltaTime;
            _navigator.Stop();
            if (_detection.HasTarget) FaceTarget();
            UpdateFacing();
            _runtime.CurrentState = MonsterState.Attack;
            return; // 공격 자세 유지 (Attack 애니 진행 중)
        }

        if (!_detection.HasTarget)
        {
            // 타겟 상실: 제자리 Idle (배회 없음)
            _navigator.Stop();
            _runtime.CurrentState = MonsterState.Idle;
            PlayAnim(AnimIdle, "Idle");
            UpdateFacing();
            return;
        }

        Transform target = _detection.DetectedTarget;

        // 공격: y 정렬 + 가로 사거리 + 쿨다운 0
        if (_runtime.AttackCooldownTimer <= 0f && IsAlignedAndInRange())
        {
            _navigator.Stop();
            FaceTarget();
            UpdateFacing();
            _runtime.CurrentState = MonsterState.Attack;
            ForceAnim(AnimAttack, "Attack");
            _attacker.ExecuteAttack(target.position);
            _attackLockTimer = _attackLockDuration;
            return;
        }

        // 사거리 안이지만 쿨다운 중: 멈춰서 대기 (단, 다른 몹과 겹치면 천천히 벌어진다)
        if (IsAlignedAndInRange())
        {
            FaceTarget();
            UpdateFacing();
            _runtime.CurrentState = MonsterState.Idle;

            Vector2 sep = ComputeSeparation();
            if (sep.sqrMagnitude > 0.04f)
            {
                _runtime.CurrentSpeed = ChaseSpeed() * 0.6f; // 겹침 해소용 느린 이동
                _navigator.MoveInDirection(sep.normalized);
                PlayAnim(AnimWalk, "Walk");
            }
            else
            {
                _navigator.Stop();
                PlayAnim(AnimIdle, "Idle");
            }
            return;
        }

        // 추격: 너무 멀면 포기
        float dist = Vector2.Distance(transform.position, target.position);
        if (dist > _chaseGiveUpDistance)
        {
            _detection.ForceRelease();
            _navigator.Stop();
            _runtime.CurrentState = MonsterState.Idle;
            PlayAnim(AnimIdle, "Idle");
            return;
        }

        _runtime.CurrentState = MonsterState.Chase;
        _runtime.CurrentSpeed = ChaseSpeed(); // 플레이어 이속 × 1.2

        _runtime.AttackCooldownTimer = 0f; // 이동하면 공격 제한 해제 (Bear와 동일)

        // 목표(플레이어 옆) 방향 + 분리(겹침 방지)를 합성해 이동
        Vector2 toPoint  = (Vector2)GetSideApproachPoint(target) - (Vector2)transform.position;
        Vector2 desired  = toPoint.normalized + ComputeSeparation() * _separationWeight;
        if (desired.sqrMagnitude < 0.0001f) desired = toPoint;
        _navigator.MoveInDirection(desired.normalized);
        PlayAnim(AnimWalk, "Walk");
        UpdateFacing();
    }

    // 추격 속도 = 플레이어 이동속도 × _speedFactorVsPlayer (버프로 변하는 속도도 추종).
    private float ChaseSpeed()
    {
        float playerSpeed = _fallbackPlayerSpeed;
        Transform t = _detection.DetectedTarget;
        if (t != null)
        {
            if (_targetStats == null) _targetStats = t.GetComponentInParent<StatSystem>();
            if (_targetStats != null) playerSpeed = _targetStats.TotalMoveSpeed;
        }
        return playerSpeed * _speedFactorVsPlayer;
    }

    // 근처 다른 몹으로부터 밀어내는 분리 벡터(겹침/교착 방지). 가까울수록 강하게 작용.
    private Vector2 ComputeSeparation()
    {
        Vector2 pos = transform.position;
        Vector2 sep = Vector2.zero;
        int n = Physics2D.OverlapCircleNonAlloc(pos, _separationRadius, _separationBuffer, EnemyLayerMask);
        for (int i = 0; i < n; i++)
        {
            var c = _separationBuffer[i];
            if (c == null) continue;
            var orb = c.attachedRigidbody;
            if (orb == null || orb == _rb) continue; // 자기 자신 제외
            Vector2 away = pos - orb.position;
            float d = away.magnitude;
            if (d < 0.0001f) { away = new Vector2(0.01f, 0f); d = 0.01f; }
            if (d < _separationRadius)
                sep += (away / d) * (1f - d / _separationRadius);
        }
        return sep;
    }

    // ── 5) Dead: Death 애니가 "끝난 뒤" 페이드아웃 → 완전 투명 ──────────
    private void TickDead()
    {
        _deathTimer += Time.deltaTime;
        if (_renderer == null) return;

        // Death 애니메이션이 완전히 재생된 뒤부터 페이드 시작
        if (_deathTimer >= _deathDuration)
        {
            float t = _fadeDuration > 0f
                ? Mathf.Clamp01((_deathTimer - _deathDuration) / _fadeDuration)
                : 1f;
            Color c = _baseColor;
            c.a = 1f - t;
            _renderer.color = c;

            // 페이드 완료 시 렌더러를 꺼서 완전 투명을 보장(플래시 셰이더가 알파를 무시해도 안전).
            if (t >= 1f) _renderer.enabled = false;
        }
    }

    private void HandleDeath()
    {
        if (_phase == Phase.Dead) return;
        _phase = Phase.Dead;

        _runtime.CurrentState = MonsterState.Death;
        _runtime.IsStaggered  = false;        // 사망 직전 피격 경직(히트스탑) 해제
        _attackLockTimer      = 0f;
        _attacker.CancelAttack();
        _navigator.Stop();

        if (_animator != null) _animator.speed = 1f; // OnHit 히트스탑(speed=0) 복구
        ForceAnim(AnimDeath, "Death");

        SetCollidersEnabled(false);           // 충돌 처리 끔

        _deathTimer = 0f;
        if (_renderer != null) _baseColor = _renderer.color;
    }

    // ══════════════════════════════════════════════════════════════
    //  피격 반응 (Bear와 동일): 공격 중단 + 넉백
    // ══════════════════════════════════════════════════════════════
    public override void TakeDamage(float damage, GameObject source = null)
    {
        bool wasAlive = IsAlive;
        base.TakeDamage(damage, source);

        if (!wasAlive || !IsAlive) return;    // 사망은 HandleDeath가 처리
        if (_phase == Phase.Hidden || _phase == Phase.Dead) return;

        if (_phase == Phase.Active)
            InterruptAttack();
        ApplyKnockback(source);
    }

    private void InterruptAttack()
    {
        _attackLockTimer = 0f;
        _attacker.CancelAttack();
        ForceAnim(AnimIdle, "Idle"); // 진행 중 공격 애니 초기화 (경직 중 speed=0이라 프레임 고정)
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
            float fx = _runtime.CurrentDirection.x;
            dir = new Vector2(fx >= 0f ? -1f : 1f, 0f);
        }

        if (_rb != null)
            _rb.MovePosition(_rb.position + dir * _knockbackDistance);
    }

    // ══════════════════════════════════════════════════════════════
    //  헬퍼
    // ══════════════════════════════════════════════════════════════
    private float HorizontalAttackRange
    {
        get
        {
            if (_horizontalAttackRange > 0f) return _horizontalAttackRange;
            if (_runtime.Data != null && _runtime.Data.AttackShape.ShapeType == AttackShapeType.Rectangle)
                return _runtime.Data.AttackShape.RectWidth;
            if (_runtime.Data != null && _runtime.Data.AttackShape.ShapeType == AttackShapeType.Circle)
                return _runtime.Data.AttackShape.CircleRadius;
            return 1.2f;
        }
    }

    private float SideStandoff => _sideStandoff > 0f ? _sideStandoff : HorizontalAttackRange * 0.7f;

    private bool IsAlignedAndInRange()
    {
        Transform target = _detection.DetectedTarget;
        if (target == null) return false;

        float dx = target.position.x - transform.position.x;
        float dy = target.position.y - transform.position.y;
        return Mathf.Abs(dy) <= _yAlignThreshold && Mathf.Abs(dx) <= HorizontalAttackRange;
    }

    private Vector2 GetSideApproachPoint(Transform target)
    {
        float dx = target.position.x - transform.position.x;
        if (Mathf.Abs(dx) > 0.1f)
            _approachSide = dx >= 0f ? -1f : 1f;

        return new Vector2(target.position.x + _approachSide * SideStandoff, target.position.y);
    }

    private void FaceTarget()
    {
        Transform target = _detection.DetectedTarget;
        if (target == null) return;
        float dx = target.position.x - transform.position.x;
        if (Mathf.Abs(dx) > 0.01f)
            _runtime.CurrentDirection = new Vector2(Mathf.Sign(dx), 0f);
    }

    // 이동 방향이 아니라 '플레이어' 쪽을 바라본다. 데드존(히스테리시스)으로
    // 거의 정렬됐을 때의 미세한 dx 부호 흔들림에 의한 flipX 깜빡임을 막는다.
    private void UpdateFacing()
    {
        if (_renderer == null) return;

        if (_detection.HasTarget)
        {
            float dx = _detection.DetectedTarget.position.x - transform.position.x;
            if (dx > FacingDeadzone)      _facingRight = true;
            else if (dx < -FacingDeadzone) _facingRight = false;
            // 데드존 안이면 방향 유지 (깜빡임 방지)
        }

        _renderer.flipX = _spriteFacesRight ? !_facingRight : _facingRight;
    }

    // ── 가시성/충돌 토글 ───────────────────────────────────────────
    // 렌더러/HP바만 토글한다. 콜라이더(피격/충돌)는 별도로 SetCollidersEnabled로 관리하여
    // 등장 연출 동안에는 보이되 맞지 않게(콜라이더 off) 한다.
    private void SetVisible(bool visible)
    {
        if (_renderer != null) _renderer.enabled = visible;
        if (_hpBar != null && _hpBar.BarRoot != null)
            _hpBar.BarRoot.gameObject.SetActive(visible);
    }

    private void SetCollidersEnabled(bool enabled)
    {
        if (_colliders == null) return;
        foreach (var c in _colliders)
            if (c != null) c.enabled = enabled;
    }

    // ── 애니메이션 재생 (상태 변경 시에만) ─────────────────────────
    private void PlayAnim(int hash, string name)
    {
        if (_currentAnim == name) return;
        _currentAnim = name;
        if (_animator != null) _animator.Play(hash, 0, 0f);
    }

    // 같은 상태라도 강제로 처음부터 재생
    private void ForceAnim(int hash, string name)
    {
        _currentAnim = name;
        if (_animator != null) _animator.Play(hash, 0, 0f);
    }
}
