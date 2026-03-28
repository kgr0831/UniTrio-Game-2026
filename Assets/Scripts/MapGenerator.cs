using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Pool;
using System.Linq;

[System.Serializable]
public class BiomeSetting
{
    public string name;
    public int priority; 
    public TileBase[] tiles; // 0~15번 비트마스크 타일 배열
    public GameObject[] prefabs;
    [Range(0, 1)] public float weight;
    [Range(0, 1)] public float spawnChance;
    public float minSpacing = 1.5f;
    [HideInInspector] public float threshold;
}

public class MapGenerator : MonoBehaviour
{
    [Header("참조 설정")]
    public Tilemap tilemap;
    public Transform player;

    [Header("지형 설정")]
    public int chunkSize = 16;
    public float noiseScale = 0.05f;
    public List<BiomeSetting> biomes = new List<BiomeSetting>();

    [Header("렌더링 설정")]
    public int renderDistance = 3;
    
    private Dictionary<Vector2Int, TerrainChunk> chunks = new Dictionary<Vector2Int, TerrainChunk>();
    private IObjectPool<GameObject> objectPool;

    void Awake()
    {
        objectPool = new ObjectPool<GameObject>(
            createFunc: () => new GameObject("PooledObject"),
            actionOnGet: (obj) => obj.SetActive(true),
            actionOnRelease: (obj) => {
                foreach (Transform child in obj.transform) Destroy(child.gameObject);
                obj.SetActive(false);
            },
            collectionCheck: false,
            defaultCapacity: 100,
            maxSize: 1000
        );

        CalculateThresholds();
    }

    void Update() => UpdateVisibleChunks();

    void CalculateThresholds()
    {
        float total = biomes.Sum(b => b.weight);
        float current = 0;
        for (int i = 0; i < biomes.Count; i++)
        {
            current += biomes[i].weight / total;
            biomes[i].threshold = current;
        }
    }

    void UpdateVisibleChunks()
    {
        Vector2Int playerCoord = new Vector2Int(
            Mathf.FloorToInt(player.position.x / chunkSize),
            Mathf.FloorToInt(player.position.y / chunkSize)
        );

        for (int x = -renderDistance; x <= renderDistance; x++)
        {
            for (int y = -renderDistance; y <= renderDistance; y++)
            {
                Vector2Int coord = playerCoord + new Vector2Int(x, y);
                if (!chunks.ContainsKey(coord))
                {
                    chunks[coord] = new TerrainChunk(coord, this);
                }
                chunks[coord].UpdateChunk(player.position, renderDistance * chunkSize);
            }
        }
    }

    // 1. 노이즈를 기반으로 바이옴의 '인덱스'만 반환 (데이터 생성용)
    public int GetBiomeIndexAt(int x, int y)
    {
        float noiseValue = Mathf.PerlinNoise(x * noiseScale, y * noiseScale);
        for (int i = 0; i < biomes.Count; i++)
        {
            if (noiseValue <= biomes[i].threshold) return i;
        }
        return biomes.Count - 1;
    }

    // 2. [핵심] 데이터 테이블을 참조하여 듀얼 그리드 타일을 실제로 그림
    public void RenderDualTileFromTable(TerrainChunk chunk, int localX, int localY)
    {
        // 테이블에서 4개 인접 지점의 바이옴 인덱스 참조
        int blIdx = chunk.terrainData[localX, localY];
        int brIdx = chunk.terrainData[localX + 1, localY];
        int tlIdx = chunk.terrainData[localX, localY + 1];
        int trIdx = chunk.terrainData[localX + 1, localY + 1];

        BiomeSetting bl = biomes[blIdx];
        BiomeSetting br = biomes[brIdx];
        BiomeSetting tl = biomes[tlIdx];
        BiomeSetting tr = biomes[trIdx];

        // 우선순위가 가장 높은 바이옴 찾기
        int maxP = Mathf.Max(bl.priority, br.priority, tl.priority, tr.priority);
        BiomeSetting dominant = (tl.priority == maxP) ? tl : 
                                (tr.priority == maxP) ? tr : 
                                (bl.priority == maxP) ? bl : br;

        // 비트마스크 계산 (8:TL, 4:TR, 2:BL, 1:BR)
        int mask = 0;
        if (tl.priority == maxP) mask += 8;
        if (tr.priority == maxP) mask += 4;
        if (bl.priority == maxP) mask += 2;
        if (br.priority == maxP) mask += 1;

        int worldX = chunk.coord.x * chunkSize + localX;
        int worldY = chunk.coord.y * chunkSize + localY;

        if (dominant.tiles != null && mask < dominant.tiles.Length)
        {
            tilemap.SetTile(new Vector3Int(worldX, worldY, 0), dominant.tiles[mask]);
        }
    }

