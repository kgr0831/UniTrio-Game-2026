using UnityEngine;

/// <summary>
/// 조리 방식을 정의하는 enum.
/// 새로운 조리 시설이 추가되면 여기에 항목을 추가하면 됩니다. (OCP)
/// </summary>
public enum CookingMethod
{
    None = 0,      // 조리 불필요 (완성품 또는 포션)
    Bonfire = 1    // 모닥불 조리
}

/// <summary>
/// 소모품(음식, 포션 등) 데이터.
/// CookMethod가 None이 아닌 경우, 해당 조리 시설에서 CookedResult로 변환 가능합니다.
/// </summary>
[CreateAssetMenu(fileName = "NewConsumableData", menuName = "Data/Items/Consumable")]
public class ConsumableData : ItemData
{
    [Header("Consumable Settings")]
    public float HealAmount;

    [Header("Cooking Recipe (조리법)")]
    [Tooltip("조리 방식 — None이면 조리가 필요 없는 완성품/포션")]
    public CookingMethod CookMethod = CookingMethod.None;

    [Tooltip("조리에 걸리는 시간(초)")]
    public float CookingTime = 2f;

    [Tooltip("조리 완료 후 생성되는 결과 아이템 (ConsumableData)")]
    public ConsumableData CookedResult;

    protected override void OnEnable()
    {
        Type = ItemType.Food;
    }

    public override string GetStatDescription()
    {
        string desc = $"회복량: {HealAmount}";

        if (CookMethod != CookingMethod.None && CookedResult != null)
        {
            desc += $"\n조리법: {CookMethod} ({CookingTime}초)";
            desc += $"\n결과: {CookedResult.Name}";
        }

        return desc;
    }
}
