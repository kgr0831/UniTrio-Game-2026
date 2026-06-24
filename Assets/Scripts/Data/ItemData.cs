using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아이템 제작 시 필요한 재료 정보 (Tuple 대체)
/// </summary>
[System.Serializable]
public struct IngredientInfo
{
    public ItemData Item;
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

    [Tooltip("월드에 드롭될 때 생성되는 모델 프리팹")]
    public GameObject DropPrefab;

    // 기존에 있던 IngredientList 필드는 CraftingRecipeSO로 분리되어 삭제되었습니다.

    /// <summary>파생 클래스에서 override하여 _Type을 자동 설정합니다.</summary>
    protected virtual void OnEnable() { }

    /// <summary>파생 클래스에서 스탯 설명 문자열을 반환합니다.</summary>
    public virtual void Use()
    {
        Debug.Log(Id + "아이템 사용");
    }
    public abstract string GetStatDescription();
}
