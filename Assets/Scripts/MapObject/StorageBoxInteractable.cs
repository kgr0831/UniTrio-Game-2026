using System;
using UnityEngine;

/// <summary>
/// 보관 상자 월드 오브젝트.
/// IInteractable을 구현하여 플레이어가 접근 시 보관 UI를 열 수 있게 합니다.
/// 
/// 인스턴스별 영속 슬롯 데이터를 관리합니다 (상자마다 독립).
/// SRP: 이 클래스는 슬롯 데이터/로직만 담당. UI 렌더링은 StorageBoxUIPanel이 담당.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class StorageBoxInteractable : MonoBehaviour, IInteractable
{
    // ── 슬롯 데이터 구조 ────────────────────────────────────
    [Serializable]
    public struct StorageSlot
    {
        public ItemData StoredItem;
        public int Count;

        public bool IsEmpty => StoredItem == null;

        public void Clear()
        {
            StoredItem = null;
            Count = 0;
        }
    }

    // ── 설정 ────────────────────────────────────────────────
    [Header("Box Settings")]
    [Tooltip("이 상자의 데이터 (StorageBoxData SO). BuildingEntity가 사용하는 것과 동일한 에셋")]
    [SerializeField] private StorageBoxData _boxData;

    // ── 인스턴스별 영속 슬롯 배열 ───────────────────────────
    private StorageSlot[] _slots;
    private bool _initialized;

    // ── 이벤트 (UI 갱신용 — 데이터-렌더링 분리) ─────────────
    /// <summary>특정 슬롯 변경 시 발행 (슬롯 인덱스)</summary>
    public event Action<int> OnSlotChanged;

    // ── IInteractable 구현 ──────────────────────────────────
    public string InteractionPrompt => "Press F";

    public bool CanInteract(GameObject player)
    {
        return player != null;
    }

    public void Interact(GameObject player)
    {
        Debug.Log($"[StorageBox] Interact() 호출됨. UIPanel.Instance = {(StorageBoxUIPanel.Instance != null ? "있음" : "NULL")}");

        // UI가 없으면 무시 — StorageBoxUIPanel은 씬에 하나만 존재
        if (StorageBoxUIPanel.Instance != null)
        {
            StorageBoxUIPanel.Instance.Open(this);
        }
        else
        {
            Debug.LogWarning("[StorageBox] StorageBoxUIPanel.Instance가 null입니다! 씬에 StorageBoxUIPanel 컴포넌트를 추가하세요.");
        }
    }

    // ── 초기화 ──────────────────────────────────────────────

    private void Awake()
    {
        EnsureInitialized();
    }

    /// <summary>
    /// 슬롯 배열을 지연 초기화합니다.
    /// Awake 이전에 외부에서 접근할 경우를 대비한 방어적 초기화.
    /// </summary>
    private void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        int slotCount = _boxData != null ? _boxData.SlotCount : 20;
        _slots = new StorageSlot[slotCount];
    }

    // ── 읽기 전용 접근자 ────────────────────────────────────

    /// <summary>이 상자의 총 슬롯 수</summary>
    public int SlotCount
    {
        get
        {
            EnsureInitialized();
            return _slots.Length;
        }
    }

    /// <summary>특정 슬롯의 현재 상태를 복사본으로 반환</summary>
    public StorageSlot GetSlot(int index)
    {
        EnsureInitialized();
        if (index < 0 || index >= _slots.Length) return default;
        return _slots[index];
    }

    /// <summary>StorageBoxData 참조 반환</summary>
    public StorageBoxData BoxData => _boxData;

    // ── 아이템 추가 ─────────────────────────────────────────

    /// <summary>
    /// 특정 슬롯에 아이템을 추가합니다.
    /// - 빈 슬롯: 아이템 배치
    /// - 같은 아이템: 수량 스태킹
    /// - 다른 아이템: 스왑 (기존 아이템을 out으로 반환)
    /// </summary>
    /// <returns>성공 여부</returns>
    public bool TryAddItem(int slotIndex, ItemData item, int count,
                           out ItemData swappedItem, out int swappedCount)
    {
        swappedItem = null;
        swappedCount = 0;

        EnsureInitialized();
        if (slotIndex < 0 || slotIndex >= _slots.Length) return false;
        if (item == null || count <= 0) return false;

        // 스킬은 상자에 보관 불가
        if (item is SkillData)
        {
            Debug.Log("[StorageBox] 스킬은 상자에 보관할 수 없습니다.");
            return false;
        }

        StorageSlot slot = _slots[slotIndex];

        // Case 1: 빈 슬롯 — 바로 배치
        if (slot.IsEmpty)
        {
            _slots[slotIndex].StoredItem = item;
            _slots[slotIndex].Count = count;
            OnSlotChanged?.Invoke(slotIndex);
            return true;
        }

        // Case 2: 같은 아이템 — 스태킹
        if (slot.StoredItem == item)
        {
            _slots[slotIndex].Count += count;
            OnSlotChanged?.Invoke(slotIndex);
            return true;
        }

        // Case 3: 다른 아이템 — 스왑
        swappedItem = slot.StoredItem;
        swappedCount = slot.Count;

        _slots[slotIndex].StoredItem = item;
        _slots[slotIndex].Count = count;
        OnSlotChanged?.Invoke(slotIndex);
        return true;
    }

    /// <summary>
    /// 특정 슬롯의 아이템을 전량 제거하고 반환합니다.
    /// </summary>
    public bool RemoveItem(int slotIndex, out ItemData removedItem, out int removedCount)
    {
        removedItem = null;
        removedCount = 0;

        EnsureInitialized();
        if (slotIndex < 0 || slotIndex >= _slots.Length) return false;
        if (_slots[slotIndex].IsEmpty) return false;

        removedItem = _slots[slotIndex].StoredItem;
        removedCount = _slots[slotIndex].Count;
        _slots[slotIndex].Clear();
        OnSlotChanged?.Invoke(slotIndex);
        return true;
    }

    /// <summary>
    /// 특정 슬롯을 직접 갱신합니다. (드래그 드롭 스왑 처리용)
    /// </summary>
    public void SetSlot(int slotIndex, ItemData item, int count)
    {
        EnsureInitialized();
        if (slotIndex < 0 || slotIndex >= _slots.Length) return;

        _slots[slotIndex].StoredItem = item;
        _slots[slotIndex].Count = count;
        OnSlotChanged?.Invoke(slotIndex);
    }
}
