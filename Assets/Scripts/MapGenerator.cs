using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class MapGenerator : MonoBehaviour
{
    [Header("연결 설정")]
    public Tilemap tilemap;
    
    // 각 지형별로 여러 개의 타일을 등록할 수 있도록 배열로 선언
    public TileBase[] waterTiles;  // 물 타일들
    public TileBase[] sandTiles;   // 모래 타일들
    public TileBase[] grassTiles;  // 풀 타일들
    public TileBase[] rockTiles;   // 바위 타일들

    [Header("지형 설정")]
    public int chunkSize = 16;
    public int viewDistance = 2;
    public float scale = 40f; 
    public string seed = "MyWorld";

    [Header("덩어리 조절")]
    [Range(1f, 50f)]
    public float warpStrength = 20f; // 이 값을 높일수록 경계가 더 흐물흐물하고 둥글게 변함 [cite: 2026-03-04]

    private Dictionary<Vector2Int, bool> spawnedChunks = new Dictionary<Vector2Int, bool>();
    private Transform player;
    private float seedOffset;

    void Start()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null) player = playerObj.transform;
        
        seedOffset = (float)(seed.GetHashCode() % 10000);
        UpdateChunks();
    }

    void Update()
    {
        if (player != null) UpdateChunks();
    }

    void UpdateChunks()
    {
        Vector2Int currentChunkCoord = new Vector2Int(
            Mathf.FloorToInt(player.position.x / chunkSize),
            Mathf.FloorToInt(player.position.y / chunkSize)
        );

        for (int x = -viewDistance; x <= viewDistance; x++)
        {
            for (int y = -viewDistance; y <= viewDistance; y++)
            {
                Vector2Int targetCoord = currentChunkCoord + new Vector2Int(x, y);
                if (!spawnedChunks.ContainsKey(targetCoord))
                {
                    GenerateChunk(targetCoord);
                }
            }
        }
    }

    void GenerateChunk(Vector2Int coord)
    {
        for (int x = 0; x < chunkSize; x++)
        {
            for (int y = 0; y < chunkSize; y++)
            {
                int worldX = coord.x * chunkSize + x;
                int worldY = coord.y * chunkSize + y;

                // 1. 좌표 왜곡 (Warping) - 둥근 느낌을 위해 nX, nY를 섞음
                float nX = Mathf.PerlinNoise((worldX + seedOffset) / scale, (worldY + seedOffset) / scale);
                float nY = Mathf.PerlinNoise((worldX + seedOffset + 5231f) / scale, (worldY + seedOffset + 5231f) / scale);

                float warpedX = worldX + (nX - 0.5f) * warpStrength;
                float warpedY = worldY + (nY - 0.5f) * warpStrength;

                // 2. 덩어리 결정을 위한 값 (둥근 경계를 위해 왜곡된 좌표 사용)
                int cellX = Mathf.FloorToInt(warpedX / (scale * 0.8f));
                int cellY = Mathf.FloorToInt(warpedY / (scale * 0.8f));
                float biomeValue = GetSymmetricRandom(cellX, cellY, 0); // 덩어리 종류 결정용

                // 3. 해당 칸 내에서 타일을 랜덤하게 고르기 위한 값 (개별 타일 변화용)
                float tileVariationValue = GetSymmetricRandom(worldX, worldY, 1);

                TileBase selectedTile = null;

                // 덩어리 값에 따라 배열 중 하나를 랜덤하게 선택
                if (biomeValue < 0.3f)      selectedTile = PickRandomTile(waterTiles, tileVariationValue);
                else if (biomeValue < 0.45f) selectedTile = PickRandomTile(sandTiles, tileVariationValue);
                else if (biomeValue < 0.85f) selectedTile = PickRandomTile(grassTiles, tileVariationValue);
                else                        selectedTile = PickRandomTile(rockTiles, tileVariationValue);

                tilemap.SetTile(new Vector3Int(worldX, worldY, 0), selectedTile);
            }
        }
        spawnedChunks.Add(coord, true);
    }

    // 배열 내에서 랜덤하게 타일을 하나 골라주는 헬퍼 함수
    TileBase PickRandomTile(TileBase[] tileArray, float variation)
    {
        if (tileArray == null || tileArray.Length == 0) return null;
        int index = Mathf.FloorToInt(variation * tileArray.Length);
        return tileArray[Mathf.Clamp(index, 0, tileArray.Length - 1)];
    }

    // 좌표 기반 해시 함수 (seedOffset 등을 섞어 매번 다른 결과 반환)
    float GetSymmetricRandom(int x, int y, int offset)
    {
        int n = x + y * 57 + offset * 131;
        n = (n << 13) ^ n;
        int result = (n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff;
        return (float)result / 2147483647f;
    }
}