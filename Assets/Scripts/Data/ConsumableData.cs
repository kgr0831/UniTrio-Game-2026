using UnityEngine;

/// <summary>
/// 소모품(음식 등) 데이터
/// </summary>
[CreateAssetMenu(fileName = "NewConsumableData", menuName = "Data/Items/Consumable")]
public class ConsumableData : ItemData
{
    public float HealAmount;

    public override string GetStatDescription() => $"회복량: {HealAmount}";
}
