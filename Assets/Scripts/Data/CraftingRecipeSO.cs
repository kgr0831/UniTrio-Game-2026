using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 크래프팅(제작) 시스템에 사용되는 데이터 컨테이너입니다.
/// 전역 상태가 아닌 ScriptableObject를 통해 데이터를 관리하여 메모리 낭비를 줄입니다.
/// </summary>
[CreateAssetMenu(fileName = "NewCraftingRecipe", menuName = "Data/Crafting/Recipe")]
public class CraftingRecipeSO : ScriptableObject
{
    [Header("Result")]
    [Tooltip("제작 결과물 아이템")]
    public ItemData ResultItem;

    [Tooltip("제작 시 획득하는 결과물 수량")]
    public int ResultCount = 1;

    [Header("Ingredients")]
    [Tooltip("제작에 필요한 재료 리스트")]
    public List<IngredientInfo> RequiredIngredients;

    [Header("Settings")]
    [Tooltip("제작 소요 시간(초). 현재 즉시 제작일 경우 0")]
    public float CraftingTime = 0f;
}
