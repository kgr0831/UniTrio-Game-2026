using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 스크롤뷰 내 재료 항목 프리팹에 부착.
/// Bonfire에서 조리 가능한 아이템을 표시하고, 인풋 슬롯으로 드래그&드롭을 지원합니다.
///
/// 수량 0일 때: CanvasGroup.alpha = 0.3f (고스트 효과) + 드래그 불가
/// 수량 1 이상: 정상 표시 + 드래그 가능
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class BonfireIngredientEntry : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("UI References")]
    [SerializeField] private Image _itemIcon;
    [SerializeField] private Text _itemName;
    [SerializeField] private Text _itemCount;

    // BonfireFuelEntry가 같은 프리팹의 SerializeField 참조를 가져갈 수 있도록
    public Image IconImage => _itemIcon;
    public Text NameText => _itemName;
    public Text CountText => _itemCount;

    private ConsumableData _foodData;
    private int _currentCount;
    private CanvasGroup _canvasGroup;

    // 드래그 시 사용할 고스트 아이콘 (DragManager와 연동)
    private bool _isDragging;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
    }

    /// <summary>
    /// 재료 엔트리를 초기화합니다. BonfireUIPanel.PopulateIngredientList()에서 호출.
    /// </summary>
    public void Setup(ConsumableData food, int count)
    {
        _foodData = food;
        UpdateCount(count);

        if (_itemIcon != null && food.Icon != null)
        {
            _itemIcon.sprite = food.Icon;
            _itemIcon.enabled = true;
        }

        if (_itemName != null)
        {
            _itemName.text = food.Name;
        }
    }

    /// <summary>
    /// 수량을 갱신하고 고스트 효과를 적용/해제합니다.
    /// </summary>
    public void UpdateCount(int count)
    {
        _currentCount = count;

        if (_itemCount != null)
        {
            _itemCount.text = count.ToString();
        }

        // 수량 0: 고스트 효과 (반투명 + 드래그 불가)
        if (_canvasGroup != null)
        {
            bool hasStock = count > 0;
            _canvasGroup.alpha = hasStock ? 1f : 0.3f;
            _canvasGroup.blocksRaycasts = hasStock;
        }
    }

    /// <summary>해당 엔트리의 음식 데이터를 반환합니다.</summary>
    public ConsumableData FoodData => _foodData;

    // ── 드래그 핸들러 ───────────────────────────────────────

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_foodData == null || _currentCount <= 0) return;

        _isDragging = true;

        // DragManager를 통해 드래그 데이터 전달
        if (DragManager.Instance != null)
        {
            DragManager.Instance.StartDrag(_foodData, 1, null, _foodData.Icon);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDragging) return;

        if (DragManager.Instance != null)
        {
            DragManager.Instance.UpdateDragPosition(eventData.position);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_isDragging) return;
        _isDragging = false;

        if (DragManager.Instance != null)
        {
            // 드래그가 소비되지 않았으면 (슬롯에 드롭 안 됨) 아무 것도 하지 않음
            if (!DragManager.Instance.dragConsumed)
            {
                // 드래그 취소 — 원래 상태 유지
            }
            DragManager.Instance.EndDrag();
        }
    }
}
