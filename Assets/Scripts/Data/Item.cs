using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 인벤토리나 필드에 존재하는 실제 아이템 객체 (Instance)
/// </summary>
[System.Serializable]
public class Item 
{
    // 정적 데이터 참조 (기존의 모든 사양 데이터는 여기서 가져옴)
    public ItemData Data;

    // 동적 데이터 (아이템마다 달라질 수 있는 상태)
    public int Tier;         // 아이템 티어
    public int Level;        // 아이템 레벨
    public int CurrentCount; // 현재 수량 (재료 아이템 등)

    // 편리한 접근을 위한 프로퍼티
    public int Id => Data != null ? Data.Id : -1;
    public string Name => Data != null ? Data.Name : "Unassigned Item";
    public ItemType Type => Data != null ? Data.Type : ItemType.Ingredient;

    // 무기 정보 조회용 프로퍼티 (캐스팅 편의성)
    public WeaponData WeaponInfo => Data as WeaponData;
}

[System.Serializable]
public enum ItemType
{
    Ingredient  = 0, // 일반적인 재료
    Weapon      = 1, // 무기
    Tool        = 2, // 도구
    Food        = 3, // 음식
    SpecialIng  = 4, // 특수 재료
    Building    = 5, // 건축
    Accessories = 6, // 장신구
    Armor       = 7, // 방어구
    Skill       = 8  // 스킬 (ItemData 계층으로 편입)
}
