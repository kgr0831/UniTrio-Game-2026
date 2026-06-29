using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 대사 ID → DialogueSO 조회 레지스트리.
/// 인스펙터 SO 항목 + 런타임 CSV 등록을 모두 지원합니다.
/// </summary>
[CreateAssetMenu(fileName = "DialogueDatabase", menuName = "Data/Dialogue/Database")]
public class DialogueDatabase : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        [Tooltip("대사 식별자 (예: TUT_006)")]
        public string Id;
        [Tooltip("해당 ID로 재생할 대화 데이터")]
        public DialogueSO Dialogue;
    }

    [Tooltip("인스펙터에서 직접 등록한 SO 목록")]
    [SerializeField] private List<Entry> _entries = new List<Entry>();

    private Dictionary<string, DialogueSO> _lookup;

    private void BuildLookup()
    {
        _lookup = new Dictionary<string, DialogueSO>(_entries.Count);
        foreach (var e in _entries)
        {
            if (string.IsNullOrEmpty(e.Id) || e.Dialogue == null) continue;
            _lookup[e.Id] = e.Dialogue;
        }
    }

    /// <summary>ID로 대화 데이터를 조회합니다. 없으면 null.</summary>
    public DialogueSO Get(string id)
    {
        if (_lookup == null) BuildLookup();
        return _lookup.TryGetValue(id, out var so) ? so : null;
    }

    /// <summary>
    /// 런타임에 DialogueSO를 등록합니다 (CSV 로더 등에서 호출).
    /// 이미 같은 ID가 있으면 덮어씁니다.
    /// </summary>
    public void Register(string id, DialogueSO dialogue)
    {
        if (_lookup == null) BuildLookup();
        if (string.IsNullOrEmpty(id) || dialogue == null) return;
        _lookup[id] = dialogue;
    }

    /// <summary>여러 항목을 한 번에 등록합니다.</summary>
    public void RegisterAll(Dictionary<string, DialogueSO> entries)
    {
        if (_lookup == null) BuildLookup();
        foreach (var kv in entries)
            Register(kv.Key, kv.Value);
    }
}
