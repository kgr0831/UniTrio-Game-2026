using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아이템 제작 시 필요한 재료 정보 (Tuple 대체)
/// </summary>
[System.Serializable]
public struct IngredientInfo
{
    public int ItemId;
    public int Count;
}

/// <summary>
/// 모든 아이템 데이터의 베이스 (ScriptableObject).
/// 파생 클래스는 OnEnable()을 override하여 _Type을 자동 설정합니다.
/// </summary>
public abstract class ItemData : ScriptableObject
{
    public int Id;               // 고유 인덱스
    public string Name;          // 이름
    [TextArea]
    public string Description;   // 설명
    public ItemType Type;        // 아이템 타입 (파생 클래스 OnEnable에서 자동 설정)
    public Sprite Icon;          // 아이콘

    public List<IngredientInfo> IngredientList; // 제작 재료 리스트

    /// <summary>파생 클래스에서 override하여 _Type을 자동 설정합니다.</summary>
    protected virtual void OnEnable() { }

    /// <summary>파생 클래스에서 스탯 설명 문자열을 반환합니다.</summary>
    public abstract string GetStatDescription();
}

/// <summary>
/// 무기 전용 데이터
/// </summary>
[CreateAssetMenu(fileName = "NewWeaponData", menuName = "Data/Items/Weapon")]
public class WeaponData : ItemData
{
    public float Damage;
    public float AttackSpeed;

    public override string GetStatDescription() => $"데미지: {Damage} | 공격속도: {AttackSpeed}";
}

/// <summary>
/// 소모품(음식 등) 데이터
/// </summary>
[CreateAssetMenu(fileName = "NewConsumableData", menuName = "Data/Items/Consumable")]
public class ConsumableData : ItemData
{
    public float HealAmount;

    public override string GetStatDescription() => $"회복량: {HealAmount}";
}

/// <summary>
/// 일반 재료 데이터 (스탯 없음)
/// </summary>
[CreateAssetMenu(fileName = "NewIngredientData", menuName = "Data/Items/Ingredient")]
public class IngredientData : ItemData
{
    public override string GetStatDescription() => "재료 아이템입니다.";
}
