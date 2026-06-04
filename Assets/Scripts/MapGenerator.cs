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

    [Header("노이즈 시드 / 옥타브(fBm)")]
    [Tooltip("같은 시드는 항상 같은 맵을 만듭니다. 값을 바꾸면 전혀 다른 지형이 됩니다.")]
    public int seed = 0;
    [Tooltip("옥타브 수. 1이면 단일 주파수(밋밋), 클수록 디테일이 쌓여 자연스러워집니다.")]
    [Range(1, 8)] public int octaves = 4;
    [Tooltip("옥타브마다 진폭이 줄어드는 비율(0~1). 작을수록 큰 덩어리 위주, 클수록 잔디테일이 강해집니다.")]
    [Range(0f, 1f)] public float persistence = 0.5f;
    [Tooltip("옥타브마다 주파수가 커지는 배수(보통 2). 클수록 옥타브 간 디테일 간격이 벌어집니다.")]
    public float lacunarity = 2f;

    [Header("길(Path) 모드 — 연한 영역을 얇은 띠로")]
    [Tooltip("켜면 연한(낮은 우선순위) 바이옴이 면적이 아니라 등치선 띠로 생성되어 길처럼 이어집니다.")]
    public bool usePathMode = false;
    [Tooltip("길이 지나는 노이즈 높이(0~1). 길의 위치/형태가 바뀝니다.")]
    [Range(0f, 1f)] public float pathLevel = 0.5f;
    [Tooltip("띠 두께(반폭). 길 폭을 결정합니다. 너무 작으면 길이 끊깁니다.")]
    public float pathWidth = 0.07f;

    [Header("도메인 워핑 — 길이 유기적으로 굽이치게")]
    [Tooltip("켜면 샘플 좌표를 저주파 노이즈로 휘게 해서 직선적인 느낌이 사라집니다.")]
    public bool useDomainWarp = true;
    [Tooltip("좌표를 휘게 하는 세기(월드 타일 단위). 클수록 더 크게 굽이칩니다.")]
    public float warpStrength = 40f;
    [Tooltip("워프 노이즈의 주파수. 작을수록 더 완만하고 길게 굽이칩니다.")]
    public float warpScale = 0.01f;

    public List<BiomeSetting> biomes = new List<BiomeSetting>();

    // 시드 기반 옥타브별 오프셋. 원점(0,0) 거울 대칭/격자 아티팩트를 피하기 위해 사용
    private Vector2[] octaveOffsets;
    private Vector2 warpOffset;        // 도메인 워핑 노이즈용 오프셋
    private int pathBiomeIndex;        // 연한(길) = 우선순위 최저 바이옴
    private int groundBiomeIndex;      // 진한(땅) = 우선순위 최고 바이옴

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
        InitNoise();
        CalculateThresholds();
        ResolvePathBiomes();
    }

    // 길 모드용: 연한(우선순위 최저) / 진한(우선순위 최고) 바이옴 인덱스를 찾는다.
    void ResolvePathBiomes()
    {
        if (biomes == null || biomes.Count == 0) return;
        pathBiomeIndex = 0;
        groundBiomeIndex = 0;
        int minP = int.MaxValue, maxP = int.MinValue;
        for (int i = 0; i < biomes.Count; i++)
        {
            if (biomes[i].priority < minP) { minP = biomes[i].priority; pathBiomeIndex = i; }
            if (biomes[i].priority > maxP) { maxP = biomes[i].priority; groundBiomeIndex = i; }
        }
    }

    // 시드로부터 옥타브별 오프셋을 산출한다.
    // Unity Mathf.PerlinNoise는 원점(0,0) 기준 거울 대칭이라 음수/원점 부근을 그대로 샘플링하면
    // 맵이 좌우·상하 대칭으로 찍힌다. 큰 양수 오프셋으로 샘플 영역을 원점에서 멀리 떨어뜨려 이를 회피한다.
    void InitNoise()
    {
        System.Random prng = new System.Random(seed);
        int count = Mathf.Max(1, octaves);
        octaveOffsets = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            // 1,000 ~ 100,000 사이의 큰 양수 오프셋 → 원점에서 충분히 떨어진 영역만 샘플링
            float ox = prng.Next(1000, 100000);
            float oy = prng.Next(1000, 100000);
            octaveOffsets[i] = new Vector2(ox, oy);
        }
        // 워프 노이즈도 본 노이즈와 상관관계가 없도록 별도 오프셋 사용
        warpOffset = new Vector2(prng.Next(1000, 100000), prng.Next(1000, 100000));
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
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }
            else
            {
                return;
            }
        }

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

    // 옥타브(fBm) 합성 노이즈를 0~1 범위로 반환한다. (도메인 워핑 적용)
    public float SampleNoise(int x, int y)
    {
        // 옥타브 수가 런타임에 바뀌었거나 초기화 전이면 보정
        if (octaveOffsets == null || octaveOffsets.Length != Mathf.Max(1, octaves))
            InitNoise();

        float sx = x;
        float sy = y;

        // [도메인 워핑] 좌표를 저주파 노이즈로 휘게 해서 띠가 유기적으로 굽이치게 한다.
        if (useDomainWarp)
        {
            float qx = Mathf.PerlinNoise((x + warpOffset.x) * warpScale, (y + warpOffset.y) * warpScale);
            float qy = Mathf.PerlinNoise((x + warpOffset.x + 5000f) * warpScale, (y + warpOffset.y + 5000f) * warpScale);
            sx = x + (qx * 2f - 1f) * warpStrength;
            sy = y + (qy * 2f - 1f) * warpStrength;
        }

        return SampleFbm(sx, sy);
    }

    // 실수 좌표 기반 fBm 합성 (도메인 워핑된 좌표를 받는다)
    float SampleFbm(float x, float y)
    {
        float amplitude = 1f;
        float frequency = 1f;
        float noiseHeight = 0f;
        float amplitudeSum = 0f;

        for (int i = 0; i < octaveOffsets.Length; i++)
        {
            float sampleX = (x + octaveOffsets[i].x) * noiseScale * frequency;
            float sampleY = (y + octaveOffsets[i].y) * noiseScale * frequency;

            float perlin = Mathf.PerlinNoise(sampleX, sampleY);
            noiseHeight += perlin * amplitude;
            amplitudeSum += amplitude;

            amplitude *= persistence;
            frequency *= lacunarity;
        }

        // 진폭 합으로 나눠 다시 0~1로 정규화 (threshold 비교가 그대로 유효하도록)
        return amplitudeSum > 0f ? noiseHeight / amplitudeSum : 0f;
    }

    // 1. 노이즈를 기반으로 바이옴의 '인덱스'만 반환 (데이터 생성용)
    public int GetBiomeIndexAt(int x, int y)
    {
        float noiseValue = SampleNoise(x, y);

        // [길 모드] 등치선 띠 안이면 연한(길) 바이옴, 밖이면 진한(땅) 바이옴
        if (usePathMode)
        {
            bool isPath = Mathf.Abs(noiseValue - pathLevel) < pathWidth;
            return isPath ? pathBiomeIndex : groundBiomeIndex;
        }

        for (int i = 0; i < biomes.Count; i++)
        {
            if (noiseValue <= biomes[i].threshold) return i;
        }
        return biomes.Count - 1;
    }

    // 한 셀(localX, localY)에서 실제로 그려지는 '지배 바이옴'의 인덱스를 반환한다.
    // 듀얼 그리드 4개 코너 중 우선순위가 가장 높은 바이옴이 곧 그 칸의 타일을 결정한다.
    // (동률일 때 TL > TR > BL > BR 순서는 렌더링과 동일하게 유지)
    // 오브젝트 배치도 이 결과를 그대로 사용하므로 "보이는 타일 = 배치 기준"이 항상 일치한다.
    public int GetDominantBiomeIndex(TerrainChunk chunk, int localX, int localY)
    {
        int blIdx = chunk.terrainData[localX, localY];
        int brIdx = chunk.terrainData[localX + 1, localY];
        int tlIdx = chunk.terrainData[localX, localY + 1];
        int trIdx = chunk.terrainData[localX + 1, localY + 1];

        int maxP = Mathf.Max(biomes[blIdx].priority, biomes[brIdx].priority,
                             biomes[tlIdx].priority, biomes[trIdx].priority);

        if (biomes[tlIdx].priority == maxP) return tlIdx;
        if (biomes[trIdx].priority == maxP) return trIdx;
        if (biomes[blIdx].priority == maxP) return blIdx;
        return brIdx;
    }

    // 2. [핵심] 데이터 테이블을 참조하여 듀얼 그리드 타일을 실제로 그림
    public void RenderDualTileFromTable(TerrainChunk chunk, int localX, int localY)
    {
        // 테이블에서 4개 인접 지점의 바이옴 인덱스 참조
        int blIdx = chunk.terrainData[localX, localY];
        int brIdx = chunk.terrainData[localX + 1, localY];
        int tlIdx = chunk.terrainData[localX, localY + 1];
        int trIdx = chunk.terrainData[localX + 1, localY + 1];

        // 지배 바이옴 (오브젝트 배치와 동일한 판정 로직 공유)
        BiomeSetting dominant = biomes[GetDominantBiomeIndex(chunk, localX, localY)];
        int maxP = dominant.priority;

        // 비트마스크 계산 (8:TL, 4:TR, 2:BL, 1:BR)
        int mask = 0;
        if (biomes[tlIdx].priority == maxP) mask += 8;
        if (biomes[trIdx].priority == maxP) mask += 4;
        if (biomes[blIdx].priority == maxP) mask += 2;
        if (biomes[brIdx].priority == maxP) mask += 1;

        int worldX = chunk.coord.x * chunkSize + localX;
        int worldY = chunk.coord.y * chunkSize + localY;

        if (dominant.tiles != null && mask < dominant.tiles.Length)
        {
            tilemap.SetTile(new Vector3Int(worldX, worldY, 0), dominant.tiles[mask]);
        }
    }

    // [변경] 타일(셀) 기준 배치.
    // 각 셀의 '지배 바이옴'을 구해, 그 바이옴이 가진 프리팹만 그 칸 위에 생성한다.
    // → 진한 타일(예: dirt) 위엔 dirt 바이옴 프리팹(나무)만, 연한 타일(예: grass) 위엔
    //    grass 바이옴 프리팹(돌)만 생성된다. 생성 여부는 setting.spawnChance(=칸당 확률, 예 0.3)로 판정.
    public void TrySpawnAtCell(TerrainChunk chunk, int localX, int localY)
    {
        int biomeIdx = GetDominantBiomeIndex(chunk, localX, localY);
        BiomeSetting setting = biomes[biomeIdx];

        // 이 바이옴에 배치할 프리팹이 없으면 스킵 (예: 오브젝트를 두지 않는 바이옴)
        if (setting.prefabs == null || setting.prefabs.Length == 0) return;

        int worldX = chunk.coord.x * chunkSize + localX;
        int worldY = chunk.coord.y * chunkSize + localY;

        // 칸당 생성 확률 판정 (좌표 기반 결정적 난수 → 같은 맵이면 항상 같은 배치)
        float roll = GetSymmetricRandom(worldX, worldY, 50);
        if (roll > setting.spawnChance) return;

        // 셀 중심 위치 (타일 앵커가 0.5이므로 +0.5)
        Vector3 spawnPos = new Vector3(worldX + 0.5f, worldY + 0.5f, 0);

        // minSpacing + 오브젝트 크기 기반 겹침 체크
        float checkRadius = setting.minSpacing + (Mathf.Max(setting.tileSize.x, setting.tileSize.y) * 0.4f);
        if (Physics2D.OverlapCircle(spawnPos, checkRadius) != null) return;

        // 오브젝트 풀에서 컨테이너 가져오기
        GameObject container = objectPool.Get();
        container.transform.position = spawnPos;

        // 비주얼 프리팹 생성 및 스케일 조절
        int pIdx = Mathf.FloorToInt(GetSymmetricRandom(worldX, worldY, 3) * setting.prefabs.Length) % setting.prefabs.Length;
        GameObject visual = Instantiate(setting.prefabs[pIdx], container.transform);

        SpriteRenderer sr = visual.GetComponentInChildren<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            Vector2 spriteSize = sr.sprite.bounds.size;
            float scaleX = setting.tileSize.x / spriteSize.x;
            float scaleY = setting.tileSize.y / spriteSize.y;

            // 비율 유지를 원한다면 Mathf.Min(scaleX, scaleY)를 사용하세요.
            visual.transform.localScale = new Vector3(scaleX, scaleY, 1);
            visual.transform.localPosition = Vector3.zero;
        }

        container.transform.SetParent(chunk.objectParent);
        chunk.AddObject(container);

        // 주의: 생성된 프리팹에 Collider2D가 있어야 겹침 체크에서 감지됩니다.
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
        // 셀 단위로 순회하며, 각 칸의 지배 바이옴에 맞는 프리팹만 배치한다.
        for (int x = 0; x < gen.chunkSize; x++)
        {
            for (int y = 0; y < gen.chunkSize; y++)
            {
                gen.TrySpawnAtCell(this, x, y);
            }
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