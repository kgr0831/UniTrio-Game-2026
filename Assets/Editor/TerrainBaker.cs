using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// MapGenerator의 절차적 지형(듀얼 그리드 비트마스크)을 에디터에서 Tilemap에 직접 베이크한다.
/// 런타임 MapGenerator는 건드리지 않고, 활성 씬의 MapGenerator 설정(biomes/noiseScale/chunkSize)을
/// 그대로 읽어 원점 중심 64x64 영역을 그려 넣는다. 결과는 씬에 저장된다(Ctrl+S).
/// 메뉴: Tools > Terrain > Bake 64x64
/// </summary>
public static class TerrainBaker
{
    // 원점 중심 64x64 (셀 -32 ~ +31)
    const int Half = 32;

    [MenuItem("Tools/Terrain/Bake 64x64")]
    static void Bake64()
    {
        var gen = Object.FindObjectOfType<MapGenerator>();
        if (gen == null)
        {
            EditorUtility.DisplayDialog("MapGenerator 없음",
                "활성 씬에서 MapGenerator를 찾지 못했습니다.\nUnitro2 씬을 열고 다시 실행하세요.", "확인");
            return;
        }
        if (gen.tilemap == null)
        {
            EditorUtility.DisplayDialog("Tilemap 미할당",
                "MapGenerator.tilemap이 비어 있습니다. 인스펙터에서 Tilemap을 할당하세요.", "확인");
            return;
        }
        if (gen.biomes == null || gen.biomes.Count == 0)
        {
            EditorUtility.DisplayDialog("Biome 없음", "MapGenerator.biomes가 비어 있습니다.", "확인");
            return;
        }

        BakeRegion(gen, -Half, -Half, Half * 2, Half * 2);

        var scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[TerrainBaker] {Half * 2}x{Half * 2} 베이크 완료 (셀 -{Half}~+{Half - 1}). 저장하려면 Ctrl+S.");
    }

    static void BakeRegion(MapGenerator gen, int minX, int minY, int width, int height)
    {
        // 1. threshold 계산 (런타임 CalculateThresholds와 동일 로직)
        float total = gen.biomes.Sum(b => b.weight);
        var threshold = new float[gen.biomes.Count];
        float current = 0f;
        for (int i = 0; i < gen.biomes.Count; i++)
        {
            current += gen.biomes[i].weight / total;
            threshold[i] = current;
        }

        // 2. 데이터 테이블 생성 (경계 비트마스크용으로 +1 크게). 인덱스 오프셋 적용.
        int dw = width + 1, dh = height + 1;
        var data = new int[dw, dh];
        for (int x = 0; x < dw; x++)
            for (int y = 0; y < dh; y++)
                data[x, y] = BiomeIndexAt(gen, threshold, minX + x, minY + y);

        // 3. 셀마다 dominant biome + 비트마스크 계산 후 SetTile (RenderDualTileFromTable과 동일)
        var tm = gen.tilemap;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int blIdx = data[x, y];
                int brIdx = data[x + 1, y];
                int tlIdx = data[x, y + 1];
                int trIdx = data[x + 1, y + 1];

                var bl = gen.biomes[blIdx];
                var br = gen.biomes[brIdx];
                var tl = gen.biomes[tlIdx];
                var tr = gen.biomes[trIdx];

                int maxP = Mathf.Max(bl.priority, br.priority, tl.priority, tr.priority);
                BiomeSetting dominant = (tl.priority == maxP) ? tl :
                                        (tr.priority == maxP) ? tr :
                                        (bl.priority == maxP) ? bl : br;

                int mask = 0;
                if (tl.priority == maxP) mask += 8;
                if (tr.priority == maxP) mask += 4;
                if (bl.priority == maxP) mask += 2;
                if (br.priority == maxP) mask += 1;

                if (dominant.tiles != null && mask < dominant.tiles.Length)
                    tm.SetTile(new Vector3Int(minX + x, minY + y, 0), dominant.tiles[mask]);
            }
        }
        tm.RefreshAllTiles();
    }

    // 런타임 MapGenerator.GetBiomeIndexAt와 동일하되, 에디터에서 계산한 threshold 사용
    static int BiomeIndexAt(MapGenerator gen, float[] threshold, int x, int y)
    {
        float noiseValue = Mathf.PerlinNoise(x * gen.noiseScale, y * gen.noiseScale);
        for (int i = 0; i < gen.biomes.Count; i++)
            if (noiseValue <= threshold[i]) return i;
        return gen.biomes.Count - 1;
    }
}
