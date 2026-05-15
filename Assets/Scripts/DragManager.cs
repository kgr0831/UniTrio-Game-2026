using UnityEngine;
using UnityEngine.UI;

public class DragManager : MonoBehaviour
{
    public static DragManager Instance;

    [Header("Drag Visual")]
    public Image dragVisualIcon;
    public ItemData draggingData;
    public InventorySlot startSlot; // 어디서 드래그를 시작했는지 저장
    public StorageBoxSlotUI startBoxSlot; // 박스 슬롯에서 드래그 시작 시 저장
    public int draggingCount;       // 드래그 중인 아이템의 개수
    public bool dragConsumed;       // 드롭이 성공적으로 처리되었는지 여부

    private Transform _originalParent;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (dragVisualIcon != null)
            dragVisualIcon.gameObject.SetActive(false);
    }

    // 인벤토리 슬롯에서 호출할 때 파라미터를 추가했습니다.
    public void StartDrag(ItemData data, int count, InventorySlot slot, Sprite icon)
    {
        if (dragVisualIcon == null) return;

        draggingData = data;
        draggingCount = count;
        startSlot = slot; // 출발지 슬롯 저장
        dragConsumed = false;

        dragVisualIcon.sprite = icon;
        dragVisualIcon.gameObject.SetActive(true);
        dragVisualIcon.raycastTarget = false;

        // 드래그 비주얼을 최상위 Canvas로 이동시켜 모든 슬롯 위에 렌더링
        _originalParent = dragVisualIcon.transform.parent;
        Canvas rootCanvas = dragVisualIcon.GetComponentInParent<Canvas>()?.rootCanvas;
        if (rootCanvas != null)
        {
            dragVisualIcon.transform.SetParent(rootCanvas.transform, true);
        }
        dragVisualIcon.transform.SetAsLastSibling();
    }

    public void UpdateDragPosition(Vector2 mousePosition)
    {
        if (dragVisualIcon != null && dragVisualIcon.gameObject.activeSelf)
            dragVisualIcon.transform.position = mousePosition;
    }

    public void EndDrag()
    {
        draggingData = null;
        startSlot = null; // 초기화
        startBoxSlot = null; // 상자 출처 초기화
        draggingCount = 0;
        if (dragVisualIcon != null)
        {
            // 원래 부모로 복원
            if (_originalParent != null)
                dragVisualIcon.transform.SetParent(_originalParent, true);
            dragVisualIcon.gameObject.SetActive(false);
        }
    }
}