using UnityEngine;

/// <summary>
/// 던전 포탈 스크립트.
/// IInteractable을 구현하여 플레이어가 F키로 상호작용하면 던전으로 이동하거나
/// '던전 탐사' 퀘스트 목표를 달성합니다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class DungeonPortal : MonoBehaviour, IInteractable
{
    [Header("Settings")]
    [Tooltip("포탈 이름 (퀘스트 TargetId와 매칭)")]
    [SerializeField] private string _dungeonId = "Dungeon";
    
    [Tooltip("상호작용 프롬프트")]
    [SerializeField] private string _prompt = "F키: 던전 입장";

    [Tooltip("이동할 씬 이름 (비워두면 씬 이동 없이 퀘스트만 갱신)")]
    [SerializeField] private string _sceneToLoad;

    public string InteractionPrompt => _prompt;

    public bool CanInteract(GameObject player)
    {
        // 퀘스트 조건, 입장 아이템 조건 등 추가 가능
        return true;
    }

    public void Interact(GameObject player)
    {
        Debug.Log($"[DungeonPortal] 던전 입장: {_dungeonId}");

        // 1. 퀘스트 보고
        QuestEventBridge.ReportDungeonExplore(_dungeonId);

        // 2. 알림
        if (NotificationUI.Instance != null)
        {
            NotificationUI.Instance.ShowMessage("던전에 입장했습니다!");
        }

        // 3. 씬 이동 처리 (씬 이름이 있으면 로드)
        if (!string.IsNullOrEmpty(_sceneToLoad))
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(_sceneToLoad);
        }
        else
        {
            // 같은 씬 내 좌표 이동 처리 (예시: 임의의 위치로 텔레포트)
            // player.transform.position = _dungeonEntryPoint;
        }
    }
}
