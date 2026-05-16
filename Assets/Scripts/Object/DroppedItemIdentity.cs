using UnityEngine;

/// <summary>
/// 필드에 떨어진 아이템이 어떤 ItemData인지 기억하는 데이터 홀더 컴포넌트.
/// FloatingMagneticItem에서 플레이어 획득 시 이 데이터를 읽어 InventoryManager로 넘김.
/// </summary>
public sealed class DroppedItemIdentity : MonoBehaviour
{
    public ItemData ItemData { get; private set; }
    public int ItemCount { get; private set; }

    public void Setup(ItemData itemData, int count)
    {
        ItemData = itemData;
        ItemCount = count;
    }
}
