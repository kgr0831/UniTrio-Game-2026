using UnityEngine;

/// <summary>
/// 무기 슬롯(핫바 1번)과 보조 슬롯(SubWeaponSlot)을 관리하고 E키로 스왑하는 컴포넌트.
/// - 핫바 1번 슬롯의 무기가 현재 장착된 무기로 간주됩니다.
/// - E키 입력 시 핫바 1번 ↔ 보조 무기 슬롯 아이템 스왑.
/// - 상시 감시를 통해 핫바 1번의 아이템이 바뀌면 PlayerWeaponController에 장착 요청.
/// </summary>
[RequireComponent(typeof(PlayerWeaponController))]
public class WeaponSlotManager : MonoBehaviour
{
    [Header("Slot References")]
    [Tooltip("핫바 1번 슬롯 (Alpha 1에 대응하는 슬롯)")]
    [SerializeField] private InventorySlot _activeSlot;
    
    [Tooltip("보조 무기 슬롯 (E키로 스왑할 대상)")]
    [SerializeField] private InventorySlot _subWeaponSlot;

    [Header("Settings")]
    [SerializeField] private KeyCode _swapKey = KeyCode.E;

    private PlayerWeaponController _weaponCtrl;
    private ItemData               _lastEquippedData;

    private void Awake()
    {
        _weaponCtrl = GetComponent<PlayerWeaponController>();
    }

    private void Start()
    {
        // 시작 시 현재 슬롯 상태 확인
        SyncWeaponWithActiveSlot();
    }

    private void Update()
    {
        if (Input.GetKeyDown(_swapKey))
            SwapWeapon();
    }

    private void LateUpdate()
    {
        // 핫바 1번 슬롯의 데이터 변화 감지 (드래그앤드롭 등으로 바뀌었을 때)
        if (_activeSlot != null && _activeSlot.currentData != _lastEquippedData)
        {
            SyncWeaponWithActiveSlot();
        }
    }

    // ── Public API ──────────────────────────────────────────────────

    /// <summary>핫바 1번 슬롯과 보조 슬롯의 아이템을 서로 교체합니다.</summary>
    public void SwapWeapon()
    {
        if (_activeSlot == null || _subWeaponSlot == null) return;

        ItemData oldActiveData  = _activeSlot.currentData;
        int      oldActiveCount = _activeSlot.currentCount;

        // 보조 슬롯 데이터 -> 활성 슬롯
        _activeSlot.RefreshSlot(_subWeaponSlot.currentData, _subWeaponSlot.currentCount);
        
        // 이전 활성 데이터 -> 보조 슬롯
        _subWeaponSlot.RefreshSlot(oldActiveData, oldActiveCount);

        Debug.Log("[WeaponSlot] 무기 스왑 완료");
        
        // 데이터가 바뀌었으므로 즉시 동기화
        SyncWeaponWithActiveSlot();
    }

    /// <summary>현재 활성 슬롯의 데이터를 기반으로 실제 무기 모델을 장착/해제합니다.</summary>
    public void SyncWeaponWithActiveSlot()
    {
        if (_activeSlot == null || _weaponCtrl == null) return;

        ItemData current = _activeSlot.currentData;
        _lastEquippedData = current;

        if (current is WeaponData weapon)
        {
            _weaponCtrl.EquipWeaponByData(weapon);
            Debug.Log($"[WeaponSlot] '{weapon.Name}' 장착됨");
        }
        else
        {
            // 무기가 아니거나 빈 칸이면 해제
            _weaponCtrl.UnequipAll();
        }
    }
}

