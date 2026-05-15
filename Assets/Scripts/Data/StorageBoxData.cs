using UnityEngine;

/// <summary>
/// 보관 상자 건축 아이템 데이터.
/// BuildingData를 상속하므로 기존 건축 시스템(BuildingPlacementController)과 100% 호환됩니다.
/// SlotCount 필드로 상자마다 보관 용량을 다르게 설정할 수 있습니다.
/// </summary>
[CreateAssetMenu(fileName = "NewStorageBoxData", menuName = "Data/Items/StorageBox")]
public class StorageBoxData : BuildingData
{
    [Header("Storage Settings")]
    [Tooltip("이 상자가 보유하는 보관 슬롯 수")]
    public int SlotCount = 20;

    protected override void OnEnable()
    {
        // 건축 아이템 타입 자동 설정 (BuildingData와 동일)
        Type = ItemType.Building;
    }

    public override string GetStatDescription() =>
        $"보관 슬롯: {SlotCount}칸\n타일 크기: {TileSize.x}×{TileSize.y}";
}
