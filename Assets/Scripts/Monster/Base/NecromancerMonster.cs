using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 네크로맨서: 제자리 원거리 캐스터.
///
/// - 플레이어를 감지하면 거리에 따라 두 스펠을 시전한다.
///   · 플레이어가 _spell2Radius(스켈레톤 감지 반경) 이내 → Spell2: 양 옆에 스켈레톤 소환.
///   · 그보다 멀고 _spell1Radius(활 사거리/1.5) 이내 → Spell1: 플레이어 방향으로 파이어볼 발사.
/// - 실제 효과(발사/소환)는 스펠 애니메이션의 Animation Event(OnSpell1Cast/OnSpell2Cast) 프레임에 발생한다.
/// - 플레이어를 바라보며 flipX (기본 flip 안 한 상태 = 왼쪽).
/// - 피격/사망 반응은 스켈레톤과 동일(넉백 + 시전 중단 / Death 애니 후 페이드아웃 → 완전 투명).
/// </summary>
[RequireComponent(typeof(DetectionSystem))]
[RequireComponent(typeof(MonsterNavigator))]
public sealed class NecromancerMonster : MonsterBase
{
    [Header("Spell 거리")]
    [Tooltip("플레이어가 이 반경 이내이면 Spell2(소환). 보통 스켈레톤 감지 반경(6).")]
    [SerializeField] private float _spell2Radius = 6f;
    [Tooltip("플레이어가 _spell2Radius보다 멀고 이 반경 이내이면 Spell1(파이어볼). 보통 활 사거리(10)/1.5.")]
    [SerializeField] private float _spell1Radius = 10f / 1.5f;

    [Header("Spell 쿨다운")]
    [SerializeField] private float _spell1Cooldown = 2.5f;
    [SerializeField] private float _spell2Cooldown = 7f;

    [Header("Spell1 - Fireball")]
    [SerializeField] private GameObject _fireballPrefab;
    [SerializeField] private float _fireballSpeed       = 15f;
    [SerializeField] private float _fireballMaxDistance = 10f;
    [SerializeField] private float _fireballDamage      = 20f;
    [Tooltip("파이어볼 생성 위치를 발사 방향으로 살짝 띄우는 거리.")]
    [SerializeField] private float _fireballSpawnDistance = 0.6f;
    [Tooltip("파이어볼 스프라이트 기본 방향 보정각(도). 스프라이트가 +X(오른쪽)를 향하면 0.")]
    [SerializeField] private float _fireballSpriteAngleOffset = 0f;

    [Header("Spell2 - Summon")]
    [SerializeField] private GameObject  _skeletonPrefab;
    [SerializeField] private MonsterData _skeletonData;
    [Tooltip("네크로맨서 양 옆으로 스켈레톤을 소환할 가로 거리.")]
    [SerializeField] private float _summonSideOffset = 1.8f;
    [Tooltip("동시에 살아있을 수 있는 소환 스켈레톤 최대 수.")]
    [SerializeField] private int   _maxSummon = 2;
    [Tooltip("소환 스켈레톤 사망 후 풀 반환까지의 딜레이(Death 애니/페이드 보장).")]
    [SerializeField] private float _summonDeathReturnDelay = 3.5f;

    [Header("Movement (도망/추적)")]
    [Tooltip("플레이어가 이 거리보다 가까우면 도망(반대 방향으로 이동).")]
    [SerializeField] private float _fleeRadius = 3.5f;
    [Tooltip("도망/추적 이동 속도(초당).")]
    [SerializeField] private float _moveSpeed = 4f;

    [Header("Facing")]
    [Tooltip("스프라이트 기본 방향이 오른쪽이면 true. 네크로맨서는 기본 왼쪽이므로 false.")]
    [SerializeField] private bool _spriteFacesRight = false;

    [Header("Hit / Death (스켈레톤과 동일)")]
    [SerializeField] private float _knockbackDistance = 0.5f;
    [Tooltip("Death 애니가 끝난 뒤 완전 투명까지 페이드 시간(초).")]
    [SerializeField] private float _fadeDuration = 1f;

