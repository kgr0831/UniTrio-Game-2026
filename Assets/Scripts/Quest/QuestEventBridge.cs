using UnityEngine;

/// <summary>
/// 게임 이벤트를 QuestManager에 연결하는 브릿지.
/// 몬스터 처치, 아이템 수집 등의 게임 이벤트를 감지하여
/// QuestManager.ReportProgress를 호출합니다.
///
/// GameScene에 하나 배치합니다.
/// </summary>
public class QuestEventBridge : MonoBehaviour
{
    private bool _inventorySubscribed;

    private void OnEnable()
    {
        // 인벤토리 변경 감지 (아이템 수집 추적)
        Core.ItemEvents.OnInventoryChanged += OnInventoryChanged;
        _inventorySubscribed = true;
    }

    private void OnDisable()
    {
        if (_inventorySubscribed)
        {
            Core.ItemEvents.OnInventoryChanged -= OnInventoryChanged;
            _inventorySubscribed = false;
        }
    }

    /// <summary>
    /// 몬스터가 죽었을 때 호출합니다.
    /// MonsterBase 또는 HealthSystem.OnDied에서 이 메서드를 호출하세요.
    /// </summary>
    public static void ReportMonsterKill(string monsterName)
    {
        if (QuestManager.Instance != null)
            QuestManager.Instance.ReportProgress(QuestObjectiveType.Kill, monsterName);
    }

    /// <summary>
    /// 던전 입장/탐사를 보고합니다.
    /// </summary>
    public static void ReportDungeonExplore(string dungeonName)
    {
        if (QuestManager.Instance != null)
            QuestManager.Instance.ReportProgress(QuestObjectiveType.Explore, dungeonName);
    }

    /// <summary>
    /// 아이템 제작을 보고합니다.
    /// </summary>
    public static void ReportItemCrafted(string itemName)
    {
        if (QuestManager.Instance != null)
            QuestManager.Instance.ReportProgress(QuestObjectiveType.Craft, itemName);
    }

    /// <summary>
    /// 아이템 구매를 보고합니다.
    /// </summary>
    public static void ReportItemBought(string itemName)
    {
        if (QuestManager.Instance != null)
            QuestManager.Instance.ReportProgress(QuestObjectiveType.Buy, itemName);
    }

    /// <summary>
    /// 인벤토리 변경 시 Collect 타입 퀘스트 목표를 확인합니다.
    /// 주의: 증분이 아닌 총 보유량 기반으로 판단합니다.
    /// </summary>
    private void OnInventoryChanged()
    {
        if (QuestManager.Instance == null || InventoryManager.Instance == null) return;

        var activeQuests = QuestManager.Instance.GetActiveQuests();
        foreach (var q in activeQuests)
        {
            for (int i = 0; i < q.Objectives.Count; i++)
            {
                var obj = q.Objectives[i];
                if (obj.Type != QuestObjectiveType.Collect) continue;

                // TargetId를 아이템 ID(이름)로 취급하여 수량을 구함
                int currentAmount = InventoryManager.Instance.GetItemCountByTargetId(obj.TargetId);

                // 모자라면 진행도 갱신 시도 (증분량을 계산)
                int currentProgress = QuestManager.Instance.GetProgress(q.QuestId, i);
                if (currentProgress < obj.RequiredAmount && currentAmount > currentProgress)
                {
                    // ReportProgress는 증분을 받으므로 추가된 양만큼 보고
                    int diff = currentAmount - currentProgress;
                    QuestManager.Instance.ReportProgress(QuestObjectiveType.Collect, obj.TargetId, diff);
                }
            }
        }
    }
}
