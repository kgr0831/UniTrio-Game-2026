using UnityEngine;

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
