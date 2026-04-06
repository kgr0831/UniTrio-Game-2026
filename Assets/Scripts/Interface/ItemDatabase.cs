using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "ItemDB", menuName = "Inventory/ItemDB")]
public class ItemDatabase : ScriptableObject {
    public List<ItemData> itemDatas;

    public ItemData GetItem(int id) {
        return itemDatas.Find(x => x.id == id);
    }
}
