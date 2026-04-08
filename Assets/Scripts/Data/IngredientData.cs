using UnityEngine;

/// <summary>
/// 일반 재료 데이터 (스탯 없음)
/// </summary>
[CreateAssetMenu(fileName = "NewIngredientData", menuName = "Data/Items/Ingredient")]
public class IngredientData : ItemData
{
    public override string GetStatDescription() => "재료 아이템입니다.";
}
