using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 몹의 무작위 배회 시스템.
/// - 벡터 기반: GetWanderDirection() (HostileMonster 호환)
/// - 타일 기반: 경로 생성 + Idle 상태만 관리. 이동/도달 제어는 호출자가 담당.
/// </summary>
public sealed class WanderSystem : MonoBehaviour
{
    [Header("벡터 배회 (기존)")]
    [SerializeField] private float _minWanderTime = 1f;
    [SerializeField] private float _maxWanderTime = 3f;
    [SerializeField] private float _wanderRadius = 3f;

    [Header("타일 배회")]
    [SerializeField] private int _tileWanderRange = 4;
    [SerializeField] private float _idleChance = 0.4f;
    [SerializeField] private float _minIdleTime = 1f;
    [SerializeField] private float _maxIdleTime = 3f;

    // ── 벡터 배회 상태 ──
    private float   _wanderTimer;
    private Vector2 _currentWanderDirection;
    private Vector2 _basePosition;

    // ── 타일 배회 상태 ──
    private List<Vector2Int> _tilePath;
    private int              _tilePathIndex;
    private float            _idleTimer;
    private bool             _isIdling;

    public bool IsIdling => _isIdling;
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

    /// <summary>Idle 타이머 갱신. true면 아직 Idle 중.</summary>
    public bool TickIdle()
    {
        if (!_isIdling) return false;

        _idleTimer -= Time.deltaTime;
        if (_idleTimer <= 0f)
        {
            _isIdling = false;
            return false;
        }
        return true;
    }

    /// <summary>Idle 상태 시작</summary>
    public void StartIdle()
    {
        _isIdling = true;
        _idleTimer = Random.Range(_minIdleTime, _maxIdleTime);
    }

    /// <summary>경로가 없거나 끝났으면 새 경로 생성. 현재 경로 유효 여부 반환.</summary>
    public bool EnsurePath(LayerMask obstacleMask)
    {
        if (_tilePath != null && _tilePathIndex < _tilePath.Count)
            return true;

        return GenerateTileWanderPath(obstacleMask);
    }

    /// <summary>현재 목표 타일의 월드 중앙 좌표. 경로 없으면 null.</summary>
    public Vector2? GetCurrentTileTarget()
    {
        if (_tilePath == null || _tilePathIndex >= _tilePath.Count)
            return null;

        return TileGridHelper.TileToWorld(_tilePath[_tilePathIndex]);
    }

    /// <summary>현재 타일 도달 후 다음 타일로 진행. 경로 끝이면 false.</summary>
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
        _idleTimer = 0f;
    }
}
