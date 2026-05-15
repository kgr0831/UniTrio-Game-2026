using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // UI 이벤트를 처리하기 위한 필수 임포트

public enum SlotType { Inventory, QuickSlot, EquipmentSlot, SkillTreeNode }

public class InventorySlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler {
    [Header("Slot Settings")]
    public SlotType slotType; 
    public Image iconImage;
    public Text countText;
    
    public ItemData currentData;
    public int currentCount;
    
    [Header("Sync System")]
    public int slotIndex = -1; // 이 슬롯이 인벤토리 제네레이터에서 몇 번째 인덱스인지 저장
    private bool _isSyncing = false;

    [Header("Equipment Slot Filter")]
    [Tooltip("EquipmentSlot 전용: 이 슬롯에 허용되는 GadgetType")]
    public GadgetType allowedGadgetType;

    [Header("Cooldown Overlay")]
    [Tooltip("퀵슬롯 전용: 쿨다운 표시용 Filled Image (Radial 360)")]
    public Image cooldownOverlay;

    [Header("Skill Tree Node")]
    [Tooltip("스킬트리 노드 전용: 스킬이 빠졌을 때의 alpha값")]
    [SerializeField] private float _emptyNodeAlpha = 0.3f;
    private SkillData _originalSkillData;

    void Start() {
        // 퀵슬롯이면 쿨다운 오버레이 자동 생성
        if (slotType == SlotType.QuickSlot && cooldownOverlay == null) {
            CreateCooldownOverlay();
        }
        RefreshSlot(currentData, currentCount);
    }

    private void CreateCooldownOverlay() {
        GameObject overlayGo = new GameObject("CooldownOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlayGo.transform.SetParent(transform, false);

        RectTransform rt = overlayGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Radial360 Filled 모드에는 Sprite가 필요 — 1x1 흰색 텍스처로 생성
        Texture2D tex = new Texture2D(4, 4);
        Color[] pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        Sprite whiteSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));

        Image img = overlayGo.GetComponent<Image>();
        img.sprite = whiteSprite;
        img.color = new Color(0f, 0f, 0f, 0.85f);
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Radial360;
        img.fillOrigin = (int)Image.Origin360.Top;
        img.fillClockwise = true;
        img.fillAmount = 0f;
        img.raycastTarget = false;
        img.enabled = false;

