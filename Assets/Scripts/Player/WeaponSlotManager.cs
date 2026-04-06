using System;
using UnityEngine;

/// <summary>
/// 무기 슬롯 A / B를 관리하고 E키로 활성 슬롯을 스왑하는 컴포넌트.
/// - 슬롯 0 ↔ 슬롯 1 토글
/// - 스왑 시 PlayerWeaponController.EquipWeapon() 호출
/// - OnSlotChanged 이벤트로 UI(핫바 1번) 갱신 알림
/// </summary>
[RequireComponent(typeof(PlayerWeaponController))]
public class WeaponSlotManager : MonoBehaviour
{
    [Header("Weapon Slot Data")]
    [Tooltip("슬롯 0번(기본 장착) WeaponData")]
    [SerializeField] private WeaponData _slotA;
    [Tooltip("슬롯 1번 WeaponData")]
    [SerializeField] private WeaponData _slotB;

    [Header("Settings")]
    [SerializeField] private KeyCode _swapKey = KeyCode.E;

    /// <summary>현재 활성 슬롯 인덱스 (0 또는 1).</summary>
    public int ActiveSlotIndex { get; private set; } = 0;

    /// <summary>현재 활성 무기 데이터.</summary>
    public WeaponData ActiveWeapon => ActiveSlotIndex == 0 ? _slotA : _slotB;

    /// <summary>슬롯이 변경될 때 발생. (activeIndex, newWeaponData) 전달.</summary>
    public event Action<int, WeaponData> OnSlotChanged;

    private PlayerWeaponController _weaponCtrl;

    private void Awake()
    {
        _weaponCtrl = GetComponent<PlayerWeaponController>();
    }

    private void Start()
    {
        // 시작 시 슬롯 0 장착
        _weaponCtrl.EquipWeapon(0);
        OnSlotChanged?.Invoke(0, _slotA);
    }

    private void Update()
    {
        if (Input.GetKeyDown(_swapKey))
            SwapWeapon();
    }

    // ── Public API ──────────────────────────────────────────────────

    /// <summary>활성 슬롯을 0 ↔ 1 토글합니다. 공격 중이면 큐에 저장해 공격 종료 후 실행됩니다.</summary>
    public void SwapWeapon()
    {
        ActiveSlotIndex = 1 - ActiveSlotIndex;
        _weaponCtrl.TryEquipWeapon(ActiveSlotIndex);
        OnSlotChanged?.Invoke(ActiveSlotIndex, ActiveWeapon);
    }

    /// <summary>
    /// 인벤토리 드래그 앤 드롭으로 특정 슬롯에 무기 데이터를 배치합니다.
    /// </summary>
    public void SetSlot(int slotIndex, WeaponData data)
    {
        if (slotIndex == 0) _slotA = data;
        else if (slotIndex == 1) _slotB = data;

        // 현재 활성 슬롯이 교체되면 즉시 UI 갱신
        if (slotIndex == ActiveSlotIndex)
            OnSlotChanged?.Invoke(ActiveSlotIndex, ActiveWeapon);
    }

    /// <summary>슬롯 인덱스의 WeaponData를 반환합니다.</summary>
    public WeaponData GetSlot(int slotIndex) =>
        slotIndex == 0 ? _slotA : _slotB;
}
