using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // UI 이벤트를 처리하기 위한 필수 임포트

public enum SlotType { Inventory, QuickSlot, SkillSlot, SubWeaponSlot }

public class InventorySlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler {
    [Header("Slot Settings")]
    public SlotType slotType; 
    public Image iconImage;
    public Text countText;
    
    public ItemData currentData; 
    public int currentCount;

    void Start() {
        RefreshSlot(currentData, currentCount);
    }

    public void RefreshSlot(ItemData data, int count) {
        currentData = data;
        currentCount = (data == null) ? 0 : count;
        
        if (currentData != null) {
            if (iconImage != null) { iconImage.sprite = currentData.Icon; iconImage.enabled = true; }
            if (countText != null) {
                countText.text = currentCount > 1 ? currentCount.ToString() : ""; 
                countText.enabled = true;
            }
        } else {
            if (iconImage != null) iconImage.enabled = false;
            if (countText != null) countText.enabled = false;
        }
    }

    public void OnBeginDrag(PointerEventData eventData) {
        if (currentData == null) return;
        // 수정된 DragManager에 맞춰 데이터와 '나 자신(this)'을 전달
        DragManager.Instance.StartDrag(currentData, currentCount, this, iconImage.sprite);
    }

    public void OnDrag(PointerEventData eventData) {
        DragManager.Instance.UpdateDragPosition(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData) {
        DragManager.Instance.EndDrag();
    }

    public void OnDrop(PointerEventData eventData) 
    {
        DragManager dm = DragManager.Instance;
        ItemData draggedData = dm.draggingData;
        InventorySlot fromSlot = dm.startSlot;

        // 데이터가 없거나 자기 자신에게 드랍한 경우 무시
        if (draggedData == null || fromSlot == null || fromSlot == this) return;

        // --- 스왑(Swap) 처리를 위한 로직 ---
        
        if (this.slotType == SlotType.SkillSlot) 
        {
            Debug.Log("스킬창의 스킬 위치는 변경할 수 없습니다.");
            return; 
        }
        
        // 1. 내가 인벤토리 슬롯일 때
        if (this.slotType == SlotType.Inventory) 
        {
            // 스킬은 인벤토리에 들어올 수 없음
            if (draggedData is SkillData) {
                Debug.LogWarning("스킬은 인벤토리에 보관할 수 없습니다.");
                return; 
            }

            // 현재 내 칸에 있던 데이터를 출발지 슬롯으로 보낼 준비
            ItemData myOldData = this.currentData;
            int myOldCount = this.currentCount;

            // 나(도착지)를 새 데이터로 갱신
            RefreshSlot(draggedData, dm.draggingCount);

            if (fromSlot.slotType != SlotType.SkillSlot) {
                fromSlot.RefreshSlot(myOldData, myOldCount);
            }
        }
        // 2. 내가 퀵슬롯일 때
        else if (this.slotType == SlotType.QuickSlot) 
        {
            ItemData myOldData = this.currentData;
            int myOldCount = this.currentCount;

            // 나(도착지)를 새 데이터로 갱신
            RefreshSlot(draggedData, dm.draggingCount);

            // [출발지 슬롯 처리]
            // [수정] 출발지가 스킬창이 아닐 때만 출발지 슬롯을 갱신(이동/스왑)합니다.
            if (fromSlot.slotType != SlotType.SkillSlot) {
                if (myOldData is SkillData && fromSlot.slotType == SlotType.Inventory) {
                    fromSlot.RefreshSlot(null, 0);
                } else {
                    fromSlot.RefreshSlot(myOldData, myOldCount);
                }
            }
            
            Debug.Log($"{draggedData.Name}이(가) 퀵슬롯에 등록/교체되었습니다.");
        }
        // 3. 내가 보조 무기 슬롯일 때
        else if (this.slotType == SlotType.SubWeaponSlot)
        {
            // 무기만 허용
            if (!(draggedData is WeaponData))
            {
                Debug.LogWarning("보조 무기 슬롯에는 무기만 등록할 수 있습니다!");
                return;
            }

            ItemData myOldData = this.currentData;
            int myOldCount = this.currentCount;

            RefreshSlot(draggedData, dm.draggingCount);

            if (fromSlot.slotType != SlotType.SkillSlot)
            {
                fromSlot.RefreshSlot(myOldData, myOldCount);
            }

            Debug.Log($"{draggedData.Name}이(가) 보조 무기 슬롯에 장착되었습니다.");
        }
    }
}