    private DetectionSystem  _detection;
    private MonsterNavigator _navigator;
    private Animator         _animator;
    private Rigidbody2D     _rb;
    private SpriteRenderer  _renderer;
    private MonsterHPBar    _hpBar;
    private Collider2D[]    _colliders;

    private bool   _dead;
    private float  _castLockTimer;
    private float  _spell1Timer;
    private float  _spell2Timer;
    private bool   _facingRight = true;
    private string _currentAnim;

    private float _spell1Duration = 1.58f;
    private float _spell2Duration = 1.08f;
    private float _deathDuration  = 1.0f;

    private float _deathTimer;
    private Color _baseColor = Color.white;

    private readonly List<GameObject> _summoned = new List<GameObject>();

    private const float FacingDeadzone = 0.35f;

    private static readonly int AnimIdle   = Animator.StringToHash("Idle");
    private static readonly int AnimWalk   = Animator.StringToHash("Walk");
    private static readonly int AnimSpell1 = Animator.StringToHash("Spell1");
    private static readonly int AnimSpell2 = Animator.StringToHash("Spell2");
    private static readonly int AnimDeath  = Animator.StringToHash("Death");

    protected override void Awake()
    {
        base.Awake();
        _detection = GetComponent<DetectionSystem>();
        _navigator = GetComponent<MonsterNavigator>();
        _animator  = GetComponent<Animator>();
        _rb        = GetComponent<Rigidbody2D>();
        _renderer  = GetComponent<SpriteRenderer>();
        _hpBar     = GetComponent<MonsterHPBar>();
        _colliders = GetComponentsInChildren<Collider2D>(true);
        CacheClipDurations();
    }