    public void TrySpawnObject(TerrainChunk chunk, int x, int y, BiomeSetting setting, List<Vector2Int> occupied)
    {
        if (setting.prefabs == null || setting.prefabs.Length == 0) return;
        if (GetSymmetricRandom(x, y, 2) > setting.spawnChance) return;

        Vector2Int pos = new Vector2Int(x, y);
        if (occupied.Any(o => Vector2Int.Distance(o, pos) < setting.minSpacing)) return;

        GameObject obj = objectPool.Get();
        int pIdx = Mathf.FloorToInt(GetSymmetricRandom(x, y, 3) * setting.prefabs.Length) % setting.prefabs.Length;
        Instantiate(setting.prefabs[pIdx], obj.transform);

        obj.transform.position = new Vector3(x + 0.5f, y + 0.5f, 0);
        obj.transform.SetParent(chunk.objectParent);
        chunk.AddObject(obj);
        occupied.Add(pos);
    }

    public void ReleaseObject(GameObject obj) => objectPool.Release(obj);

    float GetSymmetricRandom(int x, int y, int offset)
    {
        int n = x + y * 57 + offset * 131;
        n = (n << 13) ^ n;
        return ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 2147483647f;
    }
}

public class TerrainChunk
{
    public Vector2Int coord;
    public int[,] terrainData; // 데이터 테이블
    public Transform objectParent;
    private List<GameObject> objects = new List<GameObject>();
    private MapGenerator gen;
    private bool isActive = false;

    public TerrainChunk(Vector2Int coord, MapGenerator gen)
    {
        this.coord = coord;
        this.gen = gen;
        
        GameObject go = new GameObject($"Chunk_{coord.x}_{coord.y}");
        objectParent = go.transform;
        objectParent.SetParent(gen.transform);
        
        GenerateContent();
    }

    void GenerateContent()
    {
        // 1. 데이터 테이블 생성 (인접 청크 경계를 위해 +1 크게 생성)
        terrainData = new int[gen.chunkSize + 1, gen.chunkSize + 1];
        for (int x = 0; x <= gen.chunkSize; x++)
        {
            for (int y = 0; y <= gen.chunkSize; y++)
            {
                int worldX = coord.x * gen.chunkSize + x;
                int worldY = coord.y * gen.chunkSize + y;
                terrainData[x, y] = gen.GetBiomeIndexAt(worldX, worldY);
            }
        }

        // 2. 렌더링 및 오브젝트 배치
        List<Vector2Int> occupied = new List<Vector2Int>();
        for (int x = 0; x < gen.chunkSize; x++)
        {
            for (int y = 0; y < gen.chunkSize; y++)
            {
                // 지형 렌더링 (테이블 기반)
                gen.RenderDualTileFromTable(this, x, y);

                // 오브젝트 스폰 (중심점 바이옴 기반)
                int worldX = coord.x * gen.chunkSize + x;
                int worldY = coord.y * gen.chunkSize + y;
                gen.TrySpawnObject(this, worldX, worldY, gen.biomes[terrainData[x,y]], occupied);
            }
        }
    }

    public void AddObject(GameObject obj) => objects.Add(obj);

    public void UpdateChunk(Vector3 playerPos, float maxD)
    {
        float dist = Vector2.Distance(new Vector2(playerPos.x, playerPos.y), 
                     new Vector2(objectParent.position.x + gen.chunkSize/2f, objectParent.position.y + gen.chunkSize/2f));
        bool shouldBeActive = dist <= maxD;

        if (shouldBeActive && !isActive)
        {
            objectParent.gameObject.SetActive(true);
            isActive = true;
        }
        else if (!shouldBeActive && isActive)
        {
            objectParent.gameObject.SetActive(false);
            isActive = false;
        }
    }
}