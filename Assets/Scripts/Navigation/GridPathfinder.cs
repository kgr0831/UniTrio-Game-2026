using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 경량 A* 경로 탐색기.
/// TileGridHelper 기반 2D 타일 그리드에서 동작하며, 독립 유틸리티로 재사용 가능.
/// </summary>
public static class GridPathfinder
{
    private static readonly Vector2Int[] DIR_4 =
    {
        new Vector2Int( 0,  1), // 상
        new Vector2Int( 0, -1), // 하
        new Vector2Int(-1,  0), // 좌
        new Vector2Int( 1,  0), // 우
    };

    private static readonly Vector2Int[] DIR_8 =
    {
        new Vector2Int( 0,  1),
        new Vector2Int( 0, -1),
        new Vector2Int(-1,  0),
        new Vector2Int( 1,  0),
        new Vector2Int(-1,  1),
        new Vector2Int( 1,  1),
        new Vector2Int(-1, -1),
        new Vector2Int( 1, -1),
    };

    private const float STRAIGHT_COST = 1f;
    private const float DIAGONAL_COST = 1.414f;

    private struct Node
    {
        public Vector2Int Pos;
        public float G;
        public float F;
    }

    /// <summary>
    /// A* 경로 탐색.
    /// </summary>
    /// <param name="start">시작 타일 좌표</param>
    /// <param name="goal">목표 타일 좌표</param>
    /// <param name="obstacleMask">장애물 레이어 마스크</param>
    /// <param name="allowDiagonal">대각선 이동 허용 여부</param>
    /// <param name="maxNodes">최대 탐색 노드 수 (병목 방지)</param>
    /// <returns>시작→목표 타일 좌표 리스트 (시작 포함, 실패 시 null)</returns>
    public static List<Vector2Int> FindPath(
        Vector2Int start,
        Vector2Int goal,
        LayerMask obstacleMask,
        bool allowDiagonal = true,
        int maxNodes = 512)
    {
        if (start == goal)
            return new List<Vector2Int> { start };

        if (!TileGridHelper.IsWalkable(goal, obstacleMask))
            return null;

        var directions = allowDiagonal ? DIR_8 : DIR_4;

        var openSet = new SortedList<float, List<Vector2Int>>();
        var gScore = new Dictionary<Vector2Int, float>();
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var closedSet = new HashSet<Vector2Int>();

        gScore[start] = 0f;
        float startH = Heuristic(start, goal);
        AddToOpen(openSet, startH, start);

        int nodesExpanded = 0;

        while (openSet.Count > 0)
        {
            Vector2Int current = PopBest(openSet);

            if (current == goal)
                return ReconstructPath(cameFrom, start, goal);

            if (closedSet.Contains(current))
                continue;

            closedSet.Add(current);
            nodesExpanded++;

            if (nodesExpanded >= maxNodes)
                return null;

            float currentG = gScore[current];

            for (int i = 0; i < directions.Length; i++)
            {
                Vector2Int neighbor = current + directions[i];

                if (closedSet.Contains(neighbor))
                    continue;

                if (!TileGridHelper.IsWalkable(neighbor, obstacleMask))
                    continue;

                // 대각선 이동 시 인접 두 칸이 모두 열려있어야 통과 (코너 컷 방지)
                if (allowDiagonal && directions[i].x != 0 && directions[i].y != 0)
                {
                    Vector2Int adjX = new Vector2Int(current.x + directions[i].x, current.y);
                    Vector2Int adjY = new Vector2Int(current.x, current.y + directions[i].y);
                    if (!TileGridHelper.IsWalkable(adjX, obstacleMask) ||
                        !TileGridHelper.IsWalkable(adjY, obstacleMask))
                        continue;
                }

                bool isDiagonal = directions[i].x != 0 && directions[i].y != 0;
                float moveCost = isDiagonal ? DIAGONAL_COST : STRAIGHT_COST;
                float tentativeG = currentG + moveCost;

                if (gScore.TryGetValue(neighbor, out float existingG) && tentativeG >= existingG)
                    continue;

                gScore[neighbor] = tentativeG;
                cameFrom[neighbor] = current;
                float f = tentativeG + Heuristic(neighbor, goal);
                AddToOpen(openSet, f, neighbor);
            }
        }

        return null;
    }

    /// <summary>
    /// 목표 지점이 막혀있을 경우, 목표 주변에서 가장 가까운 걷기 가능 타일을 탐색.
    /// </summary>
    public static Vector2Int? FindNearestWalkable(Vector2Int center, LayerMask obstacleMask, int maxRadius = 5)
    {
        if (TileGridHelper.IsWalkable(center, obstacleMask))
            return center;

        for (int r = 1; r <= maxRadius; r++)
        {
            for (int x = -r; x <= r; x++)
            {
                for (int y = -r; y <= r; y++)
                {
                    if (Mathf.Abs(x) != r && Mathf.Abs(y) != r) continue;

                    Vector2Int candidate = new Vector2Int(center.x + x, center.y + y);
                    if (TileGridHelper.IsWalkable(candidate, obstacleMask))
                        return candidate;
                }
            }
        }

        return null;
    }

    private static float Heuristic(Vector2Int a, Vector2Int b)
    {
        // Octile distance: 대각선 이동 비용을 정확히 반영
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return STRAIGHT_COST * (dx + dy) + (DIAGONAL_COST - 2f * STRAIGHT_COST) * Mathf.Min(dx, dy);
    }

    private static void AddToOpen(SortedList<float, List<Vector2Int>> openSet, float f, Vector2Int pos)
    {
        // SortedList는 동일 키를 허용하지 않으므로 List로 묶어서 관리
        if (!openSet.TryGetValue(f, out var list))
        {
            list = new List<Vector2Int>(4);
            openSet.Add(f, list);
        }
        list.Add(pos);
    }

    private static Vector2Int PopBest(SortedList<float, List<Vector2Int>> openSet)
    {
        var list = openSet.Values[0];
        float key = openSet.Keys[0];
        Vector2Int best = list[list.Count - 1];
        list.RemoveAt(list.Count - 1);
        if (list.Count == 0)
            openSet.RemoveAt(0);
        return best;
    }

    private static List<Vector2Int> ReconstructPath(
        Dictionary<Vector2Int, Vector2Int> cameFrom,
        Vector2Int start,
        Vector2Int goal)
    {
        var path = new List<Vector2Int>();
        Vector2Int current = goal;

        while (current != start)
        {
            path.Add(current);
            current = cameFrom[current];
        }
        path.Add(start);
        path.Reverse();
        return path;
    }
}
