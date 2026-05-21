using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(DetectionSystem))]
[RequireComponent(typeof(MonsterNavigator))]
[RequireComponent(typeof(WanderSystem))]
public sealed class NeutralMonster : MonsterBase
{
    [Header("Flee Settings")]
    [SerializeField] private LayerMask _obstacleMask;
    [SerializeField] private LayerMask _threatMask;
    [SerializeField] private float _fleeSpeedMultiplier = 1.8f;
    [SerializeField] private float _fleeTimeout = 3f;
    [SerializeField] private float _tileReachThreshold = 0.3f;

    [Header("Player Alert")]
    [SerializeField] private float _playerAlertDuration = 10f;
    [SerializeField] private LayerMask _playerMask;

    private DetectionSystem  _detection;
    private MonsterNavigator _navigator;
    private WanderSystem     _wander;

    private List<Vector2Int> _fleePath;
    private int              _fleePathIndex;
    private float            _fleeTimer;
    private bool             _isFleeing;
    private float            _fleeCooldown;
    private const float      FLEE_COOLDOWN_TIME = 0.5f;

    private float _playerAlertTimer;
    private bool  _needsFleePathRegen;
    private float _pathRegenCooldown;
    private const float PATH_REGEN_COOLDOWN = 0.3f;

    private readonly Collider2D[] _threatBuffer = new Collider2D[16];

    protected override void Awake()
    {
        base.Awake();
        _detection = GetComponent<DetectionSystem>();
        _navigator = GetComponent<MonsterNavigator>();
        _wander    = GetComponent<WanderSystem>();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        _navigator.SnapToTileCenter();
        _playerAlertTimer = 0f;
        _fleeCooldown = 0f;
        _isFleeing = false;
        _needsFleePathRegen = false;
    }

    protected override BTNode BuildBT()
    {
        var checkFlee = new BTCondition(() => _detection.HasTarget && _fleeCooldown <= 0f);
        var fleeAction = new BTAction(ExecuteFlee);
        var fleeSeq = new BTSequence(new BTNode[] { checkFlee, fleeAction });

        var wanderAction = new BTAction(ExecuteWander);

        return new BTSelector(new BTNode[] { fleeSeq, wanderAction });
    }

