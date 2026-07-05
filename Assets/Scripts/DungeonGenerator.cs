using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Tilemaps;

// ─────────────────────────────────────────────────────────────
// 소울나이트 스타일 로그라이크 던전 생성 - '데이터 뼈대' 단계
//  - 실제 오브젝트 배치 없음. 콘솔 로그(텍스트)로 데이터 검증이 목적.
//  - 격자(Grid) 좌표계: 한 칸 = 방 하나. up = +y, right = +x.
// ─────────────────────────────────────────────────────────────

/// <summary>방 타입. enum은 5종 모두 정의해두되, 이번 뼈대 단계에서는 Start/Normal만 배치한다.</summary>
public enum RoomType
{
    Start,    // 시작 방 (항상 중앙)
    Normal,   // 일반 방    Treasure, // 보물 방   (다음 단계에서 배치 예정)
    Shop,     // 상점 방   (다음 단계에서 배치 예정)
    Boss      // 보스 방   (다음 단계에서 배치 예정)
}

/// <summary>던전을 구성하는 방 한 칸의 데이터.</summary>
[System.Serializable]
public class Room
{
    public Vector2Int gridPos; // 격자 좌표 (X, Y)
    public RoomType type;

    // 상/하/좌/우 문(복도) 연결 여부
    public bool doorUp;
    public bool doorDown;
    public bool doorLeft;
    public bool doorRight;

    public Room(Vector2Int gridPos, RoomType type)
    {
        this.gridPos = gridPos;
        this.type = type;
    }

    /// <summary>현재 방에 연결된 문(복도) 개수.</summary>
    public int DoorCount =>
        (doorUp ? 1 : 0) + (doorDown ? 1 : 0) + (doorLeft ? 1 : 0) + (doorRight ? 1 : 0);
}

public class DungeonGenerator : MonoBehaviour
{
    [Header("던전 생성 규칙")]
    [Tooltip("생성할 최대 방 개수")]
    public int maxRooms = 10;

    [Range(0f, 1f)]
    [Tooltip("BFS 확장 시 한 방향으로 새 방을 만들 확률")]
    public float expansionChance = 0.5f;

    [Tooltip("확률 확장만으로 maxRooms를 못 채우면, 빈 자리를 강제로 채워 정확히 maxRooms개를 보장할지 여부")]
    public bool guaranteeMaxRooms = true;

    [Header("방 / 복도 크기 (타일)")]
    [Tooltip("방 한 칸의 한 변 타일 수 (벽 테두리 포함)")]
    public int roomSize = 12;

    [Tooltip("복도 폭 (타일)")]
    public int corridorWidth = 4;

    [Tooltip("방과 방 사이 복도의 길이(빈 간격) 타일 수")]
    public int corridorGap = 8;

    /// <summary>격자 한 칸의 월드 간격(방 + 복도) 타일 수.</summary>
    private int Stride => roomSize + corridorGap;

    [Header("타일맵 렌더링")]
    [Tooltip("데이터 생성 후 실제 타일맵으로 그릴지 여부")]
    public bool renderTilemap = true;

    [Tooltip("Grid 셀 크기 (WallField와 동일하게 1.5 권장)")]
    public Vector3 cellSize = new Vector3(1.5f, 1.5f, 0f);

    [Header("디버그")]
    public bool generateOnStart = true;

    // 런타임에 생성/재사용하는 타일맵과 타일 캐시
    private Tilemap dungeonTilemap;
    private readonly Dictionary<string, TileBase> tileCache = new Dictionary<string, TileBase>();

    // 격자 좌표 → 방. 좌표 중복 생성을 막고 인접 탐색을 O(1)로 처리한다.
    private readonly Dictionary<Vector2Int, Room> rooms = new Dictionary<Vector2Int, Room>();

    // 4방향. (셔플해서 사용 → 특정 방향 편향 방지)
    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    public IReadOnlyDictionary<Vector2Int, Room> Rooms => rooms;

    private void Start()
    {
        if (generateOnStart) Generate();
    }

