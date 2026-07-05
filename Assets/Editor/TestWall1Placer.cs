using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// [테스트용] wall.png의 wall_1 스프라이트를 현재 열린 씬의 원점에 GameObject로 배치한다.
/// reload/배치 파이프라인 점검용. 확인 후 삭제해도 무방.
/// 메뉴: Tools > Terrain > Test Place Wall_1
/// </summary>
public static class TestWall1Placer
{
    const string WallPng = "Assets/Resources/Dummy/wall.png";
    const string SpriteName = "wall_1";
    const string GoName = "TestWall1";

    [MenuItem("Tools/Terrain/Test Place Wall_1")]
    static void Place()
    {
        Sprite sprite = null;
        foreach (var o in AssetDatabase.LoadAllAssetRepresentationsAtPath(WallPng))
        {
            if (o is Sprite s && s.name == SpriteName) { sprite = s; break; }
        }
        if (sprite == null)
        {
            EditorUtility.DisplayDialog("스프라이트 없음",
                $"{WallPng} 안에서 '{SpriteName}' 스프라이트를 찾지 못했습니다.", "확인");
            return;
        }

        var existing = GameObject.Find(GoName);
        if (existing != null) Object.DestroyImmediate(existing);

        var go = new GameObject(GoName);
        go.transform.position = Vector3.zero;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 100; // 다른 타일 위에 보이도록

        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[TestWall1Placer] '{SpriteName}'를 원점(0,0,0)에 배치 완료. 저장하려면 Ctrl+S.");
    }
}
