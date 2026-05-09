using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // UI 이벤트를 처리하기 위한 필수 임포트

public enum SlotType { Inventory, QuickSlot, SubWeaponSlot, SkillTreeNode }

public class InventorySlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler {
    [Header("Slot Settings")]
    public SlotType slotType; 
    public Image iconImage;
    public Text countText;
    
    public ItemData currentData;
    public int currentCount;

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
            } else {
                if (iconImage != null) iconImage.enabled = false;
                if (countText != null) countText.enabled = false;
            }
        }
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

        // 데이터가 없거나 자기 자신에게 드랍한 경우 무시
        if (draggedData == null || fromSlot == null || fromSlot == this) return;

        // --- 스왑(Swap) 처리를 위한 로직 ---
        
        // 1. 내가 인벤토리 슬롯일 때
        if (this.slotType == SlotType.Inventory) 
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
                dm.dragConsumed = true;

                // 출발지 슬롯 비우기 (합산했으므로)
                if (fromSlot.slotType != SlotType.SkillTreeNode)
                {
                    fromSlot.RefreshSlot(null, 0);
                }
                return;
            }

            // 다른 아이템 → 스왑(Swap) 처리
            RefreshSlot(draggedData, dm.draggingCount);
            dm.dragConsumed = true;

            if (fromSlot.slotType != SlotType.SkillTreeNode) {
                fromSlot.RefreshSlot(myOldData, myOldCount);
            }
        }
        // 2. 내가 퀵슬롯일 때
        else if (this.slotType == SlotType.QuickSlot) 
        {
            if (draggedData is WeaponData) {
                if (NotificationUI.Instance != null) NotificationUI.Instance.ShowMessage("무기는 퀵슬롯에 배치할 수 없습니다!");
                else Debug.LogWarning("무기는 퀵슬롯에 배치할 수 없습니다.");
                return;
            }

            ItemData myOldData = this.currentData;
            int myOldCount = this.currentCount;

            // 나(도착지)를 새 데이터로 갱신
            RefreshSlot(draggedData, dm.draggingCount);
            dm.dragConsumed = true;

            // [출발지 슬롯 처리]
            if (fromSlot.slotType != SlotType.SkillTreeNode) {
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
                if (NotificationUI.Instance != null) NotificationUI.Instance.ShowMessage("보조 무기 슬롯에는 무기만 등록할 수 있습니다!");
                else Debug.LogWarning("보조 무기 슬롯에는 무기만 등록할 수 있습니다!");
                return;
            }

            ItemData myOldData = this.currentData;
            int myOldCount = this.currentCount;

            RefreshSlot(draggedData, dm.draggingCount);
            dm.dragConsumed = true;

            if (fromSlot.slotType != SlotType.SkillTreeNode)
            {
                fromSlot.RefreshSlot(myOldData, myOldCount);
            }

            Debug.Log($"{draggedData.Name}이(가) 보조 무기 슬롯에 장착되었습니다.");
        }
        // 4. 내가 스킬트리 노드일 때
        else if (this.slotType == SlotType.SkillTreeNode)
        {
            if (!(draggedData is SkillData)) {
                return; // 스킬만 허용
            }
            if (_originalSkillData != null && draggedData != _originalSkillData) {
                return; // 원래 자신의 스킬만 다시 장착 가능
            }
            
            RefreshSlot(draggedData, dm.draggingCount);
            dm.dragConsumed = true;

            if (fromSlot.slotType != SlotType.SkillTreeNode) {
                fromSlot.RefreshSlot(null, 0);
            }
        }
    }
}
