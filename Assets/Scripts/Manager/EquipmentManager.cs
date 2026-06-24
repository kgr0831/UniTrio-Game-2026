using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 장비 장착/해제 시 StatSystem 보너스를 등록/해제하는 매니저.
/// SRP — 장비 보너스 관리만 담당, 슬롯 UI/데이터는 InventorySlot이 처리.
/// DIP — IBonusProvider 인터페이스에만 의존하여 구체 타입(GadgetData)과 느슨하게 결합.
/// </summary>
public sealed class EquipmentManager : MonoBehaviour
{
    public static EquipmentManager Instance { get; private set; }

    [Header("Player Reference")]
    [SerializeField] private StatSystem _stats;

    // 슬롯별 현재 장착된 보너스 추적 (Dictionary는 Start 이후 크기 고정, GC 없음)
    private readonly Dictionary<InventorySlot, IBonusProvider> _equippedBonuses 
        = new Dictionary<InventorySlot, IBonusProvider>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// 장비 장착 시 호출. StatSystem에 보너스를 등록합니다.
    /// </summary>
    public void OnEquip(InventorySlot slot, GadgetData gadget)
    {
        if (_stats == null || slot == null || gadget == null) return;

        // 기존에 같은 슬롯에 장비가 있었다면 먼저 해제
        OnUnequip(slot);

        _stats.RegisterBonus(gadget);
        _equippedBonuses[slot] = gadget;

        Debug.Log($"[EquipmentManager] {gadget.Name} 보너스 등록 (DEF +{gadget.DefBonus}, ATK +{gadget.AtkBonus})");
    }

    /// <summary>
    /// 장비 해제 시 호출. StatSystem에서 보너스를 제거합니다.
    /// </summary>
    public void OnUnequip(InventorySlot slot)
    {
        if (_stats == null || slot == null) return;

        if (_equippedBonuses.TryGetValue(slot, out IBonusProvider oldProvider))
        {
            _stats.UnregisterBonus(oldProvider);
            _equippedBonuses.Remove(slot);
            Debug.Log("[EquipmentManager] 장비 보너스 해제 완료");
        }
    }
}
