using UnityEngine;

/// <summary>
/// 프리팹 기반 강타(Bash) 스킬 로직.
/// 검 착용 시에만 사용 가능하며, 다음 3번의 기본 공격 데미지를 2배로 만듭니다.
/// </summary>
public class BashSkill : SkillBase
{
    [Header("Bash Effect Settings")]
    [Tooltip("강타 효과가 적용될 공격 횟수")]
    public int BashStackCount = 3;

    public override void Execute(GameObject player)
    {
        // 1. 플레이어 스탯 시스템에 스택 부여
        var stats = player.GetComponent<StatSystem>();
        if (stats != null)
        {
            stats.BashCount = BashStackCount;
            Debug.Log($"[BashSkill] 프리팹 로직 실행: 강타 스택 {BashStackCount}회 부여");
        }

        // 2. 검(Sword) 비주얼 효과 활성화
        var weaponCtrl = player.GetComponent<PlayerWeaponController>();
        if (weaponCtrl != null && weaponCtrl.ActiveBehaviour is SwordBehaviour sword)
        {
            sword.SetBashEffectActive(true);
        }

        // 3. 버프형 스킬이므로 로직 실행 후 프리팹 파괴
        // (VFX나 지속적인 사운드가 필요하다면 이 타이밍을 조절할 수 있습니다.)
        OnFinish();
    }
}
