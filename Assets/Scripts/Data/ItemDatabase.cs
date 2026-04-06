using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 모든 아이템 데이터를 총괄하는 중앙 마스터 테이블 (DB 역할)
/// </summary>
[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Data/ItemDatabase")]
public class ItemDatabase : ScriptableObject
{
    [Header("모든 아이템 데이터 목록")]
    public List<ItemData> AllItems = new List<ItemData>();

    /// <summary>
    /// ID를 기준으로 아이템의 사양(Master Data)을 검색하여 반환합니다.
    /// </summary>
    /// <param name="id">찾으려는 아이템의 고유 ID</param>
    /// <returns>검색된 ItemData (없으면 null)</returns>
    public ItemData GetItemById(int id)
    {
        if (AllItems == null) return null;

        // 리스트에서 해당 ID를 가진 첫 번째 아이템을 찾음
        return AllItems.Find(item => item != null && item._Id == id);
    }

    /// <summary>
    /// 아이템 타입별로 필터링된 리스트를 반환합니다.
    /// </summary>
    public List<ItemData> GetItemsByType(ItemType type)
    {
        return AllItems.FindAll(item => item != null && item._Type == type);
    }

    /// <summary>
    /// (선택 사항) ID와 매칭되는 딕셔너리를 생성하여 조회 속도를 높일 수도 있습니다.
    /// </summary>
    private Dictionary<int, ItemData> _itemDict;
    public void Initialize()
    {
        _itemDict = new Dictionary<int, ItemData>();
        foreach (var item in AllItems)
        {
            if (item != null && !_itemDict.ContainsKey(item._Id))
                _itemDict.Add(item._Id, item);
        }
    }
}
