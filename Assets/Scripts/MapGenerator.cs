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
    public Vector2Int tileSize = new Vector2Int(2, 2); // 추가: 이 바이옴의 오브젝트가 차지할 타일 수 (1x1, 2x2 등)
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
    private IObjectPool<GameObject> _objectPool;
    private IObjectPool<GameObject> objectPool
    {
        get
        {
            if (_objectPool == null)
            {
                _objectPool = new ObjectPool<GameObject>(
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
            }
            return _objectPool;
        }
    }

    void Awake()
    {
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

    // UpdateVisibleChunks 내부 로직 보강
    void UpdateVisibleChunks()
    {
        Vector2Int playerCoord = new Vector2Int(
            Mathf.FloorToInt(player.position.x / chunkSize),
            Mathf.FloorToInt(player.position.y / chunkSize)
        );

        // renderDistance를 2 이상으로 설정하면 캐릭터 주변 5x5 혹은 그 이상의 청크가 활성화됩니다.
        for (int x = -renderDistance; x <= renderDistance; x++)
        {
            for (int y = -renderDistance; y <= renderDistance; y++)
            {
                Vector2Int coord = playerCoord + new Vector2Int(x, y);
                if (!chunks.ContainsKey(coord))
                {
                    chunks[coord] = new TerrainChunk(coord, this);
                }
            
                // 단순히 거리로 끄는게 아니라, renderDistance 내에 있으면 무조건 활성화
                chunks[coord].UpdateChunk(player.position, renderDistance * chunkSize);
            }
        }

        // [추가] 범위를 벗어난 청크 비활성화 로직 (선택 사항)
        foreach (var chunk in chunks)
        {
            float dist = Vector2.Distance(playerCoord, chunk.Key);
            if (dist > renderDistance + 1) // 한 칸 정도 더 여유를 두고 비활성화
            {
                chunk.Value.UpdateChunk(player.position, renderDistance * chunkSize);
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

    // 기존 이중 루프 방식이 아닌, 청크당 스폰 시도 횟수를 기반으로 호출하도록 변경 권장
    public void TrySpawnObject(TerrainChunk chunk, BiomeSetting setting)
    {
        if (setting.prefabs == null || setting.prefabs.Length == 0) return;

        // 1. 청크 면적에 비례하여 스폰 시도 횟수 설정 (spawnChance를 밀도로 사용)
        int spawnAttempts = Mathf.FloorToInt(chunkSize * chunkSize * setting.spawnChance);

        for (int i = 0; i < spawnAttempts; i++)
        {
            // 2. 정수(int)가 아닌 실수(float) 기반의 자유 좌표 생성
            // i를 offset으로 활용해 고유한 랜덤값 추출
            float localX = GetSymmetricRandom(chunk.coord.x, i, 100) * chunkSize;
            float localY = GetSymmetricRandom(chunk.coord.y, i, 200) * chunkSize;

            Vector3 spawnPos = new Vector3(
                chunk.coord.x * chunkSize + localX,
                chunk.coord.y * chunkSize + localY,
                0
            );

            // 3. 물리 엔진을 이용한 겹침 체크 (CircleCast 또는 OverlapCircle)
            // 설정된 minSpacing과 오브젝트의 tileSize 중 큰 값을 기준으로 반경 설정
            float checkRadius = setting.minSpacing + (Mathf.Max(setting.tileSize.x, setting.tileSize.y) * 0.4f);
            
            // 해당 위치에 이미 배치된 오브젝트(Collider2D)가 있는지 확인
            Collider2D hit = Physics2D.OverlapCircle(spawnPos, checkRadius);

            if (hit == null)
            {
                // 4. 오브젝트 풀에서 컨테이너 가져오기 및 위치 설정
                GameObject container = objectPool.Get();
                container.transform.position = spawnPos;

                // 5. 비주얼 프리팹 생성 및 스케일 조절 (기존 로직 유지)
                int pIdx = Mathf.FloorToInt(GetSymmetricRandom((int)spawnPos.x, (int)spawnPos.y, 3) * setting.prefabs.Length) % setting.prefabs.Length;
                GameObject visual = Instantiate(setting.prefabs[pIdx], container.transform);

                SpriteRenderer sr = visual.GetComponentInChildren<SpriteRenderer>();
                if (sr != null && sr.sprite != null)
                {
                    Vector2 spriteSize = sr.sprite.bounds.size;
                    float scaleX = setting.tileSize.x / spriteSize.x;
                    float scaleY = setting.tileSize.y / spriteSize.y;
                    
                    // 비율 유지를 원한다면 Mathf.Min(scaleX, scaleY)를 사용하세요.
                    visual.transform.localScale = new Vector3(scaleX, scaleY, 1);
                    
                    // 자유 배치이므로 로컬 위치는 중앙(0,0,0)으로 초기화
                    visual.transform.localPosition = Vector3.zero;
                }

                container.transform.SetParent(chunk.objectParent);
                chunk.AddObject(container);
                
                // 주의: 생성된 프리팹에 Collider2D가 있어야 다음 루프에서 hit으로 감지됩니다.
            }
        }
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
        // [수정] 청크의 실제 월드 위치를 설정합니다.
        objectParent.position = new Vector3(coord.x * gen.chunkSize, coord.y * gen.chunkSize, 0);
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

        // 2. 지형 렌더링
        for (int x = 0; x < gen.chunkSize; x++)
        {
            for (int y = 0; y < gen.chunkSize; y++)
            {
                gen.RenderDualTileFromTable(this, x, y);
            }
        }

        // 3. 오브젝트 배치 (수정된 부분)
        // 각 바이옴 설정별로 청크 전체에 대해 스폰을 시도하도록 호출합니다.
        foreach (var biome in gen.biomes)
        {
            gen.TrySpawnObject(this, biome);
        }
    }

    public void AddObject(GameObject obj) => objects.Add(obj);

    public void UpdateChunk(Vector3 playerPos, float maxD)
    {
        // 청크 중심점 계산: (현재 위치 + 청크 절반 크기)
        Vector2 chunkCenter = new Vector2(
            objectParent.position.x + gen.chunkSize / 2f, 
            objectParent.position.y + gen.chunkSize / 2f
        );
    
        float dist = Vector2.Distance(new Vector2(playerPos.x, playerPos.y), chunkCenter);
    
        // 렌더링 거리(maxD) 안에 있는지 확인
        bool shouldBeActive = dist <= maxD;

        if (shouldBeActive != isActive)
        {
            objectParent.gameObject.SetActive(shouldBeActive);
            isActive = shouldBeActive;
        }
    }
}