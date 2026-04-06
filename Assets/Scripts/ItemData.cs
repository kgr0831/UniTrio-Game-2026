using UnityEngine;

[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
public class ItemData : ScriptableObject, IUseable {
    public int id;
    public string itemName;
    public Sprite icon;  // 에디터에서 직접 할당 (서버에는 id만 저장됨)
    public int count;

    public int ID => id;
    public string Name => itemName;
    public Sprite Icon => icon;

    public void Use() {
        Debug.Log($"{itemName}을 사용했습니다.");
        count--;
    }
}