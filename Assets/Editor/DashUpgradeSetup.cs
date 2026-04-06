using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// [Tools/Setup Dash Upgrade] 메뉴 실행 시:
/// 1. 씬의 Player에 DashAfterimagePool 컴포넌트 추가 (이미 있으면 스킵)
/// 2. DashHandler._isUpgraded = false 기본값 확인 안내
/// 3. 씬 저장
/// </summary>
public static class DashUpgradeSetup
{
    [MenuItem("Tools/Setup Dash Upgrade")]
    public static void Run()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            Debug.LogError("[DashUpgradeSetup] 'Player' 오브젝트를 씬에서 찾을 수 없습니다.");
            return;
        }

        // DashAfterimagePool: SpriteRenderer RequireComponent 확인
        SpriteRenderer sr = player.GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            Debug.LogError("[DashUpgradeSetup] Player에 SpriteRenderer가 없습니다. " +
                           "DashAfterimagePool을 추가하려면 SpriteRenderer가 필요합니다.");
            return;
        }

        // 이미 있으면 스킵
        DashAfterimagePool pool = player.GetComponent<DashAfterimagePool>();
        if (pool == null)
        {
            pool = player.AddComponent<DashAfterimagePool>();
            Debug.Log("[DashUpgradeSetup] DashAfterimagePool 컴포넌트 추가 완료.");
        }
        else
        {
            Debug.Log("[DashUpgradeSetup] DashAfterimagePool이 이미 존재합니다. 스킵.");
        }

        // DashHandler._isUpgraded 기본값(false) 상태 출력
        DashHandler dash = player.GetComponent<DashHandler>();
        if (dash != null)
        {
            var so = new SerializedObject(dash);
            var prop = so.FindProperty("_isUpgraded");
            if (prop != null)
                Debug.Log($"[DashUpgradeSetup] DashHandler._isUpgraded = {prop.boolValue} " +
                          "(Inspector에서 Is Upgraded 체크 시 업그레이드 대시 활성화)");
        }

        EditorUtility.SetDirty(player);
        EditorSceneManager.MarkSceneDirty(player.scene);
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("[DashUpgradeSetup] 완료. DashHandler의 'Is Upgraded'를 체크하면 잔상 VFX가 활성화됩니다.");
    }
}
