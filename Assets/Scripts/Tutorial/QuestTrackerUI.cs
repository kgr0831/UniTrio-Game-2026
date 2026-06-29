using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// 화면에 활성 퀘스트 목록을 표시하는 트래커 UI.
/// QuestManager의 이벤트를 구독하여 자동으로 갱신됩니다.
///
/// 기존 TutorialManager에서 Show/Hide로 직접 제어하던 방식도 그대로 지원합니다.
/// </summary>
public class QuestTrackerUI : MonoBehaviour
{
    [Header("Root")]
    [Tooltip("표시/숨김 대상 루트")]
    [SerializeField] private GameObject _root;

    [Header("Manual Mode (기존 호환)")]
    [Tooltip("수동 모드용 제목 텍스트")]
    [SerializeField] private TextMeshProUGUI _titleText;
    [Tooltip("수동 모드용 목표 텍스트")]
    [SerializeField] private TextMeshProUGUI _objectiveText;

    [Header("Quest List Mode")]
    [Tooltip("퀘스트 항목들을 배치할 부모 Transform (Vertical Layout 등)")]
    [SerializeField] private Transform _questListParent;
    [Tooltip("퀘스트 항목 프리팹 (없으면 수동 모드만 사용)")]
    [SerializeField] private GameObject _questEntryPrefab;

    [Header("카테고리 색상")]
    [SerializeField] private Color _tutorialColor = new Color(1f, 0.85f, 0.3f);
    [SerializeField] private Color _mainColor = new Color(0.4f, 0.85f, 1f);
    [SerializeField] private Color _npcColor = new Color(0.6f, 1f, 0.6f);

    private bool _manualMode;
    private readonly List<GameObject> _entries = new List<GameObject>();

    private void OnEnable()
    {
        if (QuestManager.Instance != null)
            QuestManager.Instance.OnQuestStateChanged += RefreshList;
    }

    private void OnDisable()
    {
        if (QuestManager.Instance != null)
            QuestManager.Instance.OnQuestStateChanged -= RefreshList;
    }

    private void Start()
    {
        // QuestManager가 있으면 자동 모드로 초기화
        if (QuestManager.Instance != null && _questListParent != null)
        {
            RefreshList();
        }
    }

    // ── 수동 모드 (기존 TutorialManager 호환) ─────────────

    /// <summary>단일 퀘스트를 수동으로 표시합니다 (기존 방식).</summary>
    public void Show(string title, string objective)
    {
        _manualMode = true;
        if (_root != null) _root.SetActive(true);
        if (_titleText != null) _titleText.text = title;
        if (_objectiveText != null) _objectiveText.text = objective;

        // 수동 모드 시 리스트 항목 숨김
        ClearEntries();
    }

    /// <summary>트래커를 숨깁니다.</summary>
    public void Hide()
    {
        _manualMode = false;
        if (_root != null) _root.SetActive(false);
    }

    // ── 자동 모드 (QuestManager 연동) ─────────────────────

    /// <summary>QuestManager의 활성 퀘스트 목록을 반영하여 UI를 갱신합니다.</summary>
    public void RefreshList()
    {
        if (_manualMode) return; // 수동 모드 중에는 자동 갱신 안 함
        if (QuestManager.Instance == null) return;

        var activeQuests = QuestManager.Instance.GetActiveQuests();

        if (activeQuests.Count == 0)
        {
            if (_root != null) _root.SetActive(false);
            ClearEntries();
            return;
        }

        if (_root != null) _root.SetActive(true);

        // 리스트 모드 사용 가능 여부 확인
        if (_questListParent != null && _questEntryPrefab != null)
        {
            RefreshListMode(activeQuests);
        }
        else
        {
            // 프리팹 없으면 첫 번째 퀘스트만 기존 방식으로 표시
            RefreshSimpleMode(activeQuests);
        }
    }

    private void RefreshSimpleMode(List<QuestSO> quests)
    {
        if (quests.Count == 0) return;

        var q = quests[0];
        if (_titleText != null)
        {
            _titleText.text = q.QuestName;
            _titleText.color = GetCategoryColor(q.Category);
        }

        if (_objectiveText != null && q.Objectives.Count > 0)
        {
            var obj = q.Objectives[0];
            int prog = QuestManager.Instance.GetProgress(q.QuestId, 0);
            _objectiveText.text = obj.RequiredAmount > 1
                ? $"{obj.Description} ({prog}/{obj.RequiredAmount})"
                : obj.Description;
        }
    }

    private void RefreshListMode(List<QuestSO> quests)
    {
        // 재사용 가능하면 재사용, 아니면 생성/삭제
        while (_entries.Count > quests.Count)
        {
            var last = _entries[_entries.Count - 1];
            _entries.RemoveAt(_entries.Count - 1);
            Destroy(last);
        }

        while (_entries.Count < quests.Count)
        {
            var go = Instantiate(_questEntryPrefab, _questListParent);
            _entries.Add(go);
        }

        for (int i = 0; i < quests.Count; i++)
        {
            var q = quests[i];
            var entry = _entries[i];

            // 엔트리 내부 TMP 업데이트
            var texts = entry.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length >= 2)
            {
                texts[0].text = q.QuestName;
                texts[0].color = GetCategoryColor(q.Category);

                var obj = q.Objectives.Count > 0 ? q.Objectives[0] : null;
                if (obj != null)
                {
                    int prog = QuestManager.Instance.GetProgress(q.QuestId, 0);
                    texts[1].text = obj.RequiredAmount > 1
                        ? $"{obj.Description} ({prog}/{obj.RequiredAmount})"
                        : obj.Description;
                }
                else
                {
                    texts[1].text = "";
                }
            }
            else if (texts.Length == 1)
            {
                texts[0].text = q.QuestName;
                texts[0].color = GetCategoryColor(q.Category);
            }
        }
    }

    private void ClearEntries()
    {
        foreach (var e in _entries)
        {
            if (e != null) Destroy(e);
        }
        _entries.Clear();
    }

    private Color GetCategoryColor(QuestCategory cat)
    {
        return cat switch
        {
            QuestCategory.Tutorial => _tutorialColor,
            QuestCategory.Main => _mainColor,
            QuestCategory.Npc => _npcColor,
            _ => Color.white
        };
    }
}
