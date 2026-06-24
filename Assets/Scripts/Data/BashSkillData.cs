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
    [Tooltip("강타 발동 시 머리 위에 띄울 버프 표시 프리팹 (1초 후 자동 삭제)")]
    [SerializeField] private GameObject _bashBuffPrefab;

    [Tooltip("강타 효과가 적용될 기본 공격 횟수")]
    public int BashStackCount = 3;

    protected override void OnEnable()
    {
        base.OnEnable();
        // 기본값 설정 (검과 창 공용, 또는 None으로 두어 스킬매니저가 별도로 통제할 수도 있음)
        RequiredWeapon = WeaponType.None; 
        if (ManaCost == 0f) ManaCost = 20f;
        if (Cooldown == 0f) Cooldown = 10f;
    }

    public override void Execute(GameObject player)
    {
        Debug.Log($"[BashSkillData] Execute 호출됨! 전달받은 Player: {(player != null ? player.name : "NULL")}");
        if (player == null) return;

        // 1. 플레이어 스탯 시스템에 강타 스택 부여
        var stats = player.GetComponent<StatSystem>();
        if (stats != null)
        {
            stats.BashCount = BashStackCount;
            Debug.Log($"[BashSkillData] 스탯 부여 완료: 강타 스택 {BashStackCount}회");
        }
        else
        {
            Debug.LogError("[BashSkillData] 대상에게 StatSystem이 없습니다!");
        }

        // 2. 머리 위 버프 이펙트 생성 (1초 후 자동 파괴)
        if (_bashBuffPrefab != null)
        {
            GameObject buff = Instantiate(_bashBuffPrefab, player.transform);
            buff.transform.localPosition = new Vector3(0, 2f, 0);
            Destroy(buff, 1f);
            Debug.Log("[BashSkillData] 버프 프리팹 생성 성공!");
        }
        else
        {
            Debug.LogError("[BashSkillData] 인스펙터에 BashBuffPrefab이 할당되지 않았습니다!");
        }

        // 3. 무기 범용 비주얼 효과 활성화
        var weaponCtrl = player.GetComponent<PlayerWeaponController>();
        if (weaponCtrl != null)
        {
            var weapon = weaponCtrl.ActiveBehaviour;
            if (weapon != null)
            {
                weapon.SetBashEffectActive(true);
                Debug.Log($"[BashSkillData] {weapon.WeaponType} 무기의 Bash 효과 활성화 완료!");
            }
            else
            {
                Debug.LogWarning("[BashSkillData] 현재 활성화된 무기가 없습니다.");
            }
        }
        else
        {
            Debug.LogError("[BashSkillData] 대상에게 PlayerWeaponController가 없습니다!");
        }
    }
}

