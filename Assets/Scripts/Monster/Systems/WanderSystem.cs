using System.Collections.Generic;
using UnityEngine;

public sealed class WanderSystem : MonoBehaviour
{
    [Header("벡터 배회 (기존)")]
    [SerializeField] private float _minWanderTime = 1f;
    [SerializeField] private float _maxWanderTime = 3f;
    [SerializeField] private float _wanderRadius = 3f;

    [Header("타일 배회")]
    [SerializeField] private int _tileWanderRange = 4;
    [SerializeField] private float _idleChance = 0.4f;

    [Header("동적 장애물 회피")]
    [Tooltip("경로 회피 대상 레이어 (동물, 에너미, 플레이어 등)")]
    [SerializeField] private LayerMask _dynamicObstacleMask;

    [Header("Eat 애니메이션")]
    [SerializeField] private float _eatAnimDuration = 1.17f;
    [SerializeField] private int _minEatCount = 1;
    [SerializeField] private int _maxEatCount = 3;

    private float   _wanderTimer;
    private Vector2 _currentWanderDirection;
    private Vector2 _basePosition;

    private List<Vector2Int> _tilePath;
    private int              _tilePathIndex;
    private float            _idleTimer;
    private bool             _isIdling;
    private bool             _isEating;

    private float _dynamicCheckTimer;
    private const float DYNAMIC_CHECK_INTERVAL = 0.3f;

    private readonly Collider2D[] _dynamicBuffer = new Collider2D[32];

    public bool IsIdling => _isIdling;
    public bool IsEating => _isEating;
    public float IdleChance => _idleChance;

    private void OnEnable()
    {
        _basePosition = transform.position;
        ResetTileWander();
    }

    // ══════════════════════════════════════════════════════════════════
    //  벡터 기반 배회 (HostileMonster 호환)
    // ══════════════════════════════════════════════════════════════════

    public void SetBasePosition(Vector2 pos) => _basePosition = pos;

    public Vector2 GetWanderDirection()
    {
        _wanderTimer -= Time.deltaTime;
        if (_wanderTimer <= 0f)
        {
            Vector2 randomPoint = _basePosition + Random.insideUnitCircle * _wanderRadius;
            _currentWanderDirection = (randomPoint - (Vector2)transform.position).normalized;
            _wanderTimer = Random.Range(_minWanderTime, _maxWanderTime);
        }
        return _currentWanderDirection;
    }

    public void ForceRecalculate()
    {
        _wanderTimer = 0f;
        ResetTileWander();
    }

    // ══════════════════════════════════════════════════════════════════
    //  타일 기반 배회 (NeutralMonster용)
    // ══════════════════════════════════════════════════════════════════

    public bool TickIdle()
    {
        if (!_isIdling) return false;

        _idleTimer -= Time.deltaTime;
        if (_idleTimer <= 0f)
        {
            _isIdling = false;
            _isEating = false;
            return false;
        }
        return true;
    }

    public void StartIdle()
    {
        _isIdling = true;
        int eatCount = Random.Range(_minEatCount, _maxEatCount + 1);
        _idleTimer = _eatAnimDuration * eatCount;
        _isEating = true;
    }

    public bool EnsurePath(LayerMask obstacleMask)
    {
        if (_tilePath != null && _tilePathIndex < _tilePath.Count)
        {
            // 주기적으로 경로 앞 타일에 동적 장애물이 있는지 검사
            _dynamicCheckTimer -= Time.deltaTime;
            if (_dynamicCheckTimer <= 0f)
            {
                _dynamicCheckTimer = DYNAMIC_CHECK_INTERVAL;
                if (IsPathBlockedByDynamic())
                    return GenerateTileWanderPath(obstacleMask);
            }
            return true;
        }

        return GenerateTileWanderPath(obstacleMask);
    }

