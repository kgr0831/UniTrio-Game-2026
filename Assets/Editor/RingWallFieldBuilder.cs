using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// wall.png(wall_0~35)로 20x20 정사각형 벽 필드를 동심(ring) 구조로 그린다.
/// 구조(y가 위쪽):
///   바깥 링 : 상12 / 하28 / 좌19 / 우21,  꼭짓점 좌상11·우상13·좌하27·우하29
///   안쪽 링 : 상6  / 하8  / 좌9  / 우7 ,  꼭짓점 좌상25·우상22·좌하24·우하23
///   중앙    : wall_0~3 랜덤 바닥
///   최하단  : wall_30~35 성벽 2줄(30·31·32 위 / 33·34·35 아래)
///
/// 기존 WallFieldBuilder(자동 생성)와 충돌하지 않도록 별도 오브젝트 "RingWallField"를 만들고
/// 메뉴 실행 전용이다(자동 빌드 없음).
/// 메뉴: Tools > Wall Field > Build Ring Field (20x20)
/// </summary>
public static class RingWallFieldBuilder
{
    const string WallPng     = "Assets/Resources/Dummy/wall.png";
    const string WallDir     = "Assets/Resources/Tile/Wall";
    const string TargetScene = "Assets/Scenes/Unitro2.unity";
    const string RootName    = "RingWallField";
    const int W = 20, H = 20;
    static readonly Vector3 CellSize = new Vector3(1.5f, 1.5f, 0f);

    [MenuItem("Tools/Wall Field/Build Ring Field (20x20)")]
    static void BuildMenu()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != TargetScene)
        {
            EditorUtility.DisplayDialog("씬 확인",
                $"{TargetScene} 를 먼저 열어주세요.\n현재: {scene.path}", "확인");
            return;
        }

        var existing = GameObject.Find(RootName);
        if (existing != null) Object.DestroyImmediate(existing);

        Build();
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[RingWallFieldBuilder] 20x20 동심 벽 필드 생성 완료. 저장하려면 Ctrl+S.");
    }

    static void Build()
    {
        var gridGo = new GameObject(RootName);
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

    /// <summary>
    /// 20x20 좌표(x:0=좌~19=우, y:0=하~19=상)별 타일 이름 결정.
    /// </summary>
    static string TileName(int x, int y)
    {
        bool oL = x == 0,     oR = x == W - 1;   // 바깥 좌/우 열
        bool iL = x == 1,     iR = x == W - 2;   // 안쪽 좌/우 열

        // ── 최하단 성벽 2줄 ──
        if (y == 0) return oL ? "wall_33" : oR ? "wall_35" : "wall_34";
        if (y == 1) return oL ? "wall_30" : oR ? "wall_32" : "wall_31";

        // ── 바깥 링 (이 줄 위 영역의 테두리) ──
        if (y == 2)        return oL ? "wall_27" : oR ? "wall_29" : "wall_28"; // 바깥 하단
        if (y == H - 1)    return oL ? "wall_11" : oR ? "wall_13" : "wall_12"; // 바깥 상단
        if (oL) return "wall_19";  // 바깥 좌
        if (oR) return "wall_21";  // 바깥 우

        // ── 안쪽 링 ──
        if (y == 3)        return iL ? "wall_24" : iR ? "wall_23" : "wall_8";  // 안쪽 하단
        if (y == H - 2)    return iL ? "wall_25" : iR ? "wall_22" : "wall_6";  // 안쪽 상단
        if (iL) return "wall_9";   // 안쪽 좌
        if (iR) return "wall_7";   // 안쪽 우

        // ── 중앙 바닥 wall_0~3 랜덤 (좌표 기반 결정적 분포) ──
        int v = (x * 73856093) ^ (y * 19349663);
        return "wall_" + (((v % 4) + 4) % 4);
    }

    static TileBase GetTile(string name, Dictionary<string, TileBase> cache)
    {
        if (cache.TryGetValue(name, out var c)) return c;

        TileBase t = AssetDatabase.LoadAssetAtPath<TileBase>($"{WallDir}/{name}.asset");
        if (t == null)
        {
            // 폴백: Tile 에셋이 없으면 wall.png 스프라이트로 즉석 Tile 생성
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
        if (t == null)
            Debug.LogWarning($"[RingWallFieldBuilder] '{name}' 스프라이트를 찾지 못했습니다.");
        cache[name] = t;
        return t;
    }
}
