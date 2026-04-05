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
/// 모든 아이템 데이터의 베이스 (ScriptableObject)
/// </summary>
public abstract class ItemData : ScriptableObject
{
    public int _Id;               // 고유 인덱스
    public string _Name;         // 이름
    [TextArea]
    public string _Description;  // 설명
    public ItemType _Type;       // 아이템 타입
    public Sprite _Icon;         // 아이콘

    public List<IngredientInfo> _IngredientList; // 제작 재료 리스트

    // 자식 클래스에서 구현할 공통 인터페이스
    public abstract string GetStatDescription();
}

/// <summary>
/// 무기 전용 데이터
/// </summary>
[CreateAssetMenu(fileName = "NewWeaponData", menuName = "Data/Items/Weapon")]
public class WeaponData : ItemData
{
    public float _Damage;
    public float _AttackSpeed;

    public override string GetStatDescription() => $"데미지: {_Damage} | 공격속도: {_AttackSpeed}";
}

/// <summary>
/// 소모품(음식 등) 데이터
/// </summary>
[CreateAssetMenu(fileName = "NewConsumableData", menuName = "Data/Items/Consumable")]
public class ConsumableData : ItemData
{
    public float _HealAmount;

    public override string GetStatDescription() => $"회복량: {_HealAmount}";
}

/// <summary>
/// 일반 재료 데이터 (스탯 없음)
/// </summary>
[CreateAssetMenu(fileName = "NewIngredientData", menuName = "Data/Items/Ingredient")]
public class IngredientData : ItemData
{
    public override string GetStatDescription() => "재료 아이템입니다.";
}
