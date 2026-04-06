using UnityEngine;
using System.Collections.Generic;

public class DataManager : MonoBehaviour {
    public static DataManager Instance;

    [Header("Resources")]
    public List<ItemData> itemDatabase;  // 인스펙터에서 등록
    public List<SkillData> skillDatabase;

    // 실시간 조회를 위한 딕셔너리 (메모리 상의 인덱스)
    private Dictionary<int, ItemData> itemCache = new Dictionary<int, ItemData>();
    private Dictionary<int, SkillData> skillCache = new Dictionary<int, SkillData>();

    void Awake() {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        GenerateCache();
    }

    private void GenerateCache() {
        foreach (var item in itemDatabase) {
            // 신규 ItemData API: id → _Id
            if (item != null && !itemCache.ContainsKey(item._Id)) itemCache.Add(item._Id, item);
        }
        foreach (var skill in skillDatabase) {
            if (skill != null && !skillCache.ContainsKey(skill.id)) skillCache.Add(skill.id, skill);
        }
    }

    /// <summary>ID로 ItemData를 조회합니다.</summary>
    public ItemData GetItem(int id) {
        return itemCache.TryGetValue(id, out var item) ? item : null;
    }

    /// <summary>스킬 전용 IUseable 조회. ItemData는 IUseable을 구현하지 않으므로 Item은 GetItem()을 사용하세요.</summary>
    public IUseable GetUseable(string type, int id) {
        if (type == "Skill") {
            return skillCache.TryGetValue(id, out var skill) ? skill : null;
        }
        return null;
    }
}