using UnityEngine;

/// <summary>
/// 건축 아이템 데이터. 핫바 2~9번에 배치하고 키 입력으로 타일에 맞춰 설치합니다.
/// 미리보기·배치 시스템은 Milestone 7에서 구현 예정.
/// </summary>
[CreateAssetMenu(fileName = "NewBuildingData", menuName = "Data/Items/Building")]
public class BuildingData : ItemData
{
    [Header("Building Settings")]
    [Tooltip("최대 내구도 (체력)")]
    public int MaxHealth = 100;
    [Tooltip("설치할 건물/오브젝트 프리팹")]
    public GameObject BuildingPrefab;
    [Tooltip("차지하는 타일 크기 (가로 x 세로)")]
    public Vector2Int TileSize = Vector2Int.one;

    protected override void OnEnable()
    {
        Type = ItemType.Building;
    }

    public override string GetStatDescription() =>
        $"타일 크기: {TileSize.x}×{TileSize.y}";
}