        cooldownOverlay = img;
    }

    public void RefreshSlot(ItemData data, int count) {
        currentData = data;
        currentCount = (data == null) ? 0 : count;
        
        if (slotType == SlotType.SkillTreeNode && _originalSkillData == null && data is SkillData) {
            _originalSkillData = data as SkillData;
        }

        if (currentData != null) {
            if (iconImage != null) { 
                iconImage.sprite = currentData.Icon; 
                iconImage.enabled = true; 
                Color c = iconImage.color;
                c.a = 1f;
                iconImage.color = c;
            } else {
                Debug.LogWarning($"[InventorySlot] {gameObject.name}에 iconImage 컴포넌트 참조가 없습니다!");
            }

            if (countText != null) {
                countText.text = currentCount > 1 ? currentCount.ToString() : ""; 
                countText.enabled = true;
            }
        } else {
            if (slotType == SlotType.SkillTreeNode && _originalSkillData != null) {
                if (iconImage != null) {
                    iconImage.sprite = _originalSkillData.Icon;
                    iconImage.enabled = true;
                    Color c = iconImage.color;
                    c.a = _emptyNodeAlpha;
                    iconImage.color = c;
                }
                if (countText != null) countText.enabled = false;
            } else if (slotType == SlotType.EquipmentSlot) {
                // 장비 슬롯은 비어있어도 자식 아이콘(placeholder)을 유지
                if (countText != null) countText.enabled = false;
            } else {
                if (iconImage != null) iconImage.enabled = false;
                if (countText != null) countText.enabled = false;
            }
        }

        // 다른 동기화 인벤토리 UI에 변경 사항 브로드캐스팅
        if (!_isSyncing && slotIndex >= 0 && slotType == SlotType.Inventory && InventoryManager.Instance != null)
        {
            InventoryManager.Instance.SyncSlotAcrossUI(slotIndex, currentData, currentCount, this);
        }
    }

    /// <summary>
    /// 무한 루프 방지를 위해 동기화 브로드캐스트를 쏘지 않고 슬롯을 갱신합니다.
    /// </summary>
    public void RefreshSlotWithoutSync(ItemData data, int count)
    {
        _isSyncing = true;
        RefreshSlot(data, count);
        _isSyncing = false;
    }

    public void OnBeginDrag(PointerEventData eventData) {
        if (InventoryToggle.Instance != null && !InventoryToggle.Instance.IsAnyPanelOpen()) return;

        if (currentData == null) return;

        // StartDrag를 먼저 호출하여 데이터를 DragManager에 캐싱
        DragManager.Instance.StartDrag(currentData, currentCount, this, iconImage.sprite);

        // 드래그 중 원본 아이콘 숨기기 (이동 느낌)
        if (slotType == SlotType.SkillTreeNode) {
            RefreshSlot(null, 0);
        } else {
            if (iconImage != null) iconImage.enabled = false;
            if (countText != null) countText.enabled = false;
        }
    }

    public void OnDrag(PointerEventData eventData) {
        DragManager.Instance.UpdateDragPosition(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData) {
        DragManager dm = DragManager.Instance;
        // 드롭이 성공하지 않았으면 원래 슬롯 복원
        if (!dm.dragConsumed && dm.startSlot != null) {
            dm.startSlot.RefreshSlot(dm.draggingData, dm.draggingCount);
        }
        dm.EndDrag();
    }

    public void OnDrop(PointerEventData eventData) 
    {
        if (InventoryToggle.Instance != null && !InventoryToggle.Instance.IsAnyPanelOpen()) return;

        DragManager dm = DragManager.Instance;
        ItemData draggedData = dm.draggingData;
        InventorySlot fromSlot = dm.startSlot;
        StorageBoxSlotUI fromBoxSlot = dm.startBoxSlot;

        // 데이터가 없거나, 자기 자신에게 드랍한 경우 무시
        if (draggedData == null || fromSlot == this || (fromSlot == null && fromBoxSlot == null)) return;

        // --- 슬롯 타입별 드롭 처리 (OCP — 새 슬롯 타입은 분기 추가만으로 확장) ---
        
        // 1. 내가 인벤토리 슬롯일 때
        if (this.slotType == SlotType.Inventory) 
        {
            HandleInventoryDrop(dm, draggedData, fromSlot, fromBoxSlot);
        }
        // 2. 내가 퀵슬롯일 때 (무기 포함 모든 아이템 허용)
        else if (this.slotType == SlotType.QuickSlot) 
        {
            HandleQuickSlotDrop(dm, draggedData, fromSlot, fromBoxSlot);
        }
        // 3. 내가 장비 슬롯일 때 (GadgetType 필터링)
        else if (this.slotType == SlotType.EquipmentSlot)
        {
            HandleEquipmentSlotDrop(dm, draggedData, fromSlot, fromBoxSlot);
        }
        // 4. 내가 스킬트리 노드일 때
        else if (this.slotType == SlotType.SkillTreeNode)
        {
            HandleSkillTreeDrop(dm, draggedData, fromSlot, fromBoxSlot);
        }
    }

    // ── 인벤토리 슬롯 드롭 처리 ──────────────────────────────────────
    private void HandleInventoryDrop(DragManager dm, ItemData draggedData, InventorySlot fromSlot, StorageBoxSlotUI fromBoxSlot)
    {
        // 스킬은 인벤토리에 들어올 수 없음
        if (draggedData is SkillData) {
            if (NotificationUI.Instance != null) NotificationUI.Instance.ShowMessage("스킬은 인벤토리에 보관할 수 없습니다.");
            else Debug.LogWarning("스킬은 인벤토리에 보관할 수 없습니다.");
            return; 
        }

        ItemData myOldData = this.currentData;
        int myOldCount = this.currentCount;

        // ★ 동일 아이템 스태킹: 같은 아이템이면 수량 합산
        if (myOldData != null && myOldData == draggedData)
        {
            RefreshSlot(draggedData, myOldCount + dm.draggingCount);
            dm.dragConsumed = true; // 소비됨. 상자 슬롯은 복원하지 않음.

            if (fromSlot != null && fromSlot.slotType != SlotType.SkillTreeNode)
            {
                fromSlot.RefreshSlot(null, 0);
            }
            return;
        }

        // 다른 아이템 → 스왑(Swap) 처리
        RefreshSlot(draggedData, dm.draggingCount);
        
        if (fromSlot != null && fromSlot.slotType != SlotType.SkillTreeNode) {
            fromSlot.RefreshSlot(myOldData, myOldCount);
            dm.dragConsumed = true;
        }
        else if (fromBoxSlot != null) {
            if (myOldData != null) {
                // 상자에서 왔고, 내 자리에 다른 아이템이 있었음 -> 스왑
                // dragConsumed를 false로 두고, 드래그 데이터를 내 데이터로 바꿔치기
                // 그러면 BoxSlot.OnEndDrag에서 상자에 해당 아이템을 넣어줍니다.
                dm.draggingData = myOldData;
                dm.draggingCount = myOldCount;
                dm.dragConsumed = false;
            } else {
                dm.dragConsumed = true; // 비어있었으므로 그냥 넣음
            }
        }
    }

    // ── 퀵슬롯 드롭 처리 (무기 포함 허용) ─────────────────────────────
    private void HandleQuickSlotDrop(DragManager dm, ItemData draggedData, InventorySlot fromSlot, StorageBoxSlotUI fromBoxSlot)
    {
        ItemData myOldData = this.currentData;
        int myOldCount = this.currentCount;

        // 나(도착지)를 새 데이터로 갱신
        RefreshSlot(draggedData, dm.draggingCount);
        
        // [출발지 슬롯 처리]
        if (fromSlot != null && fromSlot.slotType != SlotType.SkillTreeNode) {
            if (myOldData is SkillData && fromSlot.slotType == SlotType.Inventory) {
                fromSlot.RefreshSlot(null, 0);
            } else {
                fromSlot.RefreshSlot(myOldData, myOldCount);
            }
            dm.dragConsumed = true;
        }
        else if (fromBoxSlot != null) {
            if (myOldData != null && !(myOldData is SkillData)) {
                dm.draggingData = myOldData;
                dm.draggingCount = myOldCount;
                dm.dragConsumed = false; 
            } else {
                dm.dragConsumed = true;
            }
        }
        
        Debug.Log($"{draggedData.Name}이(가) 퀵슬롯에 등록/교체되었습니다.");
    }

    // ── 장비 슬롯 드롭 처리 (GadgetType 필터링) ────────────────────────
    private void HandleEquipmentSlotDrop(DragManager dm, ItemData draggedData, InventorySlot fromSlot, StorageBoxSlotUI fromBoxSlot)
    {
        // GadgetData만 허용
        if (!(draggedData is GadgetData gadget))
        {
            if (NotificationUI.Instance != null) 
                NotificationUI.Instance.ShowMessage("장비 아이템만 장착할 수 있습니다!");
            else 
                Debug.LogWarning("장비 아이템만 장착할 수 있습니다!");
            return;
        }

        // GadgetType 필터링 — 슬롯에 지정된 종류만 장착 가능
        if (gadget.GadgetType != allowedGadgetType)
        {
            if (NotificationUI.Instance != null) 
                NotificationUI.Instance.ShowMessage($"이 슬롯에는 {allowedGadgetType} 장비만 장착 가능합니다!");
            else 
                Debug.LogWarning($"[EquipmentSlot] {gadget.GadgetType} != {allowedGadgetType}");
            return;
        }

        ItemData myOldData = this.currentData;
        int myOldCount = this.currentCount;

        // 기존 장비 해제 → EquipmentManager에 알림 (DIP)
        if (myOldData is GadgetData oldGadget)
        {
            if (EquipmentManager.Instance != null)
                EquipmentManager.Instance.OnUnequip(this);
        }

        // 새 장비 장착
        RefreshSlot(draggedData, dm.draggingCount);
        
        // EquipmentManager에 장착 알림 → StatSystem 보너스 등록
        if (EquipmentManager.Instance != null)
            EquipmentManager.Instance.OnEquip(this, gadget);

        // 출발지 슬롯에 이전 장비 반환 (스왑)
        if (fromSlot != null && fromSlot.slotType != SlotType.SkillTreeNode)
        {
            fromSlot.RefreshSlot(myOldData, myOldCount);
            dm.dragConsumed = true;
        }
        else if (fromBoxSlot != null) {
            if (myOldData != null) {
                dm.draggingData = myOldData;
                dm.draggingCount = myOldCount;
                dm.dragConsumed = false;
            } else {
                dm.dragConsumed = true;
            }
        }

        Debug.Log($"[Equipment] {gadget.Name} ({gadget.GadgetType}) 장착 완료");
    }

    // ── 스킬트리 노드 드롭 처리 ──────────────────────────────────────
    private void HandleSkillTreeDrop(DragManager dm, ItemData draggedData, InventorySlot fromSlot, StorageBoxSlotUI fromBoxSlot)
    {
        if (!(draggedData is SkillData)) {
            return; // 스킬만 허용
        }
        if (_originalSkillData != null && draggedData != _originalSkillData) {
            return; // 원래 자신의 스킬만 다시 장착 가능
        }
        
        RefreshSlot(draggedData, dm.draggingCount);
        dm.dragConsumed = true;

        if (fromSlot != null && fromSlot.slotType != SlotType.SkillTreeNode) {
            fromSlot.RefreshSlot(null, 0);
        }
    }
}
