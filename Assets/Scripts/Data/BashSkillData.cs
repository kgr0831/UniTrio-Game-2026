using UnityEngine;

/// <summary>
/// 강타(Bash) 스킬 데이터.
/// 검 착용 시에만 사용 가능하며, 다음 3번의 기본 공격이 2배(계수 2.0)의 피해를 입힙니다.
/// 기본값: 쿨다운 10초, 마나 소모 20
/// </summary>
[CreateAssetMenu(fileName = "BashSkill", menuName = "Data/Skills/Bash")]
public class BashSkillData : SkillData
{
    [Header("Bash Settings")]
    [Tooltip("강타 효과가 적용될 기본 공격 횟수")]
    public int BashStackCount = 3;

    protected override void OnEnable()
    {
        base.OnEnable();
        // 기본값 설정 (에셋 생성 시 초기값)
        RequiredWeapon = WeaponType.Sword;
        if (ManaCost == 0f) ManaCost = 20f;
        if (Cooldown == 0f) Cooldown = 10f;
    }

    // Execute는 부모(SkillData)에서 SkillPrefab을 인스턴스화하여 처리하므로 
    // 특수한 로직이 없다면 여기서 override할 필요가 없습니다.
}

