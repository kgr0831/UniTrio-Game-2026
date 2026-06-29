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
    private List<InventoryGenerator> _inventoryGenerators = new List<InventoryGenerator>();
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
        if (!_inventoryGenerators.Contains(generator))
        {
            _inventoryGenerators.Add(generator);
            Debug.Log($"[InventoryManager] InventoryGenerator({generator.name})가 새로 등록되었습니다. 총 등록 개수: {_inventoryGenerators.Count}");
        }

        if (generator.isMainInventory)
        {
            _inventoryGenerator = generator;
            Debug.Log($"[InventoryManager] 메인 인벤토리 확정 등록: {generator.name}");
        }
    }

    /// <summary>
    /// 외부/서브 인벤토리가 켜질 때 메인 인벤토리의 실시간 데이터를 요청하여 강제 1:1 동기화시킵니다.
    /// </summary>
    public void RequestSyncFromMain(InventoryGenerator targetGen)
    {
        EnsureGeneratorExists();
        if (_inventoryGenerator == null || _inventoryGenerator.AllSlots == null || targetGen == null || targetGen.AllSlots == null)
        {
            Debug.LogWarning("[InventoryManager] 메인 인벤토리 또는 동기화 요청 대상 인벤토리가 준비되지 않아 무시합니다.");
            return;
        }

        int count = Mathf.Min(_inventoryGenerator.AllSlots.Count, targetGen.AllSlots.Count);
        for (int i = 0; i < count; i++)
        {
            var mainSlot = _inventoryGenerator.AllSlots[i];
            targetGen.AllSlots[i].RefreshSlotWithoutSync(mainSlot.currentData, mainSlot.currentCount);
        }
        Debug.Log($"[InventoryManager] 서브 인벤토리 UI({targetGen.name})가 주 인벤토리의 {count}개 슬롯과 완벽히 동기화되었습니다.");
    }

    private void SyncAllToNewGenerator(InventoryGenerator newGen)
    {
        EnsureGeneratorExists();
        if (_inventoryGenerator == null || _inventoryGenerator.AllSlots == null || newGen.AllSlots == null) return;

        for (int i = 0; i < _inventoryGenerator.AllSlots.Count && i < newGen.AllSlots.Count; i++)
        {
            var mainSlot = _inventoryGenerator.AllSlots[i];
            newGen.AllSlots[i].RefreshSlotWithoutSync(mainSlot.currentData, mainSlot.currentCount);
        }
        Debug.Log($"[InventoryManager] 신규 인벤토리 UI({newGen.name})로 기존 메인 데이터를 동기화 완료했습니다.");
    }

    /// <summary>
    /// 다른 인벤토리 슬롯 UI에서 데이터 변경이 감지되면 다른 모든 동기화 대상 UI에도 동기화 값을 복사합니다.
    /// </summary>
    public void SyncSlotAcrossUI(int slotIndex, ItemData data, int count, InventorySlot caller)
    {
        foreach (var gen in _inventoryGenerators)
        {
            if (gen != null && gen.AllSlots != null && slotIndex < gen.AllSlots.Count)
            {
                var targetSlot = gen.AllSlots[slotIndex];
                if (targetSlot != caller)
                {
                    targetSlot.RefreshSlotWithoutSync(data, count);
                }
            }
        }
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
                    if (gen.isMainInventory)
                    {
                        _inventoryGenerator = gen;
                        if (!_inventoryGenerators.Contains(gen)) _inventoryGenerators.Add(gen);
                        break;
                    }
                }
            }
            // fallback if no main inventory found
            if (_inventoryGenerator == null && generators.Length > 0)
            {
                _inventoryGenerator = generators[0];
                if (!_inventoryGenerators.Contains(_inventoryGenerator)) _inventoryGenerators.Add(_inventoryGenerator);
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
    /// 문자열 형태의 타겟 ID(아이템 Name 또는 Id)로 아이템 개수를 반환합니다.
    /// </summary>
    public int GetItemCountByTargetId(string targetId)
    {
        if (string.IsNullOrEmpty(targetId)) return 0;
        
        int totalCount = 0;
        foreach (var slot in GetAllTrackedSlots())
        {
            if (slot.currentData != null)
            {
                if (slot.currentData.Name == targetId || slot.currentData.Id.ToString() == targetId)
                {
                    totalCount += slot.currentCount;
                }
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
