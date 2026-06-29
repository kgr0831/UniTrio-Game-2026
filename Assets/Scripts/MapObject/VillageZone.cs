using UnityEngine;

/// <summary>
/// 마을 영역 감지 트리거.
/// 플레이어가 마을 테두리 3타일 이내에 접근하면 QuestManager에
/// Reach 타입 목표 달성을 보고합니다.
///
/// 별도의 Collider2D(Trigger)를 사용합니다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class VillageZone : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("마을 이름 (QuestObjective.TargetId와 매칭)")]
    [SerializeField] private string _villageName = "Village";
    [Tooltip("플레이어 태그")]
    [SerializeField] private string _playerTag = "Player";
    [Tooltip("발견 시 알림 메시지")]
    [SerializeField] private string _discoveryMessage = "마을을 발견했습니다!";

    [Header("마을 활성화")]
    [Tooltip("마을 발견 전까지 비활성화할 NPC/건물 오브젝트들")]
    [SerializeField] private GameObject[] _villageObjects;
    [Tooltip("마을 발견 전에도 보여줄지 여부 (false면 발견 시 활성화)")]
    [SerializeField] private bool _alwaysVisible = true;

    private bool _discovered;

    private void Awake()
    {
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;

        if (!_alwaysVisible && _villageObjects != null)
        {
            foreach (var obj in _villageObjects)
                if (obj != null) obj.SetActive(false);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_discovered) return;
        if (!other.CompareTag(_playerTag)) return;

        _discovered = true;
        Debug.Log($"[VillageZone] 마을 발견: {_villageName}");

        // 마을 오브젝트 활성화
        if (_villageObjects != null)
        {
            foreach (var obj in _villageObjects)
                if (obj != null) obj.SetActive(true);
        }

        // 알림
        if (NotificationUI.Instance != null)
            NotificationUI.Instance.ShowMessage(_discoveryMessage);

        // 퀘스트 목표 보고
        if (QuestManager.Instance != null)
            QuestManager.Instance.ReportProgress(QuestObjectiveType.Reach, _villageName);
    }

    /// <summary>이미 발견된 상태인지 반환합니다.</summary>
    public bool IsDiscovered => _discovered;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 0.3f);
        var col = GetComponent<Collider2D>();
        if (col != null)
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
    }
}
