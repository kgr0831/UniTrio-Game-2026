using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 보관 상자의 개별 슬롯 UI.
/// 기존 DragManager 시스템과 완전 호환되는 드래그 드롭을 구현합니다.
///
/// 드래그 흐름:
///   - 인벤토리 → 상자: InventorySlot.OnBeginDrag → DragManager → 이 슬롯의 OnDrop
///   - 상자 → 인벤토리: 이 슬롯의 OnBeginDrag → DragManager → InventorySlot.OnDrop
///   - 상자 → 상자: 이 슬롯의 OnBeginDrag → DragManager → 다른 상자 슬롯의 OnDrop
///
/// SRP: 이 클래스는 UI 렌더링 + 드래그 드롭 이벤트 처리만 담당.
///      데이터 변경은 StorageBoxInteractable에 위임.
/// </summary>
public class StorageBoxSlotUI : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [Header("UI References")]
    [SerializeField] private Image _iconImage;
    [SerializeField] private Text _countText;

    // 바인딩된 상자와 슬롯 인덱스
    private StorageBoxInteractable _boundBox;
    private int _slotIndex = -1;

    // 현재 표시 중인 데이터 캐싱 (드래그 시작 시 참조)
    private ItemData _displayedItem;
    private int _displayedCount;

    // 외부 노출 프로퍼티
    public StorageBoxInteractable BoundBox => _boundBox;
    public int SlotIndex => _slotIndex;

    // ── 초기화 ──────────────────────────────────────────────

    /// <summary>
    /// StorageBoxUIPanel에서 호출하여 특정 상자의 슬롯에 바인딩합니다.
    /// </summary>
    public void Bind(StorageBoxInteractable box, int slotIndex)
    {
        _boundBox = box;
        _slotIndex = slotIndex;

        RefreshDisplay();
    }

    /// <summary>
    /// 바인딩 해제 시 호출합니다.
    /// </summary>
    public void Unbind()
    {
        _boundBox = null;
        _slotIndex = -1;
        _displayedItem = null;
        _displayedCount = 0;

        if (_iconImage != null) _iconImage.enabled = false;
        if (_countText != null) _countText.enabled = false;
    }

    // ── 디스플레이 갱신 ─────────────────────────────────────

    /// <summary>
    /// 상자 데이터로부터 현재 슬롯 상태를 읽어 아이콘/수량 텍스트를 갱신합니다.
    /// </summary>
    public void RefreshDisplay()
    {
        if (_boundBox == null || _slotIndex < 0)
        {
            ClearDisplay();
            return;
        }

        StorageBoxInteractable.StorageSlot slot = _boundBox.GetSlot(_slotIndex);

        _displayedItem = slot.StoredItem;
        _displayedCount = slot.Count;

        if (_displayedItem != null)
        {
            if (_iconImage != null)
            {
                _iconImage.sprite = _displayedItem.Icon;
                _iconImage.enabled = true;
            }

            if (_countText != null)
            {
                _countText.text = _displayedCount > 1 ? _displayedCount.ToString() : "";
                _countText.enabled = true;
            }
        }
        else
        {
            ClearDisplay();
        }
    }

    private void ClearDisplay()
    {
        _displayedItem = null;
        _displayedCount = 0;

        if (_iconImage != null) _iconImage.enabled = false;
        if (_countText != null) _countText.enabled = false;
    }

    // ── 드래그 시작 (상자에서 아이템 꺼내기) ─────────────────

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_displayedItem == null || _boundBox == null) return;

        // DragManager에 드래그 시작 알림
        // startSlot은 null로 전달 — 상자 슬롯은 InventorySlot이 아니므로
        // EndDrag에서 별도 처리 필요
        DragManager.Instance.StartDrag(_displayedItem, _displayedCount, null, _iconImage.sprite);
        DragManager.Instance.startBoxSlot = this;

        // 드래그 중에는 상자 슬롯 데이터를 임시 제거 (이동 느낌)
        _boundBox.SetSlot(_slotIndex, null, 0);

        // 아이콘 숨기기
        if (_iconImage != null) _iconImage.enabled = false;
        if (_countText != null) _countText.enabled = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        DragManager.Instance.UpdateDragPosition(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        DragManager dm = DragManager.Instance;

        // 드롭이 성공하지 않았으면 원래 상자 슬롯으로 복원
        if (!dm.dragConsumed && dm.draggingData != null)
        {
            _boundBox.SetSlot(_slotIndex, dm.draggingData, dm.draggingCount);
        }

        dm.EndDrag();

        // 디스플레이 갱신 (복원되었든 소비되었든 최신 상태 반영)
        RefreshDisplay();
    }

    // ── 드롭 수신 (인벤토리/다른 상자에서 여기로) ──────────────

    public void OnDrop(PointerEventData eventData)
    {
        DragManager dm = DragManager.Instance;
        ItemData draggedData = dm.draggingData;
        InventorySlot fromSlot = dm.startSlot; // 인벤토리 슬롯이면 non-null, 상자 슬롯이면 null

        if (draggedData == null) return;
        if (_boundBox == null) return;

        // 스킬은 상자에 보관 불가
        if (draggedData is SkillData)
        {
            if (NotificationUI.Instance != null)
                NotificationUI.Instance.ShowMessage("스킬은 상자에 보관할 수 없습니다.");
            return;
        }

        // 상자 데이터에 아이템 추가 시도
        ItemData swappedItem;
        int swappedCount;
        bool success = _boundBox.TryAddItem(_slotIndex, draggedData, dm.draggingCount,
                                             out swappedItem, out swappedCount);

        if (!success) return;

        dm.dragConsumed = true;

        // 출발지 슬롯 처리 (인벤토리 → 상자인 경우)
        if (fromSlot != null)
        {
            // 스왑이 발생했으면 출발지 슬롯에 스왑된 아이템 반환
            if (swappedItem != null)
            {
                fromSlot.RefreshSlot(swappedItem, swappedCount);
            }
            else
            {
                fromSlot.RefreshSlot(null, 0);
            }
        }
        else if (dm.startBoxSlot != null && dm.startBoxSlot != this)
        {
            // 상자 → 상자 스왑인 경우: 원래 드래그를 시작한 상자 슬롯(startBoxSlot)에 스왑된 아이템을 돌려줍니다.
            if (swappedItem != null)
            {
                dm.startBoxSlot.BoundBox.SetSlot(dm.startBoxSlot.SlotIndex, swappedItem, swappedCount);
                dm.startBoxSlot.RefreshDisplay();
            }
            else
            {
                dm.startBoxSlot.BoundBox.SetSlot(dm.startBoxSlot.SlotIndex, null, 0);
                dm.startBoxSlot.RefreshDisplay();
            }
        }

        // 디스플레이 갱신
        RefreshDisplay();
    }
}
