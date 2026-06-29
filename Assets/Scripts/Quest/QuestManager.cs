using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 범용 퀘스트 매니저 싱글턴.
/// 모든 퀘스트의 상태(Locked → Available → Active → Completed)를 관리하고,
/// 목표 진행 이벤트를 수신하여 자동으로 완료 감지합니다.
///
/// 기존 TutorialManager의 퀘스트 로직을 대체/보완합니다.
/// </summary>
public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    // ── 이벤트 ──────────────────────────────────────────
    /// <summary>퀘스트가 Active로 전환될 때.</summary>
    public event Action<string> OnQuestStarted;
    /// <summary>퀘스트 목표 진행 갱신 시.</summary>
    public event Action<string, int> OnQuestObjectiveUpdated; // questId, objectiveIndex
    /// <summary>퀘스트 완료 시.</summary>
    public event Action<string> OnQuestCompleted;
    /// <summary>퀘스트 상태 변경 시 (UI 갱신용).</summary>
    public event Action OnQuestStateChanged;

    [Header("등록된 퀘스트")]
    [Tooltip("게임에 존재하는 모든 QuestSO를 등록합니다.")]
    [SerializeField] private List<QuestSO> _allQuests = new List<QuestSO>();

    // 런타임 상태
    private readonly Dictionary<string, QuestSO> _questById = new Dictionary<string, QuestSO>();
    private readonly Dictionary<string, QuestState> _states = new Dictionary<string, QuestState>();
    private readonly Dictionary<string, int[]> _progress = new Dictionary<string, int[]>(); // objectiveIndex → currentAmount

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        foreach (var q in _allQuests)
        {
            if (q == null || string.IsNullOrEmpty(q.QuestId)) continue;
            _questById[q.QuestId] = q;
            _states[q.QuestId] = q.PrerequisiteQuestIds.Count == 0 ? QuestState.Available : QuestState.Locked;
            _progress[q.QuestId] = new int[q.Objectives.Count];
        }
    }

    private void Start()
    {
        // 자동 시작 퀘스트 처리
        foreach (var q in _allQuests)
        {
            if (q != null && q.AutoStart && GetState(q.QuestId) == QuestState.Available)
                StartQuest(q.QuestId);
        }
    }

    // ── 조회 ─────────────────────────────────────────────

    /// <summary>퀘스트 SO를 ID로 조회합니다.</summary>
    public QuestSO GetQuest(string questId)
    {
        _questById.TryGetValue(questId, out var q);
        return q;
    }

    /// <summary>퀘스트 상태를 조회합니다.</summary>
    public QuestState GetState(string questId)
    {
        return _states.TryGetValue(questId, out var s) ? s : QuestState.Locked;
    }

    /// <summary>퀘스트 목표 진행량을 조회합니다.</summary>
    public int GetProgress(string questId, int objectiveIndex)
    {
        if (!_progress.TryGetValue(questId, out var arr)) return 0;
        return objectiveIndex >= 0 && objectiveIndex < arr.Length ? arr[objectiveIndex] : 0;
    }

    /// <summary>활성(Active) 퀘스트 목록을 반환합니다.</summary>
    public List<QuestSO> GetActiveQuests()
    {
        var list = new List<QuestSO>();
        foreach (var q in _allQuests)
        {
            if (q != null && GetState(q.QuestId) == QuestState.Active)
                list.Add(q);
        }
        list.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
        return list;
    }

    /// <summary>특정 카테고리의 활성 퀘스트를 반환합니다.</summary>
    public List<QuestSO> GetActiveQuests(QuestCategory category)
    {
        var list = new List<QuestSO>();
        foreach (var q in _allQuests)
        {
            if (q != null && q.Category == category && GetState(q.QuestId) == QuestState.Active)
                list.Add(q);
        }
        list.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
        return list;
    }

    // ── 상태 전이 ────────────────────────────────────────

    /// <summary>퀘스트를 시작합니다 (Available → Active).</summary>
    public bool StartQuest(string questId)
    {
        if (GetState(questId) != QuestState.Available)
        {
            Debug.LogWarning($"[QuestManager] 퀘스트 시작 불가 — {questId} 상태: {GetState(questId)}");
            return false;
        }

        _states[questId] = QuestState.Active;
        Debug.Log($"[QuestManager] 퀘스트 시작: {questId}");
        OnQuestStarted?.Invoke(questId);
        OnQuestStateChanged?.Invoke();
        return true;
    }

    /// <summary>퀘스트를 해금합니다 (Locked → Available). AutoStart면 즉시 Active.</summary>
    public void UnlockQuest(string questId)
    {
        if (GetState(questId) != QuestState.Locked) return;

        _states[questId] = QuestState.Available;
        Debug.Log($"[QuestManager] 퀘스트 해금: {questId}");

        var q = GetQuest(questId);
        if (q != null && q.AutoStart)
            StartQuest(questId);

        OnQuestStateChanged?.Invoke();
    }

    /// <summary>퀘스트를 강제 완료합니다. 보상을 지급하고 후속 퀘스트를 해금합니다.</summary>
    public void CompleteQuest(string questId)
    {
        if (GetState(questId) != QuestState.Active)
        {
            Debug.LogWarning($"[QuestManager] 퀘스트 완료 불가 — {questId} 상태: {GetState(questId)}");
            return;
        }

        _states[questId] = QuestState.Completed;
        Debug.Log($"[QuestManager] 퀘스트 완료: {questId}");

        var quest = GetQuest(questId);
        if (quest != null)
            GrantRewards(quest.Reward);

        // 후속 퀘스트 해금 (보상에 지정된 것 + 선행 조건 체크)
        if (quest?.Reward.UnlockQuestIds != null)
        {
            foreach (var unlockId in quest.Reward.UnlockQuestIds)
                TryUnlock(unlockId);
        }

        // 모든 퀘스트의 선행 조건 재확인
        foreach (var q in _allQuests)
        {
            if (q != null && GetState(q.QuestId) == QuestState.Locked)
                TryUnlock(q.QuestId);
        }

        OnQuestCompleted?.Invoke(questId);
        OnQuestStateChanged?.Invoke();
    }

    // ── 목표 진행 ────────────────────────────────────────

    /// <summary>
    /// 특정 타입의 목표에 진행량을 추가합니다.
    /// Kill, Collect 등 게임 이벤트 발생 시 호출합니다.
    /// </summary>
    /// <param name="type">목표 타입</param>
    /// <param name="targetId">대상 ID (MonsterData.name, ItemData 이름 등)</param>
    /// <param name="amount">추가할 양</param>
    public void ReportProgress(QuestObjectiveType type, string targetId, int amount = 1)
    {
        foreach (var q in _allQuests)
        {
            if (q == null || GetState(q.QuestId) != QuestState.Active) continue;

            var prog = _progress[q.QuestId];
            bool anyUpdated = false;

            for (int i = 0; i < q.Objectives.Count; i++)
            {
                var obj = q.Objectives[i];
                if (obj.Type != type) continue;
                if (!string.IsNullOrEmpty(obj.TargetId) && obj.TargetId != targetId) continue;
                if (prog[i] >= obj.RequiredAmount) continue; // 이미 달성

                prog[i] = Mathf.Min(prog[i] + amount, obj.RequiredAmount);
                anyUpdated = true;
                OnQuestObjectiveUpdated?.Invoke(q.QuestId, i);
            }

            if (anyUpdated)
            {
                OnQuestStateChanged?.Invoke();
                CheckAutoComplete(q.QuestId);
            }
        }
    }

    /// <summary>특정 퀘스트의 특정 목표를 직접 완료 처리합니다 (Custom 타입용).</summary>
    public void CompleteObjective(string questId, int objectiveIndex)
    {
        if (GetState(questId) != QuestState.Active) return;
        var q = GetQuest(questId);
        if (q == null || objectiveIndex < 0 || objectiveIndex >= q.Objectives.Count) return;

        _progress[questId][objectiveIndex] = q.Objectives[objectiveIndex].RequiredAmount;
        OnQuestObjectiveUpdated?.Invoke(questId, objectiveIndex);
        OnQuestStateChanged?.Invoke();
        CheckAutoComplete(questId);
    }

    /// <summary>모든 목표가 달성되었으면 자동으로 퀘스트를 완료합니다.</summary>
    private void CheckAutoComplete(string questId)
    {
        var q = GetQuest(questId);
        if (q == null) return;
        var prog = _progress[questId];

        for (int i = 0; i < q.Objectives.Count; i++)
        {
            if (prog[i] < q.Objectives[i].RequiredAmount) return;
        }

        CompleteQuest(questId);
    }

    // ── 내부 ─────────────────────────────────────────────

    private void TryUnlock(string questId)
    {
        if (GetState(questId) != QuestState.Locked) return;
        var q = GetQuest(questId);
        if (q == null) return;

        foreach (var preId in q.PrerequisiteQuestIds)
        {
            if (GetState(preId) != QuestState.Completed) return;
        }
        UnlockQuest(questId);
    }

    private void GrantRewards(QuestReward reward)
    {
        if (reward == null) return;

        if (reward.Gold > 0 && GoldManager.Instance != null)
        {
            GoldManager.Instance.AddGold(reward.Gold);
            if (NotificationUI.Instance != null)
                NotificationUI.Instance.ShowMessage($"{reward.Gold} 골드를 획득했습니다!");
        }

        if (reward.Items != null && InventoryManager.Instance != null)
        {
            foreach (var ri in reward.Items)
            {
                if (ri.Item != null)
                {
                    InventoryManager.Instance.AddItem(ri.Item, ri.Count);
                    if (NotificationUI.Instance != null)
                        NotificationUI.Instance.ShowMessage($"{ri.Item.Name} ×{ri.Count} 획득!");
                }
            }
        }
    }

    /// <summary>외부에서 퀘스트를 동적으로 등록합니다.</summary>
    public void RegisterQuest(QuestSO quest)
    {
        if (quest == null || string.IsNullOrEmpty(quest.QuestId)) return;
        if (_questById.ContainsKey(quest.QuestId)) return;

        _allQuests.Add(quest);
        _questById[quest.QuestId] = quest;
        _states[quest.QuestId] = quest.PrerequisiteQuestIds.Count == 0 ? QuestState.Available : QuestState.Locked;
        _progress[quest.QuestId] = new int[quest.Objectives.Count];

        if (quest.AutoStart && GetState(quest.QuestId) == QuestState.Available)
            StartQuest(quest.QuestId);
    }
}
