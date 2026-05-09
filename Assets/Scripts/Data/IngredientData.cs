using UnityEngine;

/// <summary>
/// 연료 타입을 정의하는 enum.
/// 새로운 조리 시설이 추가되면 여기에 항목을 추가합니다. (OCP)
/// </summary>
public enum FuelType
{
    None = 0,      // 연료가 아님
    Bonfire = 1    // 모닥불 연료
}

/// <summary>
/// 일반 재료 데이터 (스탯 없음).
/// FuelCategory가 None이 아니면 해당 시설의 연료로 사용 가능합니다.
/// </summary>
[CreateAssetMenu(fileName = "NewIngredientData", menuName = "Data/Items/Ingredient")]
public class IngredientData : ItemData
{
    [Header("Fuel Settings (연료)")]
    [Tooltip("연료 타입 — None이면 연료가 아닌 일반 재료")]
    public FuelType FuelCategory = FuelType.None;

    public override string GetStatDescription()
    {
        if (FuelCategory != FuelType.None)
        {
            return $"재료 아이템입니다.\n연료 타입: {FuelCategory}";
        }
        return "재료 아이템입니다.";
    }
}
