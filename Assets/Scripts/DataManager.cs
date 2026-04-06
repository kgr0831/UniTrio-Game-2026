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
            if (!itemCache.ContainsKey(item.id)) itemCache.Add(item.id, item);
        }
        foreach (var skill in skillDatabase) {
            if (!skillCache.ContainsKey(skill.id)) skillCache.Add(skill.id, skill);
        }
    }

    // 서버 데이터를 기반으로 리소스를 찾아주는 핵심 함수
    public IUseable GetUseable(string type, int id) {
        if (type == "Item") {
            return itemCache.TryGetValue(id, out var item) ? item : null;
        } else if (type == "Skill") {
            return skillCache.TryGetValue(id, out var skill) ? skill : null;
        }
        return null;
    }
}