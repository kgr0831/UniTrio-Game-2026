using UnityEditor;
using UnityEngine;

/// <summary>
/// "골렘 구역(GolemZone)" 프리팹을 생성하고 씬에 배치하는 에디터 유틸리티.
///
/// 구성:
///   · 트리거 CircleCollider2D (구역 범위)
///   · GolemZone 컴포넌트 (+ Golem 프리팹 참조 자동 연결)
///   · Assets/Resources/Prefabs/GolemZone.prefab 으로 저장
///
/// 벽(GolemWallRing)은 런타임 동적 생성이므로 프리팹에 포함하지 않는다.
/// </summary>
public static class GolemZoneSetup
{
    private const string GolemPrefabPath = "Assets/Resources/Prefabs/Test/Golem.prefab";
    private const string SavePath        = "Assets/Resources/Prefabs/GolemZone.prefab";

    [MenuItem("Tools/Golem Zone/Create Prefab + Place In Scene")]
    public static void CreateAndPlace()
    {
        GameObject prefab = CreatePrefabAsset();
        if (prefab == null) return;

        // 활성 씬에 인스턴스 배치 (프리팹 인스턴스로 연결)
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.position = Vector3.zero;
        Undo.RegisterCreatedObjectUndo(instance, "Create GolemZone");
        Selection.activeGameObject = instance;

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(instance.scene);
        Debug.Log("[GolemZoneSetup] GolemZone 프리팹 생성 + 씬 배치 완료. " +
                  "위치를 원하는 구역으로 옮기고, 필요 시 Activation Radius / Wall Radius 를 조정하세요.");
    }

    [MenuItem("Tools/Golem Zone/Create Prefab Only")]
    public static GameObject CreatePrefabAsset()
    {
        // 골렘 프리팹 로드
        var golemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GolemPrefabPath);
        if (golemPrefab == null)
            Debug.LogWarning($"[GolemZoneSetup] 골렘 프리팹을 찾지 못했습니다: {GolemPrefabPath} " +
                             "(나중에 GolemZone 인스펙터에서 직접 할당하세요.)");

        // 임시 루트 오브젝트 구성
        var root = new GameObject("GolemZone");

        var col = root.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius    = 24f; // 구역(트리거) 범위. 발동은 경계로부터 Activation Depth만큼 들어왔을 때.

        var zone = root.AddComponent<GolemZone>();

        // private [SerializeField] 참조/값 주입
        var so = new SerializedObject(zone);
        SetObjectRef(so, "_golemPrefab", golemPrefab);
        SetObjectRef(so, "_damageTextPrefab",
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/PlayerAttack/DmgText.prefab"));
        SetString(so, "_playerTag", "Player");
        SetFloat(so, "_activationDepth", 1.5f);
        SetFloat(so, "_centerOffsetRatio", 0.8f);
        SetFloat(so, "_wipeDamage", 99999f);
        SetFloat(so, "_wallRadius", 24f);
        SetInt(so, "_wallSegments", 44);
        SetFloat(so, "_golemActivateDelay", 1f);
        so.ApplyModifiedPropertiesWithoutUndo();

        // 프리팹으로 저장 (폴더 보장)
        EnsureFolder("Assets/Resources/Prefabs");
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, SavePath);
        Object.DestroyImmediate(root); // 임시 오브젝트 정리

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (prefab != null)
            Debug.Log($"[GolemZoneSetup] 프리팹 저장 완료: {SavePath}");
        else
            Debug.LogError($"[GolemZoneSetup] 프리팹 저장 실패: {SavePath}");

        return prefab;
    }

    // ── SerializedObject 헬퍼 ──────────────────────────────────────────────
    private static void SetObjectRef(SerializedObject so, string prop, Object value)
    {
        var p = so.FindProperty(prop);
        if (p != null) p.objectReferenceValue = value;
    }
    private static void SetString(SerializedObject so, string prop, string value)
    {
        var p = so.FindProperty(prop);
        if (p != null) p.stringValue = value;
    }
    private static void SetFloat(SerializedObject so, string prop, float value)
    {
        var p = so.FindProperty(prop);
        if (p != null) p.floatValue = value;
    }
    private static void SetInt(SerializedObject so, string prop, int value)
    {
        var p = so.FindProperty(prop);
        if (p != null) p.intValue = value;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        var leaf   = System.IO.Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
