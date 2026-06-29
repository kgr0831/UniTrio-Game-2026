using UnityEngine;

/// <summary>
/// NPC 상호작용 컴포넌트.
/// IInteractable을 구현하여 기존 PlayerInteractionDetector(F키)와 연동됩니다.
///
/// NPC에게 대화, 퀘스트 부여/완료, 상점 열기 등의 기능을 제공합니다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class NpcInteractable : MonoBehaviour, IInteractable
{
    [Header("NPC 정보")]
    [Tooltip("NPC 이름 (퀘스트 GiverNpcName과 매칭)")]
    [SerializeField] private string _npcName;
    [Tooltip("상호작용 프롬프트 텍스트")]
    [SerializeField] private string _prompt = "F키: 대화";

    [Header("대화")]
    [Tooltip("대화 DB (ID→DialogueSO)")]
    [SerializeField] private DialogueDatabase _dialogueDb;
    [Tooltip("대화 플레이어")]
    [SerializeField] private DialoguePlayer _dialoguePlayer;
    [Tooltip("첫 대화 ID (퀘스트와 무관한 일반 인사)")]
    [SerializeField] private string _greetingDialogueId;
    [Tooltip("인사 대화 이후 재방문 시 사용할 대화 ID")]
    [SerializeField] private string _revisitDialogueId;

    [Header("상점 (상인 전용)")]
    [Tooltip("상점 UI 패널 (상인 NPC만)")]
    [SerializeField] private GameObject _shopPanel;

    [Header("NPC 타입")]
    [SerializeField] private NpcType _type = NpcType.Generic;

    public string NpcName => _npcName;
    public NpcType Type => _type;
    public string InteractionPrompt => _prompt;

    private bool               _hasGreeted;
    private PlayerStateMachine _playerFsm; // 대화 중 입력 잠금용

    public bool CanInteract(GameObject player)
    {
        // 대화 중이면 상호작용 불가
        if (_dialoguePlayer != null && _dialoguePlayer.IsPlaying) return false;

        // 대화/상호작용이 실제로 가능한 상태인지 판별
        if (FindCompletableQuest() != null) return true;
        if (FindAvailableQuest() != null) return true;
        if (_type == NpcType.Merchant && _shopPanel != null) return true;
        
        if (_hasGreeted && !string.IsNullOrEmpty(_revisitDialogueId)) return true;
        if (!_hasGreeted && !string.IsNullOrEmpty(_greetingDialogueId)) return true;

        // 위 조건에 모두 해당하지 않으면 대화 불가 (프롬프트 숨김)
        return false;
    }

    public void Interact(GameObject player)
    {
        // 플레이어 FSM 캐시 (대화 중 입력 잠금에 사용)
        if (_playerFsm == null && player != null)
            _playerFsm = player.GetComponentInParent<PlayerStateMachine>();

        if (QuestManager.Instance == null)
        {
            PlayGreeting();
            return;
        }

        // 1. 완료 가능한 퀘스트 확인 (이 NPC가 부여한 퀘스트 중 모든 목표 달성된 것)
        string completableQuestId = FindCompletableQuest();
        if (completableQuestId != null)
        {
            var quest = QuestManager.Instance.GetQuest(completableQuestId);
            if (quest != null && !string.IsNullOrEmpty(quest.CompleteDialogueId))
                PlayDialogue(quest.CompleteDialogueId);
            QuestManager.Instance.CompleteQuest(completableQuestId);
            return;
        }

        // 2. 부여 가능한 퀘스트 확인 (Available 상태이고 이 NPC가 부여자)
        string availableQuestId = FindAvailableQuest();
        if (availableQuestId != null)
        {
            var quest = QuestManager.Instance.GetQuest(availableQuestId);
            if (quest != null && !string.IsNullOrEmpty(quest.StartDialogueId))
                PlayDialogue(quest.StartDialogueId);
            QuestManager.Instance.StartQuest(availableQuestId);
            return;
        }

        // 3. 상인이면 상점 열기
        if (_type == NpcType.Merchant && _shopPanel != null)
        {
            _shopPanel.SetActive(true);
            return;
        }

        // 4. 기본 인사 대화
        PlayGreeting();
    }

    private string FindCompletableQuest()
    {
        if (QuestManager.Instance == null) return null;

        // 이 NPC가 부여한 활성 퀘스트 중 모든 목표 달성된 것 찾기
        var activeQuests = QuestManager.Instance.GetActiveQuests();
        foreach (var q in activeQuests)
        {
            if (q.GiverNpcName != _npcName) continue;

            bool allDone = true;
            for (int i = 0; i < q.Objectives.Count; i++)
            {
                if (QuestManager.Instance.GetProgress(q.QuestId, i) < q.Objectives[i].RequiredAmount)
                {
                    allDone = false;
                    break;
                }
            }
            if (allDone) return q.QuestId;
        }
        return null;
    }

    private string FindAvailableQuest()
    {
        if (QuestManager.Instance == null) return null;

        // 등록된 퀘스트 중 Available이고 이 NPC가 부여자인 것
        // (QuestManager 내부에서 조회 — 여기서는 전체 순회)
        // QuestManager에 공개 메서드가 없으므로 직접 순회하는 대신 간접 접근
        // → _allQuests는 private이므로 GetQuest 사용 불가, 이벤트 기반으로 전환
        // 간단한 방법: QuestManager에 메서드 추가하기보다, 여기서 직접 확인

        // 방어적 처리: QuestManager._allQuests에 접근 못하므로 알려진 퀘스트 ID를 순회할 수 없음
        // → NPC에 할당할 수 있는 퀘스트 목록을 인스펙터에서 관리
        foreach (var qId in _assignedQuestIds)
        {
            if (QuestManager.Instance.GetState(qId) == QuestState.Available)
                return qId;
        }
        return null;
    }

    [Header("할당된 퀘스트")]
    [Tooltip("이 NPC가 부여할 수 있는 퀘스트 ID 목록 (우선순위 순)")]
    [SerializeField] private string[] _assignedQuestIds;

    private void PlayDialogue(string dialogueId)
    {
        if (_dialoguePlayer == null || _dialogueDb == null) return;
        var so = _dialogueDb.Get(dialogueId);
        if (so == null) return;

        // 대화 종료 시 플레이어 잠금 해제 (중복 구독 방지)
        _dialoguePlayer.OnDialogueEnded -= OnDialogueEnded;
        _dialoguePlayer.OnDialogueEnded += OnDialogueEnded;

        _dialoguePlayer.StartDialogue(so);
        LockPlayer();
    }

    // ── 플레이어 입력 잠금/해제 ──────────────────────────────────────

    private void LockPlayer()
    {
        if (_playerFsm == null) return;
        _playerFsm.TransitionTo(_playerFsm.Cutscene); // Movement + WeaponCtrl 비활성
        _playerFsm.Dash.enabled = false;               // 대시 비활성
        if (InventoryToggle.Instance != null)
            InventoryToggle.Instance.enabled = false;  // 인벤토리 토글 비활성
    }

    private void OnDialogueEnded()
    {
        _dialoguePlayer.OnDialogueEnded -= OnDialogueEnded;
        UnlockPlayer();
    }

    private void UnlockPlayer()
    {
        if (_playerFsm != null)
        {
            _playerFsm.Dash.enabled = true;
            _playerFsm.TransitionTo(_playerFsm.Idle); // Exit() → Movement + WeaponCtrl 재활성
        }
        if (InventoryToggle.Instance != null)
            InventoryToggle.Instance.enabled = true;
    }

    private void PlayGreeting()
    {
        if (_hasGreeted && !string.IsNullOrEmpty(_revisitDialogueId))
        {
            PlayDialogue(_revisitDialogueId);
            return;
        }
        if (!string.IsNullOrEmpty(_greetingDialogueId))
        {
            PlayDialogue(_greetingDialogueId);
            _hasGreeted = true;
        }
    }
}

public enum NpcType
{
    Generic,
    Mage,       // 마법사
    Merchant,   // 상인
    Artisan,    // 장인
    Summoner    // 정령술사
}