    private void CacheClipDurations()
    {
        if (_animator == null || _animator.runtimeAnimatorController == null) return;
        foreach (var clip in _animator.runtimeAnimatorController.animationClips)
        {
            if (clip == null) continue;
            if (clip.name == "Spell1")      _spell1Duration = clip.length;
            else if (clip.name == "Spell2") _spell2Duration = clip.length;
            else if (clip.name == "Death")  _deathDuration  = clip.length;
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (_health != null) _health.OnDied += HandleDeath;

        _dead          = false;
        _castLockTimer = 0f;
        _spell1Timer   = 0f;
        _spell2Timer   = 0f;
        _deathTimer    = 0f;
        _currentAnim   = null;
        _summoned.Clear();

        if (_animator != null) _animator.speed = 1f;
        if (_renderer != null)
        {
            _renderer.enabled = true;
            _baseColor = _renderer.color; _baseColor.a = 1f; _renderer.color = _baseColor;
        }
        SetCollidersEnabled(true);
        ForceAnim(AnimIdle, "Idle");
    }

    private void OnDisable()
    {
        if (_health != null) _health.OnDied -= HandleDeath;
    }

    protected override BTNode BuildBT() => null;

    protected override void Update()
    {
        // 회피 저스트 카운터 중에는 모든 행동 정지
        if (MonsterFreezeManager.IsFrozen) return;

        if (_dead) { TickDead(); return; }
        if (_health == null || !_health.IsAlive) return;
        if (_runtime.IsStaggered) return;

        if (_castLockTimer > 0f) { _castLockTimer -= Time.deltaTime; _navigator.Stop(); return; } // 시전 중 정지
        if (_spell1Timer > 0f) _spell1Timer -= Time.deltaTime;
        if (_spell2Timer > 0f) _spell2Timer -= Time.deltaTime;

        if (!_detection.HasTarget) { _navigator.Stop(); PlayAnim(AnimIdle, "Idle"); return; }

        Transform target = _detection.DetectedTarget;
        UpdateFacing();
        Vector2 self = transform.position;
        Vector2 ppos = target.position;
        float dist = Vector2.Distance(self, ppos);

        // ── 너무 가까우면 도망(플레이어 반대 방향으로 이동) ──
        if (dist < _fleeRadius)
        {
            Vector2 away = self - ppos;
            if (away.sqrMagnitude < 0.0001f) away = Vector2.right;
            _runtime.CurrentState = MonsterState.Flee;
            _runtime.CurrentSpeed = _moveSpeed;
            _navigator.MoveInDirection(away.normalized);
            PlayAnim(AnimWalk, "Walk");
            return;
        }

        // ── 소환 사거리: 제자리에서 Spell2 ──
        if (dist <= _spell2Radius)
        {
            _navigator.Stop();
            if (_spell2Timer <= 0f && CanSummon()) CastSpell2();
            else PlayAnim(AnimIdle, "Idle");
            return;
        }

        // ── 파이어볼 사거리: 제자리에서 Spell1 ──
        if (dist <= _spell1Radius)
        {
            _navigator.Stop();
            if (_spell1Timer <= 0f) CastSpell1();
            else PlayAnim(AnimIdle, "Idle");
            return;
        }

        // ── 시전 사거리 밖(감지 범위 안): 추적(플레이어 쪽으로 접근) ──
        _runtime.CurrentState = MonsterState.Chase;
        _runtime.CurrentSpeed = _moveSpeed;
        _navigator.MoveInDirection((ppos - self).normalized);
        PlayAnim(AnimWalk, "Walk");
    }

    // ── 시전 ───────────────────────────────────────────────────────
    private void CastSpell1()
    {
        _runtime.CurrentState = MonsterState.Attack;
        ForceAnim(AnimSpell1, "Spell1");
        _castLockTimer = _spell1Duration;
        _spell1Timer   = _spell1Cooldown;
    }

    private void CastSpell2()
    {
        _runtime.CurrentState = MonsterState.Attack;
        ForceAnim(AnimSpell2, "Spell2");
        _castLockTimer = _spell2Duration;
        _spell2Timer   = _spell2Cooldown;
    }

    // Spell1 애니메이션 이벤트(표시 프레임)에서 호출 — 플레이어 방향으로 파이어볼 발사
    public void OnSpell1Cast()
    {
        if (_dead || _fireballPrefab == null || SimpleObjectPool.Instance == null) return;

        Vector2 origin = transform.position;
        Transform target = _detection != null ? _detection.DetectedTarget : null;
        Vector2 dir = target != null ? ((Vector2)target.position - origin) : (_facingRight ? Vector2.right : Vector2.left);
        if (dir.sqrMagnitude < 0.0001f) dir = _facingRight ? Vector2.right : Vector2.left;
        dir.Normalize();

        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + _fireballSpriteAngleOffset;
        Vector2 spawnPos = origin + dir * _fireballSpawnDistance;

        var go = SimpleObjectPool.Instance.Get(_fireballPrefab, spawnPos, Quaternion.Euler(0f, 0f, ang));
        var fb = go.GetComponent<NecromancerFireball>();
        if (fb != null)
        {
            fb.SetStats(_fireballSpeed, _fireballDamage, _fireballMaxDistance);
            fb.SetOwner(gameObject);
        }
    }

    // Spell2 애니메이션 이벤트(표시 프레임)에서 호출 — 양 옆에 스켈레톤 소환
    public void OnSpell2Cast()
    {
        if (_dead || _skeletonPrefab == null || _skeletonData == null || SimpleObjectPool.Instance == null) return;

        PruneSummoned();
        // 왼쪽 → 오른쪽 순으로 최대치까지 채운다
        foreach (float side in new[] { -1f, 1f })
        {
            if (_summoned.Count >= _maxSummon) break;
            Vector2 pos = (Vector2)transform.position + new Vector2(side * _summonSideOffset, 0f);
            SummonSkeleton(pos);
        }
    }

    private void SummonSkeleton(Vector2 pos)
    {
        var go = SimpleObjectPool.Instance.Get(_skeletonPrefab, pos, Quaternion.identity);

        var rd = go.GetComponent<MonsterRuntimeData>();
        if (rd != null) rd.Initialize(_skeletonData);

        var hp = go.GetComponent<HealthSystem>();
        if (hp != null) { hp.SetMaxHp(_skeletonData.MaxHP, true); hp.Resurrect(); }

        var mb = go.GetComponent<MonsterBase>();
        if (mb != null && MobManager.Instance != null) MobManager.Instance.RegisterMob(mb);

        var skel = go.GetComponent<SkeletonMonster>();
        if (skel != null) skel.SummonActivate();

        _summoned.Add(go);

        // 사망 시: 추적 제거 + 매니저 해제 + 딜레이 후 풀 반환(Death 애니/페이드 보장)
        if (hp != null)
        {
            System.Action onDied = null;
            onDied = () =>
            {
                hp.OnDied -= onDied;
                _summoned.Remove(go);
                if (mb != null && MobManager.Instance != null) MobManager.Instance.UnregisterMob(mb);
                StartCoroutine(ReleaseAfter(go, _summonDeathReturnDelay));
            };
            hp.OnDied += onDied;
        }
    }

    private IEnumerator ReleaseAfter(GameObject go, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (go != null && go.activeSelf && SimpleObjectPool.Instance != null)
            SimpleObjectPool.Instance.Release(go);
    }

    private bool CanSummon()
    {
        PruneSummoned();
        return _summoned.Count < _maxSummon;
    }

    private void PruneSummoned()
    {
        for (int i = _summoned.Count - 1; i >= 0; i--)
        {
            var g = _summoned[i];
            if (g == null || !g.activeInHierarchy) _summoned.RemoveAt(i);
        }
    }

    // ── 피격 반응 (스켈레톤과 동일): 시전 중단 + 넉백 ───────────────
    public override void TakeDamage(float damage, GameObject source = null)
    {
        bool wasAlive = IsAlive;
        base.TakeDamage(damage, source);
        if (!wasAlive || !IsAlive || _dead) return;

        _castLockTimer = 0f;
        ForceAnim(AnimIdle, "Idle"); // 진행 중 시전 애니 초기화(경직 중 speed=0이라 프레임 고정)
        ApplyKnockback(source);
    }

    private void ApplyKnockback(GameObject source)
    {
        Vector2 dir = Vector2.zero;
        if (source != null)
        {
            Vector2 d = (Vector2)transform.position - (Vector2)source.transform.position;
            if (d.sqrMagnitude > 0.0001f) dir = d.normalized;
        }
        if (dir.sqrMagnitude < 0.0001f) dir = _facingRight ? Vector2.left : Vector2.right;
        if (_rb != null) _rb.MovePosition(_rb.position + dir * _knockbackDistance);
    }

    // ── 사망 (스켈레톤과 동일): Death 애니 후 페이드 → 완전 투명 ────
    private void HandleDeath()
    {
        if (_dead) return;
        _dead = true;

        _runtime.CurrentState = MonsterState.Death;
        _runtime.IsStaggered  = false;
        _castLockTimer        = 0f;

        if (_animator != null) _animator.speed = 1f;
        ForceAnim(AnimDeath, "Death");

        SetCollidersEnabled(false);
        _deathTimer = 0f;
        if (_renderer != null) _baseColor = _renderer.color;
    }

    private void TickDead()
    {
        _deathTimer += Time.deltaTime;
        if (_renderer == null) return;

        if (_deathTimer >= _deathDuration)
        {
            float t = _fadeDuration > 0f
                ? Mathf.Clamp01((_deathTimer - _deathDuration) / _fadeDuration)
                : 1f;
            Color c = _baseColor; c.a = 1f - t;
            _renderer.color = c;
            if (t >= 1f) _renderer.enabled = false;
        }
    }

    // ── 헬퍼 ───────────────────────────────────────────────────────
    private void UpdateFacing()
    {
        if (_renderer == null) return;
        if (_detection.HasTarget)
        {
            float dx = _detection.DetectedTarget.position.x - transform.position.x;
            if (dx > FacingDeadzone)       _facingRight = true;
            else if (dx < -FacingDeadzone) _facingRight = false;
        }
        _renderer.flipX = _spriteFacesRight ? !_facingRight : _facingRight;
    }

    private void SetCollidersEnabled(bool enabled)
    {
        if (_colliders == null) return;
        foreach (var c in _colliders) if (c != null) c.enabled = enabled;
    }

    private void PlayAnim(int hash, string name)
    {
        if (_currentAnim == name) return;
        _currentAnim = name;
        if (_animator != null) _animator.Play(hash, 0, 0f);
    }

    private void ForceAnim(int hash, string name)
    {
        _currentAnim = name;
        if (_animator != null) _animator.Play(hash, 0, 0f);
    }
}
