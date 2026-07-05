using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Unitro2 씬이 열려 있고 WallField가 없거나 비어 있으면, 재컴파일/씬오픈 시
/// 자동으로 30x20 벽 필드(Grid 셀 1.5)를 메모리 씬에 직접 그려 넣는다.
/// 디스크 reload 여부와 무관하게 에디터에 바로 표시된다.
/// 수동 실행: Tools > Wall Field > Build Now
/// </summary>
[InitializeOnLoad]
public static class WallFieldBuilder
{
    const string WallPng     = "Assets/Resources/Dummy/wall.png";
    const string WallDir     = "Assets/Resources/Tile/Wall";
    const string TargetScene = "Assets/Scenes/Unitro2.unity";
    const int W = 30, H = 20;
    static readonly Vector3 CellSize = new Vector3(1.5f, 1.5f, 0f);

    static WallFieldBuilder()
    {
        // 자동 생성 비활성화: 로그라이크 던전과 겹쳐서 끔. 수동 빌드(Tools 메뉴)는 그대로 유지.
        // EditorApplication.delayCall += AutoBuild;
    }

    static void AutoBuild()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetActiveScene();
        if (scene.path != TargetScene) return;

        var existing = GameObject.Find("WallField");
        if (existing != null)
        {
            var tmOld = existing.GetComponentInChildren<Tilemap>();
            if (tmOld != null && tmOld.GetUsedTilesCount() > 0) return; // 이미 정상 → 그대로
            Object.DestroyImmediate(existing);                          // 비어있으면 다시 생성
        }

        Build();
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[WallFieldBuilder] 필드 자동 생성 완료. 저장하려면 Ctrl+S.");
    }

    [MenuItem("Tools/Wall Field/Build Now")]
    static void BuildMenu()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != TargetScene)
        {
            EditorUtility.DisplayDialog("씬 확인", $"{TargetScene} 를 먼저 열어주세요.\n현재: {scene.path}", "확인");
            return;
        }
        var existing = GameObject.Find("WallField");
        if (existing != null) Object.DestroyImmediate(existing);
        Build();
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[WallFieldBuilder] 필드 생성 완료. Ctrl+S로 저장.");
    }

    static void Build()
    {
        var gridGo = new GameObject("WallField");
        var grid = gridGo.AddComponent<Grid>();
        grid.cellSize = CellSize;

        var tmGo = new GameObject("Field");
        tmGo.transform.SetParent(gridGo.transform);
        var tm = tmGo.AddComponent<Tilemap>();
        tmGo.AddComponent<TilemapRenderer>();

        var cache = new Dictionary<string, TileBase>();
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
            {
                var t = GetTile(TileName(x, y), cache);
                if (t != null) tm.SetTile(new Vector3Int(x, y, 0), t);
            }
        tm.RefreshAllTiles();
    }

    // 배치 규칙: 상단 11/12/13, 좌우 19/21, 하단캡 27/28/29, 정면 30~35(좌끝33·우끝35), 내부 20
    static string TileName(int x, int y)
    {
        bool l = x == 0, r = x == W - 1;
        if (y == H - 1) return l ? "wall_11" : r ? "wall_13" : "wall_12";
        if (y == 2)     return l ? "wall_27" : r ? "wall_29" : "wall_28";
        if (y == 1)     return l ? "wall_30" : r ? "wall_32" : "wall_31";
        if (y == 0)     return l ? "wall_33" : r ? "wall_35" : "wall_34";
        return l ? "wall_19" : r ? "wall_21" : "wall_20";
    }

    static TileBase GetTile(string name, Dictionary<string, TileBase> cache)
    {
        if (cache.TryGetValue(name, out var c)) return c;

        TileBase t = AssetDatabase.LoadAssetAtPath<TileBase>($"{WallDir}/{name}.asset");
        if (t == null)
        {
            // 폴백: Tile 에셋이 아직 없으면 wall.png 스프라이트로 즉석 Tile 생성
            foreach (var o in AssetDatabase.LoadAllAssetRepresentationsAtPath(WallPng))
            {
                if (o is Sprite s && s.name == name)
                {
                    var tile = ScriptableObject.CreateInstance<Tile>();
                    tile.sprite = s;
                    tile.colliderType = Tile.ColliderType.None;
                    t = tile;
                    break;
                }
            }
        }
        cache[name] = t;
        return t;
    }
}