    public Vector2? GetCurrentTileTarget()
    {
        if (_tilePath == null || _tilePathIndex >= _tilePath.Count)
            return null;

        return TileGridHelper.TileToWorld(_tilePath[_tilePathIndex]);
    }

    public bool AdvanceToNextTile()
    {
        _tilePathIndex++;
        return _tilePathIndex < _tilePath.Count;
    }

    private bool GenerateTileWanderPath(LayerMask obstacleMask)
    {
        Vector2Int currentTile = TileGridHelper.WorldToTile(transform.position);
        Vector2Int baseTile = TileGridHelper.WorldToTile(_basePosition);

        // 동적 장애물(동물, 에너미, 플레이어 등)이 있는 타일 수집
        List<Vector2Int> dynamicBlocked = CollectDynamicObstacleTiles();

        for (int attempt = 0; attempt < 8; attempt++)
        {
            int dx = Random.Range(-_tileWanderRange, _tileWanderRange + 1);
            int dy = Random.Range(-_tileWanderRange, _tileWanderRange + 1);
            if (dx == 0 && dy == 0) continue;

            Vector2Int targetTile = new Vector2Int(baseTile.x + dx, baseTile.y + dy);

            if (!TileGridHelper.IsWalkable(targetTile, obstacleMask))
                continue;
            if (TileGridHelper.ChebyshevDistance(currentTile, targetTile) < 2)
                continue;

            // 목적지 타일이 동적 장애물로 점유되어 있으면 스킵
            if (dynamicBlocked != null && dynamicBlocked.Contains(targetTile))
                continue;

            _tilePath = GridPathfinder.FindPath(
                currentTile, targetTile, obstacleMask, false, 256, dynamicBlocked);

            if (_tilePath != null && _tilePath.Count >= 2)
            {
                _tilePathIndex = 1;
                return true;
            }
        }

        return false;
    }

    // ══════════════════════════════════════════════════════════════════
    //  동적 장애물 감지
    // ══════════════════════════════════════════════════════════════════

    /// <summary>현재 경로의 앞 3타일에 동적 장애물(동물, 에너미, 플레이어)이 있는지 검사</summary>
    private bool IsPathBlockedByDynamic()
    {
        if (_dynamicObstacleMask == 0 || _tilePath == null) return false;

        int lookAhead = Mathf.Min(_tilePathIndex + 3, _tilePath.Count);
        for (int i = _tilePathIndex; i < lookAhead; i++)
        {
            if (TileGridHelper.HasObjectInTile(_tilePath[i], _dynamicObstacleMask))
                return true;
        }
        return false;
    }

    /// <summary>배회 범위 내 동적 장애물의 타일 좌표를 수집하여 A* blocked 리스트로 반환</summary>
    private List<Vector2Int> CollectDynamicObstacleTiles()
    {
        if (_dynamicObstacleMask == 0) return null;

        float scanRadius = _tileWanderRange * Mathf.Max(TileGridHelper.CellSize.x, TileGridHelper.CellSize.y);
        int hitCount = Physics2D.OverlapCircleNonAlloc(
            transform.position, scanRadius, _dynamicBuffer, _dynamicObstacleMask);

        if (hitCount == 0) return null;

        var blockedTiles = new List<Vector2Int>(hitCount);
        for (int i = 0; i < hitCount; i++)
        {
            // 자기 자신은 제외
            if (_dynamicBuffer[i].transform.root == transform.root) continue;

            Vector2Int tile = TileGridHelper.WorldToTile(_dynamicBuffer[i].transform.position);
            if (!blockedTiles.Contains(tile))
                blockedTiles.Add(tile);
        }

        return blockedTiles.Count > 0 ? blockedTiles : null;
    }

    private void ResetTileWander()
    {
        _tilePath = null;
        _tilePathIndex = 0;
        _isIdling = false;
        _isEating = false;
        _idleTimer = 0f;
        _dynamicCheckTimer = 0f;
    }
}
