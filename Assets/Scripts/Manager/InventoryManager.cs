using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 획득한 아이템을 인벤토리 빈 슬롯에 동적으로 넣어주는 매니저.
/// InventoryGenerator가 생성해둔 슬롯 정보를 참조합니다.
/// SRP(Single Responsibility Principle) 준수.
/// </summary>
public sealed class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [SerializeField] private InventoryGenerator _inventoryGenerator;
    private QuickSlotManager _quickSlotManager;

    private void Awake()
    {
        if (Instance == null) 
        {
            Instance = this;
        }
        else 
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        Core.ItemEvents.OnItemCollected += HandleItemCollected;
    }

    private void OnDisable()
    {
        Core.ItemEvents.OnItemCollected -= HandleItemCollected;
    }

    private void HandleItemCollected(ItemData item, int count)
    {
        AddItem(item, count);
    }

    public void RegisterGenerator(InventoryGenerator generator)
    {
        _inventoryGenerator = generator;
        Debug.Log($"[InventoryManager] 진짜 UI의 InventoryGenerator({generator.name})가 인벤토리 매니저에 안전하게 등록되었습니다.");
    }

    private void EnsureGeneratorExists()
    {
        if (_inventoryGenerator == null || _inventoryGenerator.gameObject.scene.rootCount == 0) 
        {
            var generators = Resources.FindObjectsOfTypeAll<InventoryGenerator>();
            foreach (var gen in generators)
            {
                if (gen.gameObject.scene.rootCount > 0 && gen.GetComponentInParent<Canvas>() != null)
                {
                    _inventoryGenerator = gen;
                    break;
                }
            }
        }
    }

    private void EnsureQuickSlotManagerExists()
    {
        if (_quickSlotManager == null)
        {
            _quickSlotManager = FindObjectOfType<QuickSlotManager>();
        }
    }

    /// <summary>
    /// 메인 인벤토리와 퀵슬롯을 포함한 모든 슬롯을 순회할 수 있는 열거자를 반환합니다.
    /// </summary>
    private IEnumerable<InventorySlot> GetAllTrackedSlots()
    {
        EnsureGeneratorExists();
        if (_inventoryGenerator != null && _inventoryGenerator.AllSlots != null)
        {
            foreach (var slot in _inventoryGenerator.AllSlots)
            {
                yield return slot;
            }
        }

        EnsureQuickSlotManagerExists();
        if (_quickSlotManager != null && _quickSlotManager.quickSlots != null)
        {
            foreach (var slot in _quickSlotManager.quickSlots)
            {
                if (slot != null) yield return slot;
            }
        }
    }

    /// <summary>
    /// 아이템을 획득하여 슬롯에 추가.
    /// 퀵슬롯과 인벤토리를 모두 탐색하여 스태킹(중첩)을 우선 처리하고, 없으면 빈 슬롯에 넣습니다.
    /// </summary>
    public bool AddItem(ItemData item, int count = 1)
    {
        EnsureGeneratorExists();
        if (_inventoryGenerator == null)
        {
            Debug.LogWarning("[InventoryManager] 유효한 UI InventoryGenerator를 씬에서 찾을 수 없습니다.");
            return false;
        }

        // 인벤토리가 아직 생성되지 않았다면 강제로 생성 (UI 비활성화 상태에서 아이템 획득 시 대비)
        if (_inventoryGenerator.AllSlots == null || _inventoryGenerator.AllSlots.Count == 0)
        {
            _inventoryGenerator.GenerateEmptySlots();
        }

        InventorySlot firstEmptySlot = null;

        // 1. 이미 같은 아이템이 있는 슬롯 찾기 (스태킹 우선 처리)
        // 2. 빈 슬롯 찾기 (스태킹 불가 시 사용할 첫 빈 슬롯)
        foreach (var slot in GetAllTrackedSlots())
        {
            if (slot.currentData == item)
            {
                slot.RefreshSlot(item, slot.currentCount + count);
                Debug.Log($"[InventoryManager] 중첩: {item.Name} 아이템 획득! (현재 총 {slot.currentCount}개)");
                Core.ItemEvents.TriggerInventoryChanged();
                return true;
            }
            
            if (slot.currentData == null && firstEmptySlot == null)
            {
                // 주 인벤토리 슬롯이 우선적으로 비어있다면 저장, 퀵슬롯이 비어있다면 저장.
                firstEmptySlot = slot;
            }
        }

        // 같은 아이템을 못 찾았고, 빈 슬롯이 있으면 새로 할당
        if (firstEmptySlot != null)
        {
            firstEmptySlot.RefreshSlot(item, count);
            Debug.Log($"[InventoryManager] 신규: {item.Name} 아이템 획득! 빈 슬롯에 찰칵!");
            Core.ItemEvents.TriggerInventoryChanged();
            return true;
        }

        // 인벤토리 및 퀵슬롯 꽉 참
        Debug.LogWarning("[InventoryManager] 획득 불가: 인벤토리가 꽉 찼습니다!");
        return false;
    }

    /// <summary>
    /// 인벤토리(퀵슬롯 포함) 내 특정 아이템의 총 수량을 반환합니다.
    /// </summary>
    public int GetItemCount(ItemData itemData)
    {
        if (itemData == null) return 0;
        
        int totalCount = 0;
        foreach (var slot in GetAllTrackedSlots())
        {
            // ScriptableObject 참조 비교 우선, 실패 시 이름 비교 (복제본 방어)
            if (slot.currentData != null && 
               (slot.currentData == itemData || slot.currentData.Name == itemData.Name))
            {
                totalCount += slot.currentCount;
            }
        }
        return totalCount;
    }

    /// <summary>
    /// 인벤토리(퀵슬롯 포함)에서 특정 아이템을 지정된 수량만큼 차감합니다.
    /// </summary>
    public bool ConsumeItems(ItemData itemData, int count)
    {
        // 먼저 수량이 충분한지 한 번 더 검증
        if (GetItemCount(itemData) < count)
        {
            Debug.LogWarning($"[InventoryManager] 아이템({itemData.Name})의 수량이 부족하여 차감할 수 없습니다.");
            return false;
        }

        int remaining = count;
        foreach (var slot in GetAllTrackedSlots())
        {
            if (slot.currentData != null && 
               (slot.currentData == itemData || slot.currentData.Name == itemData.Name))
            {
                if (slot.currentCount >= remaining)
                {
                    int finalCount = slot.currentCount - remaining;
                    slot.RefreshSlot(finalCount == 0 ? null : slot.currentData, finalCount);
                    Core.ItemEvents.TriggerInventoryChanged();
                    return true;
                }
                else
                {
                    remaining -= slot.currentCount;
                    slot.RefreshSlot(null, 0);
                }
            }
        }
        
        Core.ItemEvents.TriggerInventoryChanged();
        return true;
    }
}
