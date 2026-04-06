using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // UI 이벤트를 처리하기 위한 필수 임포트

public class InventorySlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler {
    public Image iconImage;
    public Text countText;
    
    // 이 부분이 public이어야 유니티 에디터 인스펙터 창에 칸이 생깁니다.
    public ItemData currentData; 
    public int currentCount;

    // 테스트를 위해 Start에서 초기화 로직 추가
    void Start() {
        if (currentData != null) {
            RefreshSlot(currentData, currentCount);
        }
    }

    // 서버 데이터를 UI에 반영할 때 호출
    public void RefreshSlot(ItemData data, int count) {
        currentData = data;
        currentCount = count;
        
        if (currentData != null) {
            iconImage.sprite = currentData._Icon;  // 신규 API: Icon → _Icon
            iconImage.enabled = true;
            countText.text = currentCount.ToString();
        } else {
            iconImage.enabled = false;
            countText.text = "";
        }
    }

    public void OnBeginDrag(PointerEventData eventData) {
        if (currentData == null) return;
        DragManager.Instance.StartDrag(currentData, iconImage.sprite);
    }

    public void OnDrag(PointerEventData eventData) {
        DragManager.Instance.UpdateDragPosition(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData) {
        DragManager.Instance.EndDrag();
    }

    public void OnDrop(PointerEventData eventData) {
        // 이 슬롯이 퀵슬롯일 경우 여기서 데이터를 등록받음
        if (DragManager.Instance.draggingData != null) {
            RefreshSlot(DragManager.Instance.draggingData, 1); // 예시 수량
        }
    }
}