    /// <summary>인스펙터 우클릭 컨텍스트 메뉴에서도 재생성 가능.</summary>
    [ContextMenu("Generate Dungeon")]
    public void Generate()
    {
        rooms.Clear();

        // 1) 시작 방을 중앙(0,0)에 배치하고 큐에 넣는다.
        Vector2Int startPos = Vector2Int.zero;
        Room start = new Room(startPos, RoomType.Start);
        rooms.Add(startPos, start);

        Queue<Room> frontier = new Queue<Room>();
        frontier.Enqueue(start);

        // 2) BFS 확률 확장: 큐에서 꺼낸 방의 빈 방향마다 확률로 새 방 생성.
        while (frontier.Count > 0 && rooms.Count < maxRooms)
        {
            Room current = frontier.Dequeue();

            foreach (Vector2Int dir in Shuffled(Directions))
            {
                if (rooms.Count >= maxRooms) break;

                Vector2Int nextPos = current.gridPos + dir;
                if (rooms.ContainsKey(nextPos)) continue;       // 이미 방이 있으면 건너뜀
                if (Random.value > expansionChance) continue;   // 확률 미당첨

                Room neighbor = new Room(nextPos, RoomType.Normal);
                rooms.Add(nextPos, neighbor);
                ConnectDoors(current, neighbor, dir);
                frontier.Enqueue(neighbor);
            }
        }

        // 3) (선택) 확률 운에 따라 10개를 못 채웠다면, 확장 가능한 방에서 강제로 채워 정확히 maxRooms 보장.
        if (guaranteeMaxRooms) FillRemaining();

        // 4) 데이터 검증 + 콘솔 출력
        LogDungeon();

        // 5) 실제 타일맵 렌더링
        if (renderTilemap) RenderTilemap();
    }

    /// <summary>maxRooms에 도달할 때까지, 빈 인접 칸을 가진 방에서 무작위로 한 칸씩 확장.</summary>
    private void FillRemaining()
    {
        while (rooms.Count < maxRooms)
        {
            // 빈 방향이 하나라도 있는 방들만 후보로
            List<Room> openRooms = new List<Room>();
            foreach (Room r in rooms.Values)
                if (HasFreeNeighbor(r)) openRooms.Add(r);

            if (openRooms.Count == 0) break; // 무한 격자에선 사실상 발생하지 않음

            Room from = openRooms[Random.Range(0, openRooms.Count)];

            List<Vector2Int> freeDirs = new List<Vector2Int>();
            foreach (Vector2Int dir in Directions)
                if (!rooms.ContainsKey(from.gridPos + dir)) freeDirs.Add(dir);

            Vector2Int chosen = freeDirs[Random.Range(0, freeDirs.Count)];
            Vector2Int pos = from.gridPos + chosen;

            Room neighbor = new Room(pos, RoomType.Normal);
            rooms.Add(pos, neighbor);
            ConnectDoors(from, neighbor, chosen);
        }
    }

    private bool HasFreeNeighbor(Room r)
    {
        foreach (Vector2Int dir in Directions)
            if (!rooms.ContainsKey(r.gridPos + dir)) return true;
        return false;
    }

    /// <summary>두 인접 방 사이에 양방향 문(복도)을 뚫는다.</summary>
    private void ConnectDoors(Room a, Room b, Vector2Int dir)
    {
        if (dir == Vector2Int.up)         { a.doorUp = true;    b.doorDown = true; }
        else if (dir == Vector2Int.down)  { a.doorDown = true;  b.doorUp = true; }
        else if (dir == Vector2Int.left)  { a.doorLeft = true;  b.doorRight = true; }
        else if (dir == Vector2Int.right) { a.doorRight = true; b.doorLeft = true; }
    }

    /// <summary>배열을 복사해 Fisher-Yates로 셔플한 새 배열 반환.</summary>
    private static Vector2Int[] Shuffled(Vector2Int[] source)
    {
        Vector2Int[] arr = (Vector2Int[])source.Clone();
        for (int i = arr.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (arr[i], arr[j]) = (arr[j], arr[i]);
        }
        return arr;
    }

    // ───────────────── 콘솔 로그 (검증용) ─────────────────

    private void LogDungeon()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("═══════════ DUNGEON GENERATED ═══════════");
        sb.AppendLine($"방 개수 : {rooms.Count} / {maxRooms}   |   방 크기 : {roomSize}x{roomSize}   |   복도 폭 : {corridorWidth}");
        sb.AppendLine("─────────────────────────────────────────");

