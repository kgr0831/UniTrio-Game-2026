using UnityEngine;

/// <summary>
/// 두 개의 무기 슬롯을 관리하고 숫자 키(1, 2)를 통해 스왑(단독 활성화/비활성화)하는 관리자 시스템입니다.
/// </summary>
public class PlayerWeaponManager : MonoBehaviour
{
    [Header("Equipped Weapons (1번 슬롯, 2번 슬롯)")]
    [Tooltip("크기를 2로 두고 처음 무기와 두번째 무기 프리팹을 인스펙터에 등록하세요.")]
    [SerializeField] private GameObject[] _weaponSlots = new GameObject[2];

    private int _currentSlotIndex = 0;

    private void Start()
    {
        // 시작 시 무조건 1번 무기(인덱스 0) 자동 장착
        EquipWeapon(0);
    }

    private void Update()
    {
        // 1번 슬롯 무기 장착 (키보드 상단 1)
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            EquipWeapon(0);
        }
        // 2번 슬롯 무기 장착 (키보드 상단 2)
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            EquipWeapon(1);
        }
    }

    /// <summary>
    /// 지정된 인덱스의 무기만 켜고 나머지는 모두 꺼버립니다.
    /// 꺼진 무기는 OnDisable 이벤트가 호출되며 스스로 스윙 애니메이션을 강제 취소합니다.
    /// </summary>
    private void EquipWeapon(int index)
    {
        if (index < 0 || index >= _weaponSlots.Length) return;
        if (_weaponSlots[index] == null) return;
        
        // 이미 해당 슬롯을 들고 있다면 무시
        if (index == _currentSlotIndex && _weaponSlots[index].activeSelf) return;

        // 다른 모든 무기 오브젝트 비활성화 (즉시 스윙 캔슬 용도)
        for (int i = 0; i < _weaponSlots.Length; i++)
        {
            if (_weaponSlots[i] != null)
            {
                // 인덱스가 일치하는 놈만 True 로 넘어가 활성화됩니다.
                _weaponSlots[i].SetActive(i == index);
            }
        }

        _currentSlotIndex = index;
    }
}