    protected override void Update()
    {
        base.Update();

        if (_fleeCooldown > 0f)
            _fleeCooldown -= Time.deltaTime;

        if (_playerAlertTimer > 0f)
        {
            _playerAlertTimer -= Time.deltaTime;
            if (!_detection.HasTarget)
                TryDetectPlayerDuringAlert();
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  Wander
    // ══════════════════════════════════════════════════════════════════

    private BTStatus ExecuteWander()
    {
        if (_runtime.Data != null)
            _runtime.CurrentSpeed = _runtime.Data.Speed * 0.01f * 0.5f;

        if (_wander.TickIdle())
        {
            _runtime.CurrentState = _wander.IsEating ? MonsterState.Eat : MonsterState.Idle;
            _navigator.Decelerate();

            return BTStatus.Running;
        }

        if (!_wander.EnsurePath(_obstacleMask))
        {
            _wander.StartIdle();
            _runtime.CurrentState = MonsterState.Idle;
            _navigator.Decelerate();
            return BTStatus.Running;
        }

        Vector2? target = _wander.GetCurrentTileTarget();
        if (!target.HasValue)
        {
            _wander.StartIdle();
            _navigator.Decelerate();
            return BTStatus.Running;
        }

        _runtime.CurrentState = MonsterState.Wander;
        bool reached = _navigator.MoveToTarget(target.Value, _tileReachThreshold);

        if (reached)
        {
            if (!_wander.AdvanceToNextTile())
            {
                _wander.StartIdle();
            }
            else if (Random.value < _wander.IdleChance)
            {
                _wander.StartIdle();
            }
        }

        return BTStatus.Running;
    }

    // ══════════════════════════════════════════════════════════════════
    //  Flee
    // ══════════════════════════════════════════════════════════════════

    private BTStatus ExecuteFlee()
    {
        _runtime.CurrentState = MonsterState.Flee;

        if (_runtime.Data != null)
        {
            float baseSpeed = _runtime.Data.Speed * 0.01f;
            float elapsed = _fleeTimeout - _fleeTimer;
            float t = Mathf.Clamp01(elapsed / _fleeTimeout);
            float speedCurve = Mathf.Lerp(_fleeSpeedMultiplier, 1f, t * t);
            _runtime.CurrentSpeed = baseSpeed * speedCurve;
        }

        Transform threat = _detection.DetectedTarget;
        if (threat == null)
        {
            EndFlee();
            return BTStatus.Success;
        }

        Vector2Int threatTile = TileGridHelper.WorldToTile(threat.position);

        if (_isFleeing && TileGridHelper.ChebyshevDistance(
                TileGridHelper.WorldToTile(transform.position), threatTile) <= 3)
            _fleeTimer = _fleeTimeout;

        if (!_isFleeing || _fleePath == null || _needsFleePathRegen)
        {
            _needsFleePathRegen = false;
            if (!GenerateFleePath(threat))
            {
                EndFlee();
                _fleeCooldown = FLEE_COOLDOWN_TIME;
                return BTStatus.Success;
            }
        }

        _pathRegenCooldown -= Time.deltaTime;
        if (_isFleeing && _pathRegenCooldown <= 0f && IsPathBlockedByThreat(threat))
        {
            if (!GenerateFleePath(threat))
            {
                EndFlee();
                return BTStatus.Success;
            }
            _pathRegenCooldown = PATH_REGEN_COOLDOWN;
        }

        if (_fleePath == null)
        {
            EndFlee();
            return BTStatus.Success;
        }

        _fleeTimer -= Time.deltaTime;
        if (_fleeTimer <= 0f)
        {
            EndFlee();
            return BTStatus.Success;
        }

        return FollowFleePath();
    }

    private bool GenerateFleePath(Transform threat)
    {
        Vector2Int currentTile = TileGridHelper.WorldToTile(transform.position);
        Vector2 threatPos = (Vector2)threat.position;
        Vector2Int threatTile = TileGridHelper.WorldToTile(threatPos);

        var threatTiles = new List<Vector2Int>(1) { threatTile };

        _fleePath = FleePathGenerator.GenerateFleePath(
            currentTile, threatPos, _obstacleMask, _threatMask, threatTiles);

        if (_fleePath == null || _fleePath.Count < 2)
            return false;

        _fleePathIndex = 1;
        _fleeTimer = _fleeTimeout;
        _isFleeing = true;
        return true;
    }

    private BTStatus FollowFleePath()
    {
        if (_fleePathIndex >= _fleePath.Count)
        {
            EndFlee();
            return BTStatus.Success;
        }

        Vector2 tileCenter = TileGridHelper.TileToWorld(_fleePath[_fleePathIndex]);
        bool reached = _navigator.MoveToTarget(tileCenter, _tileReachThreshold);

        if (reached)
            _fleePathIndex++;

        return BTStatus.Running;
    }

    private bool IsPathBlockedByThreat(Transform threat)
    {
        Vector2Int threatTile = TileGridHelper.WorldToTile(threat.position);
        int lookAhead = Mathf.Min(_fleePathIndex + 4, _fleePath.Count);
        for (int i = _fleePathIndex; i < lookAhead; i++)
        {
            if (TileGridHelper.ChebyshevDistance(_fleePath[i], threatTile) <= 2)
                return true;
        }
        return false;
    }

    private void EndFlee()
    {
        _isFleeing = false;
        _fleePath = null;
        _fleePathIndex = 0;
        _fleeTimer = 0f;
        _needsFleePathRegen = false;

        _detection.ForceRelease();

        _wander.SetBasePosition(transform.position);
        _wander.ForceRecalculate();
    }

    // ══════════════════════════════════════════════════════════════════
    //  Player Alert
    // ══════════════════════════════════════════════════════════════════

    private void TryDetectPlayerDuringAlert()
    {
        float radius = _runtime.Data.DetectionRadius;
        int hitCount = Physics2D.OverlapCircleNonAlloc(
            transform.position, radius, _threatBuffer, _playerMask);

        for (int i = 0; i < hitCount; i++)
        {
            Transform root = _threatBuffer[i].transform.root;
            if (root.CompareTag("Player"))
            {
                _fleeCooldown = 0f;
                _detection.ForceDetect(root);
                return;
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  Damage → Flee Trigger
    // ══════════════════════════════════════════════════════════════════

    public override void TakeDamage(float damage, GameObject source = null)
    {
        base.TakeDamage(damage, source);

        if (source == null) return;

        Transform threatRoot = null;

        var monsterData = source.GetComponentInParent<MonsterRuntimeData>();
        if (monsterData != null)
        {
            threatRoot = monsterData.transform;
        }
        else
        {
            threatRoot = source.GetComponentInParent<PlayerEntity>()?.transform;
            if (threatRoot == null)
            {
                var playerObj = GameObject.FindWithTag("Player");
                if (playerObj != null)
                    threatRoot = playerObj.transform;
            }

            if (threatRoot != null)
                _playerAlertTimer = _playerAlertDuration;
        }

        if (threatRoot == null) return;

        _fleeCooldown = 0f;

        if (_isFleeing)
        {
            _fleeTimer = _fleeTimeout;
            _needsFleePathRegen = true;
        }
        else
        {
            _detection.ForceDetect(threatRoot);
        }
    }
}
