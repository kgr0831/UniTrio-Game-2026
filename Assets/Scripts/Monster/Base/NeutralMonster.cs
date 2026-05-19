using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 중립 몹 (타일 기반 배회 + A* 경로 도망).
/// 모든 이동은 타일 중앙 → 타일 중앙으로만 진행됨.
/// </summary>
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

    // ── Flee State ──
    private List<Vector2Int> _fleePath;
    private int              _fleePathIndex;
    private float            _fleeTimer;
    private bool             _isFleeing;
    private float            _fleeCooldown;
    private const float      FLEE_COOLDOWN_TIME = 0.5f;

    // ── Player Alert State ──
    private float _playerAlertTimer;

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
            _runtime.CurrentState = MonsterState.Idle;
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
        bool reached = _navigator.MoveToTileCenter(target.Value, _tileReachThreshold);

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

        if (_isFleeing && HasNearbyThreat())
            _fleeTimer = _fleeTimeout;

        if (!_isFleeing || _fleePath == null)
        {
            if (!GenerateFleePath())
            {
                Debug.LogWarning("[NeutralMonster] GenerateFleePath FAILED! Ending flee.");
                EndFlee();
                _fleeCooldown = FLEE_COOLDOWN_TIME;
                return BTStatus.Success;
            }
            Debug.Log($"[NeutralMonster] Flee path generated: {_fleePath.Count} tiles");
        }

        _fleeTimer -= Time.deltaTime;
        if (_fleeTimer <= 0f)
        {
            EndFlee();
            return BTStatus.Success;
        }

        return FollowFleePath();
    }

    private bool GenerateFleePath()
    {
        _navigator.SnapToTileCenter();
        Vector2Int currentTile = TileGridHelper.WorldToTile(transform.position);

        Vector2 combinedThreatPos = GatherCombinedThreatPosition();

        _fleePath = FleePathGenerator.GenerateFleePath(
            currentTile, combinedThreatPos, _obstacleMask, _threatMask);

        if (_fleePath == null || _fleePath.Count < 2)
            return false;

        _fleePathIndex = 1;
        _fleeTimer = _fleeTimeout;
        _isFleeing = true;
        return true;
    }

    private Vector2 GatherCombinedThreatPosition()
    {
        Transform mainThreat = _detection.DetectedTarget;
        Vector2 sum = (Vector2)mainThreat.position;
        int count = 1;

        float radius = _runtime.Data.DetectionRadius * 1.5f;
        int hitCount = Physics2D.OverlapCircleNonAlloc(
            transform.position, radius, _threatBuffer, _threatMask);

        for (int i = 0; i < hitCount; i++)
        {
            Transform root = _threatBuffer[i].transform.root;
            if (root == transform) continue;
            if (root == mainThreat) continue;

            // 중립 몹은 위협이 아님 — 적대 몹과 플레이어만 위협으로 카운트
            var runtimeData = root.GetComponent<MonsterRuntimeData>();
            if (runtimeData != null && runtimeData.Type == MonsterType.Neutral)
                continue;

            sum += (Vector2)root.position;
            count++;
        }

        return sum / count;
    }

    private bool HasNearbyThreat()
    {
        float radius = _runtime.Data.DetectionRadius;
        int hitCount = Physics2D.OverlapCircleNonAlloc(
            transform.position, radius, _threatBuffer, _threatMask);

        for (int i = 0; i < hitCount; i++)
        {
            Transform root = _threatBuffer[i].transform.root;
            if (root == transform) continue;

            var runtimeData = root.GetComponent<MonsterRuntimeData>();
            if (runtimeData != null && runtimeData.Type == MonsterType.Neutral)
                continue;

            return true;
        }
        return false;
    }

    private BTStatus FollowFleePath()
    {
        if (_fleePathIndex >= _fleePath.Count)
        {
            EndFlee();
            return BTStatus.Success;
        }

        Vector2 tileCenter = TileGridHelper.TileToWorld(_fleePath[_fleePathIndex]);
        bool reached = _navigator.MoveToTileCenter(tileCenter, _tileReachThreshold);

        if (reached)
            _fleePathIndex++;

        return BTStatus.Running;
    }

    private void EndFlee()
    {
        _isFleeing = false;
        _fleePath = null;
        _fleePathIndex = 0;
        _fleeTimer = 0f;

        _detection.ForceRelease();
        _navigator.SnapToTileCenter();

        _wander.SetBasePosition(transform.position);
        _wander.ForceRecalculate();
    }

    // ══════════════════════════════════════════════════════════════════
    //  Player Alert (피격 후 10초간 플레이어 감지)
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
        Debug.Log($"[NeutralMonster] TakeDamage called. source={source?.name ?? "NULL"}, isFleeing={_isFleeing}");

        if (source == null) return;

        Transform threatRoot = null;

        var monsterData = source.GetComponentInParent<MonsterRuntimeData>();
        if (monsterData != null)
        {
            threatRoot = monsterData.transform;
            Debug.Log($"[NeutralMonster] Threat is monster: {threatRoot.name}");
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

            Debug.Log($"[NeutralMonster] Threat is player: {threatRoot?.name ?? "NOT FOUND"}");

            if (threatRoot != null)
                _playerAlertTimer = _playerAlertDuration;
        }

        if (threatRoot == null)
        {
            Debug.LogWarning("[NeutralMonster] No threat found! Cannot flee.");
            return;
        }

        _fleeCooldown = 0f;

        if (_isFleeing)
        {
            _fleeTimer = _fleeTimeout;
            Debug.Log("[NeutralMonster] Already fleeing, timer reset.");
        }
        else
        {
            _detection.ForceDetect(threatRoot);
            Debug.Log($"[NeutralMonster] ForceDetect → {threatRoot.name}, HasTarget={_detection.HasTarget}");
        }
    }
}
