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
            return true;

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

            _tilePath = GridPathfinder.FindPath(
                currentTile, targetTile, obstacleMask, false, 256);

            if (_tilePath != null && _tilePath.Count >= 2)
            {
                _tilePathIndex = 1;
                return true;
            }
        }

        return false;
    }

    private void ResetTileWander()
    {
        _tilePath = null;
        _tilePathIndex = 0;
        _isIdling = false;
        _isEating = false;
        _idleTimer = 0f;
    }
}
