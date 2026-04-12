using UnityEngine;

/// <summary>
/// 획득한 아이템을 인벤토리 빈 슬롯에 동적으로 넣어주는 매니저.
/// InventoryGenerator가 생성해둔 슬롯 정보를 참조합니다.
/// SRP(Single Responsibility Principle) 준수.
/// </summary>
public sealed class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [SerializeField] private InventoryGenerator _inventoryGenerator;

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

    /// <summary>
    /// 아이템을 획득하여 슬롯에 추가.
    /// 스태킹(중첩)은 구현 방식에 따라 다르지만, 여기서는 간단히
    /// 빈 슬롯을 찾아 새로 넣거나, 같은 아이템이면 갯수를 올리는 식으로 구현.
    /// </summary>
    public bool AddItem(ItemData item, int count = 1)
    {
        if (_inventoryGenerator == null || _inventoryGenerator.gameObject.scene.rootCount == 0) 
        {
            // 씬 내의 모든 Generator 중, UI(Canvas) 하위에 존재하는 진짜 Generator만 찾습니다.
            var generators = Resources.FindObjectsOfTypeAll<InventoryGenerator>();
            foreach (var gen in generators)
            {
                if (gen.gameObject.scene.rootCount > 0 && gen.GetComponentInParent<Canvas>() != null)
                {
                    _inventoryGenerator = gen;
                    break;
                }
            }

            if (_inventoryGenerator == null)
            {
                Debug.LogWarning("[InventoryManager] 유효한 UI InventoryGenerator를 씬에서 찾을 수 없습니다.");
                return false;
            }
        }

        // 인벤토리가 아직 생성되지 않았다면 강제로 생성 (UI 비활성화 상태에서 아이템 획득 시 대비)
        if (_inventoryGenerator.AllSlots == null || _inventoryGenerator.AllSlots.Count == 0)
        {
            _inventoryGenerator.GenerateEmptySlots();
        }

        var slots = _inventoryGenerator.AllSlots;
        if (slots == null || slots.Count == 0) 
        {
            Debug.LogWarning("[InventoryManager] 강제 생성 후에도 AllSlots가 비어 있습니다. 컨테이너/프리팹을 확인하세요.");
            return false;
        }

        InventorySlot firstEmptySlot = null;

        // 디버깅: 찾은 InventoryGenerator가 실제 UI인지 확인
        string generatorLoc = _inventoryGenerator.transform.parent != null ? _inventoryGenerator.transform.parent.name : "NoParent";
        Debug.Log($"[InventoryManager] 아이템 획득 시도 중: {item.Name}. 사용된 Generator: {_inventoryGenerator.gameObject.name} (Parent: {generatorLoc}), 슬롯 총 개수: {slots.Count}");

        // 1. 이미 같은 아이템이 있는 슬롯 찾기 (스태킹 가능 시)
        // 2. 빈 슬롯 찾기
        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot.currentData == item)
            {
                slot.RefreshSlot(item, slot.currentCount + count);
                Debug.Log($"[InventoryManager] 중첩: {item.Name} 아이템 획득! (현재 총 {slot.currentCount}개) - 슬롯 인덱스: {i}");
                return true;
            }
            if (slot.currentData == null && firstEmptySlot == null)
            {
                firstEmptySlot = slot;
                // 바로 리턴하지 않고 남은 슬롯 중 중첩 가능한게 있는지 더 검사하기 위해 킵
            }
        }

        // 같은 아이템을 못 찾았고, 빈 슬롯이 있으면 새로 할당
        if (firstEmptySlot != null)
        {
            firstEmptySlot.RefreshSlot(item, count);
            Debug.Log($"[InventoryManager] 신규: {item.Name} 아이템 획득! 빈 슬롯에 찰칵!");
            return true;
        }

        // 인벤토리 꽉 참
        Debug.LogWarning("[InventoryManager] 획득 불가: 인벤토리가 꽉 찼습니다!");
        return false;
    }
}
