using UnityEngine;

/// <summary>
/// 조준 사격(Aimed Shot) 스킬 데이터.
/// 활 착용 시 사용 가능하며, (3 / 공속) 초간 차징 후 관통 화살을 발사합니다.
/// 숫자키를 꾹 누르고 있으면 차징, 놓으면 발사됩니다.
/// </summary>
[CreateAssetMenu(fileName = "AimedShotSkill", menuName = "Data/Skills/AimedShot")]
public class AimedShotSkillData : SkillData
{
    [Header("Skill Constants")]
    [Tooltip("기본 차징 시간 (초). 실제 차징 시간 = BaseChargeTime / 공격속도")]
    public float BaseChargeTime = 3.0f;
    [Tooltip("최대 차징 시 데미지 계수 (공격력 × 이 값)")]
    public float DamageCoefficient = 2.5f;

    protected override void OnEnable()
    {
        base.OnEnable();
        RequiredWeapon = WeaponType.Bow;
        if (ManaCost == 0f) ManaCost = 40f;
        if (Cooldown == 0f) Cooldown = 8f;
    }

    public override void Execute(GameObject player)
    {
        var weaponController = player.GetComponent<PlayerWeaponController>();
        if (weaponController == null)
        {
            Debug.LogError("[AimedShotSkillData] PlayerWeaponController를 찾을 수 없습니다!");
            return;
        }

        var bow = weaponController.ActiveBehaviour as BowBehaviour;
        if (bow == null)
        {
            Debug.LogError("[AimedShotSkillData] 현재 활성 무기가 BowBehaviour가 아닙니다!");
            return;
        }

        // 공격 속도를 기반으로 차징 시간 계산
        var stats = player.GetComponent<StatSystem>();
        float attackSpeed = stats != null ? stats.TotalAttackSpeed : 1.0f;
        float finalChargeTime = BaseChargeTime / Mathf.Max(0.1f, attackSpeed);

        Debug.Log($"[AimedShotSkillData] 조준 사격 발동! (차징 시간: {finalChargeTime:F1}초, 최대 계수: {DamageCoefficient}배)");

        // BowBehaviour에서 조준 사격 차징 시작
        bow.StartAimedShot(finalChargeTime, DamageCoefficient);
    }
}