        // 1) 방 목록 (좌표 / 타입 / 문 연결)
        foreach (Room r in rooms.Values)
        {
            sb.AppendLine(
                $"({r.gridPos.x,2},{r.gridPos.y,2}) {r.type,-8} | " +
                $"U:{(r.doorUp ? 'O' : '.')} D:{(r.doorDown ? 'O' : '.')} " +
                $"L:{(r.doorLeft ? 'O' : '.')} R:{(r.doorRight ? 'O' : '.')}  (문 {r.DoorCount})");
        }

        sb.AppendLine("─────────────────────────────────────────");
        sb.AppendLine(BuildAsciiMap());
        sb.AppendLine("─────────────────────────────────────────");
        sb.AppendLine(Validate());
        sb.AppendLine("═════════════════════════════════════════");

        Debug.Log(sb.ToString());
    }

    /// <summary>방=기호, 복도=─ │ 로 표현한 ASCII 미니맵. (위쪽이 +y)</summary>
    private string BuildAsciiMap()
    {
        int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
        foreach (Vector2Int p in rooms.Keys)
        {
            minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
            minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
        }

        int w = (maxX - minX + 1) * 2 - 1;
        int h = (maxY - minY + 1) * 2 - 1;

        char[,] grid = new char[h, w];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                grid[y, x] = ' ';

        foreach (Room r in rooms.Values)
        {
            int col = (r.gridPos.x - minX) * 2;
            int row = (maxY - r.gridPos.y) * 2; // y가 클수록 위로
            grid[row, col] = TypeSymbol(r.type);

            if (r.doorRight) grid[row, col + 1] = '─';
            if (r.doorUp)    grid[row - 1, col] = '│';
            // 좌/하는 인접 방 쪽에서 이미 그려지므로 생략 (양방향 중복 방지)
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("[MAP]  S=시작 N=일반 (T=보물 $=상점 B=보스 : 다음 단계)");
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++) sb.Append(grid[y, x]);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static char TypeSymbol(RoomType t) => t switch
    {
        RoomType.Start    => 'S',
        RoomType.Normal   => 'N',
        RoomType.Treasure => 'T',
        RoomType.Shop     => '$',
        RoomType.Boss     => 'B',
        _ => '?'
    };

    /// <summary>문 연결의 무결성 검증: 모든 문은 실제 인접 방의 반대편 문과 짝을 이뤄야 한다.</summary>
    private string Validate()
    {
        int errors = 0;
        foreach (Room r in rooms.Values)
        {
            errors += CheckDoor(r, r.doorUp,    Vector2Int.up,    "down");
            errors += CheckDoor(r, r.doorDown,  Vector2Int.down,  "up");
            errors += CheckDoor(r, r.doorLeft,  Vector2Int.left,  "right");
            errors += CheckDoor(r, r.doorRight, Vector2Int.right, "left");
        }
        return errors == 0
            ? "[검증] OK — 모든 문이 양방향으로 올바르게 연결됨."
            : $"[검증] 실패 — {errors}건의 문 불일치 발견.";
    }

    private int CheckDoor(Room r, bool hasDoor, Vector2Int dir, string opposite)
    {
        if (!hasDoor) return 0;
        Vector2Int neighborPos = r.gridPos + dir;
        if (!rooms.TryGetValue(neighborPos, out Room n))
        {
            Debug.LogWarning($"[검증] {r.gridPos} 의 문이 존재하지 않는 방 {neighborPos} 을 가리킴.");
            return 1;
        }
        bool reciprocal = opposite switch
        {
            "down"  => n.doorDown,
            "up"    => n.doorUp,
            "right" => n.doorRight,
            "left"  => n.doorLeft,
            _ => false
        };
        if (!reciprocal)
        {
            Debug.LogWarning($"[검증] {r.gridPos} → {neighborPos} 문이 단방향임 (반대편 문 없음).");
            return 1;
        }
        return 0;
    }

    // ───────────────── 타일맵 렌더링 ─────────────────

    /// <summary>그릴 Grid/Tilemap을 준비한다. (없으면 생성, 있으면 재사용)</summary>
    private void EnsureTilemap()
    {
        if (dungeonTilemap != null) return;

        Transform existing = transform.Find("DungeonGrid");
        if (existing != null)
        {
            dungeonTilemap = existing.GetComponentInChildren<Tilemap>();
            if (dungeonTilemap != null) return;
            DestroyImmediate(existing.gameObject); // 망가진 잔재면 새로 만든다
        }

        var gridGo = new GameObject("DungeonGrid");
        gridGo.transform.SetParent(transform, false);
        var grid = gridGo.AddComponent<Grid>();
        grid.cellSize = cellSize;

        var tmGo = new GameObject("Field");
        tmGo.transform.SetParent(gridGo.transform, false);
        dungeonTilemap = tmGo.AddComponent<Tilemap>();
        tmGo.AddComponent<TilemapRenderer>();
    }

    private void RenderTilemap()
    {
        EnsureTilemap();
        dungeonTilemap.ClearAllTiles();

        // 1) 방+복도를 하나의 '바닥' 집합으로 계산
        HashSet<Vector2Int> floor = BuildFloorSet();

        // 2) 바닥 둘레로 2겹 링을 자동 산출
        HashSet<Vector2Int> inner = RingAround(floor);
        HashSet<Vector2Int> floorPlusInner = new HashSet<Vector2Int>(floor);
        floorPlusInner.UnionWith(inner);
        HashSet<Vector2Int> outer = RingAround(floorPlusInner);

        // 3) 칠하기: 바닥 → 안쪽 링(바닥 기준) → 바깥 링(바닥+안쪽 기준)
        foreach (Vector2Int c in floor) SetTile(c.x, c.y, FloorTileName(c.x, c.y));
        foreach (Vector2Int c in inner) SetTile(c.x, c.y, RingTile(c, floor, true));
        foreach (Vector2Int c in outer) SetTile(c.x, c.y, RingTile(c, floorPlusInner, false));

        // 4) 성벽: 바깥 링 '하단 변' 아래 2줄. 복도 벽(19/21 등)이 있는 칸은 양보(우선순위 ↓)
        DrawCastle(outer, floorPlusInner);

        // 4-1) 방 하단 성벽의 좌우 끝(모서리 열)이 비는 부분을 중간 타일 31/34로 채움
        HashSet<Vector2Int> occupied = new HashSet<Vector2Int>(floorPlusInner);
        occupied.UnionWith(outer);
        DrawRoomCornerCastle(occupied);

        // 5) 복도 연결부 코너: 꼭짓점당 1개만 명시 배치 (가로/세로 전용 타일)
        DrawCorridorCorners();

        dungeonTilemap.RefreshAllTiles();
    }

    /// <summary>모든 방 내부 + 연결 복도를 하나의 바닥 좌표 집합으로 만든다.</summary>
    private HashSet<Vector2Int> BuildFloorSet()
    {
        const int inset = 2; // 2겹 링 두께만큼 안쪽이 바닥
        int gapStart = (roomSize - corridorWidth) / 2;
        int gapEnd = gapStart + corridorWidth - 1;

        HashSet<Vector2Int> floor = new HashSet<Vector2Int>();

        // 방 내부
        foreach (Room r in rooms.Values)
        {
            int ox = r.gridPos.x * Stride, oy = r.gridPos.y * Stride;
            for (int lx = inset; lx < roomSize - inset; lx++)
                for (int ly = inset; ly < roomSize - inset; ly++)
                    floor.Add(new Vector2Int(ox + lx, oy + ly));
        }

        // 연결 복도 (오른쪽/위 문에서만 → 이웃 방 내부까지 이어 붙임)
        foreach (Room r in rooms.Values)
        {
            int ox = r.gridPos.x * Stride, oy = r.gridPos.y * Stride;
            if (r.doorRight)
                for (int x = ox + roomSize - inset; x <= ox + Stride + inset - 1; x++)
                    for (int y = oy + gapStart; y <= oy + gapEnd; y++)
                        floor.Add(new Vector2Int(x, y));
            if (r.doorUp)
                for (int y = oy + roomSize - inset; y <= oy + Stride + inset - 1; y++)
                    for (int x = ox + gapStart; x <= ox + gapEnd; x++)
                        floor.Add(new Vector2Int(x, y));
        }
        return floor;
    }

    /// <summary>region에 8방향 인접하지만 region에 속하지 않는 셀들(한 겹 링).</summary>
    private static HashSet<Vector2Int> RingAround(HashSet<Vector2Int> region)
    {
        HashSet<Vector2Int> ring = new HashSet<Vector2Int>();
        foreach (Vector2Int c in region)
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    Vector2Int n = new Vector2Int(c.x + dx, c.y + dy);
                    if (!region.Contains(n)) ring.Add(n);
                }
        return ring;
    }

    /// <summary>
    /// 벽 셀의 타일을 바닥(region) 방향으로 결정. inner=true면 안쪽 링 타일, false면 바깥 링.
    ///   변: 바닥이 한쪽 직교 방향
    ///   볼록 코너: 직교 바닥 없이 대각만
    ///   오목 코너: 두 직교 방향 바닥 → (임시) 직선변 톤
    /// </summary>
    private string RingTile(Vector2Int c, HashSet<Vector2Int> region, bool inner)
    {
        bool n = region.Contains(new Vector2Int(c.x, c.y + 1));
        bool s = region.Contains(new Vector2Int(c.x, c.y - 1));
        bool e = region.Contains(new Vector2Int(c.x + 1, c.y));
        bool w = region.Contains(new Vector2Int(c.x - 1, c.y));

        // 직선 변
        if (s && !n && !e && !w) return inner ? "wall_6" : "wall_12"; // 위 벽
        if (n && !s && !e && !w) return inner ? "wall_8" : "wall_28"; // 아래 벽
        if (e && !n && !s && !w) return inner ? "wall_9" : "wall_19"; // 좌 벽
        if (w && !n && !s && !e) return inner ? "wall_7" : "wall_21"; // 우 벽

        // 볼록 코너 (직교 바닥 없음)
        if (!n && !s && !e && !w)
        {
            bool ne = region.Contains(new Vector2Int(c.x + 1, c.y + 1));
            bool nw = region.Contains(new Vector2Int(c.x - 1, c.y + 1));
            bool se = region.Contains(new Vector2Int(c.x + 1, c.y - 1));
            bool sw = region.Contains(new Vector2Int(c.x - 1, c.y - 1));
            if (se) return inner ? "wall_25" : "wall_11"; // 좌상
            if (sw) return inner ? "wall_22" : "wall_13"; // 우상
            if (ne) return inner ? "wall_24" : "wall_27"; // 좌하
            if (nw) return inner ? "wall_23" : "wall_29"; // 우하
            return inner ? "wall_8" : "wall_28";          // 고립(이론상 없음)
        }

        // 오목 코너: inner 링은 주변 바닥과 같은 바닥 타일로 채워, 복도 가장자리
        //  대각선 안쪽에 직선벽(wall_8 등)이 박히던 문제를 제거한다. (4방향·세로 복도 공통)
        //  바깥 링(outer)은 기존 직선벽 톤 유지. 꼭짓점은 DrawCorridorCorners에서 명시 배치.
        if (inner) return FloorTileName(c.x, c.y);
        if (s) return "wall_12";
        if (n) return "wall_28";
        if (e) return "wall_19";
        return "wall_21";
    }

    /// <summary>
    /// 바깥 링 중 '하단 변'(바로 위가 region) 셀 아래에 성벽 2줄을 붙인다.
    /// 단, 이미 바닥/링(복도 벽 19·21 등)이 있는 칸은 건너뛰어 복도 벽이 우선되게 한다.
    /// </summary>
    private void DrawCastle(HashSet<Vector2Int> outer, HashSet<Vector2Int> region)
    {
        bool IsBottom(Vector2Int c) =>
            outer.Contains(c) && region.Contains(new Vector2Int(c.x, c.y + 1));
        bool Occupied(Vector2Int c) => region.Contains(c) || outer.Contains(c);

        foreach (Vector2Int c in outer)
        {
            if (!IsBottom(c)) continue;
            bool L = !IsBottom(new Vector2Int(c.x - 1, c.y));
            bool R = !IsBottom(new Vector2Int(c.x + 1, c.y));
            Vector2Int up = new Vector2Int(c.x, c.y - 1);
            Vector2Int dn = new Vector2Int(c.x, c.y - 2);
            if (!Occupied(up)) SetTile(up.x, up.y, L ? "wall_30" : R ? "wall_32" : "wall_31"); // 윗줄
            if (!Occupied(dn)) SetTile(dn.x, dn.y, L ? "wall_33" : R ? "wall_35" : "wall_34"); // 아랫줄
        }
    }

    /// <summary>
    /// 방 하단 성벽의 좌우 끝(방 모서리 열 ox·ox+max)은 IsBottom이 아니라 비어 있다.
    /// 그 칸을 성벽 중간 타일(윗줄 31 / 아랫줄 34)로 채워 성벽이 방 폭 전체를 덮게 한다.
    /// 또한 그 안쪽으로 가장자리와 인접한 좌·우 끝 칸(ox+1·ox+max-1)은 DrawCastle이
    /// 끝캡(30/33·32/35)을 깔아두는데, 이를 중간 타일 31/34로 덮어써 가장자리와 매끄럽게 잇는다.
    /// (복도가 차지한 칸은 occupied로 걸러 건너뜀)
    /// </summary>
    private void DrawRoomCornerCastle(HashSet<Vector2Int> occupied)
    {
        int max = roomSize - 1;
        foreach (Room r in rooms.Values)
        {
            int ox = r.gridPos.x * Stride, oy = r.gridPos.y * Stride;
            foreach (int cx in new[] { ox, ox + 1, ox + max - 1, ox + max })
            {
                Vector2Int up = new Vector2Int(cx, oy - 1);
                Vector2Int dn = new Vector2Int(cx, oy - 2);
                if (!occupied.Contains(up)) SetTile(up.x, up.y, "wall_31");
                if (!occupied.Contains(dn)) SetTile(dn.x, dn.y, "wall_34");
            }
        }
    }

    /// <summary>
    /// 복도-방 연결부의 오목 꼭짓점에 코너 타일을 '꼭짓점당 1개'만 명시 배치한다.
    /// 가로 복도: 좌상10 좌하18 우상4 우하26 / 세로 복도: 좌상13 좌하29 우상11 우하27
    /// </summary>
    private void DrawCorridorCorners()
    {
        const int inset = 2;
        int gapStart = (roomSize - corridorWidth) / 2;
        int gapEnd = gapStart + corridorWidth - 1;

        foreach (Room r in rooms.Values)
        {
            int ox = r.gridPos.x * Stride, oy = r.gridPos.y * Stride;

            if (r.doorRight)
            {
                int xl = ox + roomSize - inset + 1;    // 좌측 코너 (안쪽으로 2칸 당김)
                int xr = ox + Stride + inset - 2;      // 우측 코너 (안쪽으로 2칸 당김)
                int yt = oy + gapEnd + 2;              // 위 코너
                int yb = oy + gapStart - 2;            // 아래 코너
                SetTile(xl, yt, "wall_10"); // 좌상
                SetTile(xl, yb, "wall_18"); // 좌하
                SetTile(xr, yt, "wall_4");  // 우상
                SetTile(xr, yb, "wall_26"); // 우하
            }
            if (r.doorUp)
            {
                int yb = oy + roomSize - inset + 1;    // 아래 코너 (안쪽으로 2칸 당김)
                int yt = oy + Stride + inset - 2;      // 위 코너 (안쪽으로 2칸 당김)
                int xl = ox + gapStart - 2;            // 좌 코너
                int xr = ox + gapEnd + 2;              // 우 코너
                SetTile(xl, yt, "wall_26"); // 좌상
                SetTile(xl, yb, "wall_4");  // 좌하
                SetTile(xr, yt, "wall_18"); // 우상
                SetTile(xr, yb, "wall_10"); // 우하
            }
        }
    }

    /// <summary>좌표 기반 결정적 분포로 wall_0~3 바닥 선택.</summary>
    private static string FloorTileName(int worldX, int worldY)
    {
        int v = (worldX * 73856093) ^ (worldY * 19349663);
        return "wall_" + (((v % 4) + 4) % 4);
    }

    private void SetTile(int x, int y, string tileName)
    {
        TileBase t = GetTileAsset(tileName);
        if (t != null) dungeonTilemap.SetTile(new Vector3Int(x, y, 0), t);
    }

    private TileBase GetTileAsset(string name)
    {
        if (tileCache.TryGetValue(name, out TileBase cached)) return cached;
        TileBase t = Resources.Load<TileBase>($"Tile/Wall/{name}");
        if (t == null)
            Debug.LogWarning($"[DungeonGenerator] 타일 '{name}' 로드 실패 (Resources/Tile/Wall/{name})");
        tileCache[name] = t;
        return t;
    }
}
