using System.Collections.Generic;
using UnityEngine;

public static class FleePathGenerator
{
    private const int FLEE_TILE_DISTANCE = 5;
    private const int MAX_DESTINATION_ATTEMPTS = 16;

    public static List<Vector2Int> GenerateFleePath(
        Vector2Int currentTile,
        Vector2 threatWorldPos,
        LayerMask obstacleMask,
        LayerMask threatMask,
        List<Vector2Int> threatTiles = null)
    {
        Vector2 currentWorld = TileGridHelper.TileToWorld(currentTile);
        Vector2 fleeDir = (currentWorld - threatWorldPos).normalized;

        if (fleeDir.sqrMagnitude < 0.01f)
            fleeDir = Random.insideUnitCircle.normalized;

        Vector2Int? destination = FindFleeDestination(currentTile, fleeDir, obstacleMask, threatTiles);
        if (!destination.HasValue)
            return null;

        List<Vector2Int> path = GridPathfinder.FindPath(
            currentTile, destination.Value, obstacleMask, allowDiagonal: false, 512, threatTiles);

        return path;
    }

    private static Vector2Int? FindFleeDestination(
        Vector2Int currentTile,
        Vector2 fleeDir,
        LayerMask obstacleMask,
        List<Vector2Int> threatTiles)
    {
        for (int attempt = 0; attempt < MAX_DESTINATION_ATTEMPTS; attempt++)
        {
            float randomAngle = Random.Range(-25f, 25f);
            Vector2 rotatedDir = RotateVector2(fleeDir, randomAngle);

            int distance = FLEE_TILE_DISTANCE + Random.Range(-2, 3);
            distance = Mathf.Max(distance, 5);

            Vector2Int targetTile = new Vector2Int(
                currentTile.x + Mathf.RoundToInt(rotatedDir.x * distance),
                currentTile.y + Mathf.RoundToInt(rotatedDir.y * distance));

            if (TileGridHelper.IsWalkable(targetTile, obstacleMask) && !IsThreatTile(targetTile, threatTiles))
            {
                Vector2 toTarget = new Vector2(
                    targetTile.x - currentTile.x,
                    targetTile.y - currentTile.y);
                if (Vector2.Dot(toTarget.normalized, fleeDir) > 0.3f)
                    return targetTile;
            }

            Vector2Int? fallback = GridPathfinder.FindNearestWalkable(targetTile, obstacleMask, 5);
            if (fallback.HasValue && !IsThreatTile(fallback.Value, threatTiles))
            {
                Vector2 toFallback = new Vector2(
                    fallback.Value.x - currentTile.x,
                    fallback.Value.y - currentTile.y);
                if (Vector2.Dot(toFallback.normalized, fleeDir) > 0.3f)
                    return fallback.Value;
            }
        }

        for (int dist = 5; dist >= 2; dist--)
        {
            Vector2Int shortTarget = new Vector2Int(
                currentTile.x + Mathf.RoundToInt(fleeDir.x * dist),
                currentTile.y + Mathf.RoundToInt(fleeDir.y * dist));

            Vector2Int? near = GridPathfinder.FindNearestWalkable(shortTarget, obstacleMask, 3);
            if (near.HasValue)
                return near.Value;
        }

        return null;
    }

    private static bool IsThreatTile(Vector2Int tile, List<Vector2Int> threatTiles)
    {
        if (threatTiles == null) return false;
        for (int i = 0; i < threatTiles.Count; i++)
        {
            if (TileGridHelper.ChebyshevDistance(tile, threatTiles[i]) <= 1)
                return true;
        }
        return false;
    }

    private static Vector2 RotateVector2(Vector2 v, float angleDeg)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }
}
