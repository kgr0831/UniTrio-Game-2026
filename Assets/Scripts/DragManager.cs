using UnityEngine;
using UnityEngine.UI; // UI Image 컴포넌트 제어를 위한 임포트

public class DragManager : MonoBehaviour
{
    // 어디서든 접근 가능한 싱글톤
    public static DragManager Instance;

    [Header("Drag Visual")]
    public Image dragVisualIcon; // Canvas 최하단에 만든 마우스 추적용 이미지
    public ItemData draggingData; // 현재 유저가 들고 있는 데이터 정보

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // 시작 시에는 드래그 비주얼 숨김
        if (dragVisualIcon != null)
            dragVisualIcon.gameObject.SetActive(false);
    }

    // 드래그 시작 시 호출
    public void StartDrag(ItemData data, Sprite icon)
    {
        draggingData = data;
        dragVisualIcon.sprite = icon;
        dragVisualIcon.gameObject.SetActive(true);
        
        // 드래그 아이콘은 마우스 클릭을 방해하면 안 되므로 Raycast Target 해제 확인
        dragVisualIcon.raycastTarget = false;
    }

    // 드래그 중 마우스 위치 갱신 (Screen Space)
    public void UpdateDragPosition(Vector2 mousePosition)
    {
        if (dragVisualIcon.gameObject.activeSelf)
        {
            dragVisualIcon.transform.position = mousePosition;
        }
    }

    // 드래그 종료 시 초기화
    public void EndDrag()
    {
        draggingData = null;
        dragVisualIcon.gameObject.SetActive(false);
    }
}