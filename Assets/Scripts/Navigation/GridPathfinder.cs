using System.Collections.Generic;
using UnityEngine;

public static class GridPathfinder
{
    private static readonly Vector2Int[] DIR_4 =
    {
        new Vector2Int( 0,  1),
        new Vector2Int( 0, -1),
        new Vector2Int(-1,  0),
        new Vector2Int( 1,  0),
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

    private struct HeapNode
    {
        public Vector2Int Pos;
        public float F;
    }

    public static List<Vector2Int> FindPath(
        Vector2Int start,
        Vector2Int goal,
        LayerMask obstacleMask,
        bool allowDiagonal = true,
        int maxNodes = 512,
        List<Vector2Int> blockedTiles = null)
    {
        if (start == goal)
            return new List<Vector2Int> { start };

        if (!TileGridHelper.IsWalkable(goal, obstacleMask))
            return null;

        var directions = allowDiagonal ? DIR_8 : DIR_4;

        HashSet<Vector2Int> blockedSet = null;
        if (blockedTiles != null && blockedTiles.Count > 0)
        {
            blockedSet = new HashSet<Vector2Int>();
            for (int b = 0; b < blockedTiles.Count; b++)
            {
                Vector2Int bt = blockedTiles[b];
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                        blockedSet.Add(new Vector2Int(bt.x + dx, bt.y + dy));
            }
        }

        var walkableCache = new Dictionary<Vector2Int, bool>(256);
        var openHeap = new List<HeapNode>(128);
        var gScore = new Dictionary<Vector2Int, float>(256);
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>(256);
        var closedSet = new HashSet<Vector2Int>();

        gScore[start] = 0f;
        HeapPush(openHeap, new HeapNode { Pos = start, F = Heuristic(start, goal) });

        int nodesExpanded = 0;

        while (openHeap.Count > 0)
        {
            HeapNode currentNode = HeapPop(openHeap);
            Vector2Int current = currentNode.Pos;

            if (current == goal)
                return ReconstructPath(cameFrom, start, goal);

            if (!closedSet.Add(current))
                continue;

            nodesExpanded++;
            if (nodesExpanded >= maxNodes)
                return null;

            float currentG = gScore[current];

            for (int i = 0; i < directions.Length; i++)
            {
                Vector2Int neighbor = current + directions[i];

                if (closedSet.Contains(neighbor))
                    continue;

                if (blockedSet != null && blockedSet.Contains(neighbor))
                    continue;

                if (!IsWalkableCached(neighbor, obstacleMask, walkableCache))
                    continue;

                if (allowDiagonal && directions[i].x != 0 && directions[i].y != 0)
                {
                    Vector2Int adjX = new Vector2Int(current.x + directions[i].x, current.y);
                    Vector2Int adjY = new Vector2Int(current.x, current.y + directions[i].y);
                    if (!IsWalkableCached(adjX, obstacleMask, walkableCache) ||
                        !IsWalkableCached(adjY, obstacleMask, walkableCache))
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
                HeapPush(openHeap, new HeapNode { Pos = neighbor, F = f });
            }
        }

        return null;
    }

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

    private static bool IsWalkableCached(Vector2Int tile, LayerMask obstacleMask, Dictionary<Vector2Int, bool> cache)
    {
        if (cache.TryGetValue(tile, out bool result))
            return result;

        result = TileGridHelper.IsWalkable(tile, obstacleMask);
        cache[tile] = result;
        return result;
    }

    private static float Heuristic(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return STRAIGHT_COST * (dx + dy) + (DIAGONAL_COST - 2f * STRAIGHT_COST) * Mathf.Min(dx, dy);
    }

    private static void HeapPush(List<HeapNode> heap, HeapNode node)
    {
        heap.Add(node);
        int i = heap.Count - 1;
        while (i > 0)
        {
            int parent = (i - 1) / 2;
            if (heap[parent].F <= heap[i].F) break;
            (heap[parent], heap[i]) = (heap[i], heap[parent]);
            i = parent;
        }
    }

    private static HeapNode HeapPop(List<HeapNode> heap)
    {
        HeapNode top = heap[0];
        int last = heap.Count - 1;
        heap[0] = heap[last];
        heap.RemoveAt(last);
        last--;

        int i = 0;
        while (true)
        {
            int left = 2 * i + 1;
            int right = 2 * i + 2;
            int smallest = i;

            if (left <= last && heap[left].F < heap[smallest].F)
                smallest = left;
            if (right <= last && heap[right].F < heap[smallest].F)
                smallest = right;

            if (smallest == i) break;
            (heap[i], heap[smallest]) = (heap[smallest], heap[i]);
            i = smallest;
        }

        return top;
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
