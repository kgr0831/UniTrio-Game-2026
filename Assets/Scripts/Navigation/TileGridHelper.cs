using UnityEngine;

/// <summary>
/// 타일 그리드 좌표 변환 및 걷기 가능 여부 판정 유틸리티.
/// Grid 참조를 static 캐싱하여 씬 내 1회만 검색.
/// </summary>
public static class TileGridHelper
{
    private static Grid _cachedGrid;
    private static bool _gridSearched;

    private static readonly Vector2 HALF_CELL = new Vector2(1f, 1f);

    public static Grid CachedGrid
    {
        get
        {
            if (_cachedGrid == null && !_gridSearched)
            {
                _cachedGrid = Object.FindFirstObjectByType<Grid>();
                _gridSearched = true;
            }
            return _cachedGrid;
        }
    }

    public static Vector2 CellSize
    {
        get
        {
            if (CachedGrid != null)
                return new Vector2(CachedGrid.cellSize.x, CachedGrid.cellSize.y);
            return Vector2.one * 2f;
        }
    }

    /// <summary>씬 전환 시 캐시 초기화용</summary>
    public static void ClearCache()
    {
        _cachedGrid = null;
        _gridSearched = false;
    }

    /// <summary>월드 좌표 → 타일 좌표 (정수)</summary>
    public static Vector2Int WorldToTile(Vector2 worldPos)
    {
        if (CachedGrid != null)
        {
            Vector3Int cell = CachedGrid.WorldToCell(worldPos);
            return new Vector2Int(cell.x, cell.y);
        }
        Vector2 size = CellSize;
        return new Vector2Int(
            Mathf.FloorToInt(worldPos.x / size.x),
            Mathf.FloorToInt(worldPos.y / size.y));
    }

    /// <summary>타일 좌표 → 타일 중앙 월드 좌표</summary>
    public static Vector2 TileToWorld(Vector2Int tile)
    {
        if (CachedGrid != null)
        {
            Vector3 worldPos = CachedGrid.CellToWorld(new Vector3Int(tile.x, tile.y, 0));
            Vector2 size = CellSize;
            return new Vector2(worldPos.x + size.x * 0.5f, worldPos.y + size.y * 0.5f);
        }
        Vector2 s = CellSize;
        return new Vector2(tile.x * s.x + s.x * 0.5f, tile.y * s.y + s.y * 0.5f);
    }

    /// <summary>현재 월드 위치에서 가장 가까운 타일 중앙 좌표 반환</summary>
    public static Vector2 GetTileCenter(Vector2 worldPos)
    {
        return TileToWorld(WorldToTile(worldPos));
    }

    /// <summary>
    /// 해당 타일이 걷기 가능한지 판정.
    /// 타일 중앙에 OverlapBox를 사용하여 Wall/Obstacle 레이어 충돌 검사.
    /// </summary>
    public static bool IsWalkable(Vector2Int tile, LayerMask obstacleMask)
    {
        Vector2 center = TileToWorld(tile);
        Vector2 halfSize = CellSize * 0.4f;
        Collider2D hit = Physics2D.OverlapBox(center, halfSize, 0f, obstacleMask);
        return hit == null;
    }

    /// <summary>
    /// 해당 타일에 특정 레이어의 오브젝트가 있는지 검사.
    /// 위협(적/플레이어) 감지용.
    /// </summary>
    public static bool HasObjectInTile(Vector2Int tile, LayerMask mask)
    {
        Vector2 center = TileToWorld(tile);
        Vector2 halfSize = CellSize * 0.45f;
        Collider2D hit = Physics2D.OverlapBox(center, halfSize, 0f, mask);
        return hit != null;
    }

    /// <summary>두 타일 간 맨해튼 거리</summary>
    public static int ManhattanDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    /// <summary>두 타일 간 체비셰프 거리 (8방향 이동 시 실제 타일 수)</summary>
    public static int ChebyshevDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
    }
}
