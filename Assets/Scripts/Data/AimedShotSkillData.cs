using UnityEngine;

/// <summary>
/// 조준 사격(Aimed Shot) 스킬 데이터.
/// 활 착용 시 사용 가능하며, (3 / 공속) 초간 차징 후 강력한 화살을 발사합니다.
/// </summary>
[CreateAssetMenu(fileName = "AimedShotSkill", menuName = "Data/Skills/AimedShot")]
public class AimedShotSkillData : SkillData
{
    [Header("Skill Constants")]
    public float BaseChargeTime = 3.0f;
    public float DamageCoefficient = 2.5f;

    protected override void OnEnable()
    {
        base.OnEnable();
        RequiredWeapon = WeaponType.Bow;
    }

    public override void Execute(GameObject player)
    {
        var weaponController = player.GetComponent<PlayerWeaponController>();
        if (weaponController == null) return;

        var bow = weaponController.ActiveBehaviour as BowBehaviour;
        if (bow == null) return;

        // "3 / 공속" 차징 시간 계산
        // WeaponData에서 공속을 가져와야 함. 
        // 현재 WeaponBehaviourBase에는 WeaponData 참조가 없으므로 
        // 임시로 PlayerWeaponController의 현재 슬롯 정보를 활용하거나 
        // 기본값으로 처리합니다. (여기서는 1.0f로 가정하거나, BowBehaviour에 공속 필드가 있다면 사용)
        
        float attackSpeed = 1.0f; // 기본값
        // 만약 WeaponData에서 가져올 수 있다면:
        // attackSpeed = weaponController.CurrentWeaponData.AttackSpeed;
        
        float finalChargeTime = BaseChargeTime / attackSpeed;

        bow.StartSkillCharge(finalChargeTime, DamageCoefficient);
    }
}
