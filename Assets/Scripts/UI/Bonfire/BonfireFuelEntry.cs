using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 스크롤뷰 내 연료 항목 프리팹에 부착.
/// BonfireIngredientEntry와 동일한 프리팹을 공유하므로,
/// 기존 BonfireIngredientEntry의 [SerializeField] 참조를 그대로 활용합니다.
/// 연료 슬롯(BonfireFuelSlotUI)으로 드래그&드롭을 지원합니다.
///
/// 수량 0일 때: CanvasGroup.alpha = 0.3f (고스트 효과) + 드래그 불가
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class BonfireFuelEntry : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private Image _itemIcon;
    private Text _itemName;
    private Text _itemCount;

    private IngredientData _fuelData;
    private int _currentCount;
    private CanvasGroup _canvasGroup;
    private bool _isDragging;

    /// <summary>
    /// 연료 엔트리를 초기화합니다.
    /// BonfireIngredientEntry의 SerializeField 참조를 가져와서 재사용합니다.
    /// Transform.Find() 대신 확실한 참조를 보장합니다.
    /// </summary>
    public void Setup(IngredientData fuel, int count, GameObject entryGo)
    {
        _fuelData = fuel;
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // BonfireIngredientEntry의 SerializeField 참조를 가져온 후 비활성화
        // 프리팹에 [SerializeField]로 연결된 참조가 Transform.Find()보다 안정적
        BonfireIngredientEntry existingEntry = GetComponent<BonfireIngredientEntry>();
        if (existingEntry != null)
        {
            // 리플렉션 없이 접근하기 위해 public 접근자 사용
            _itemIcon = existingEntry.IconImage;
            _itemName = existingEntry.NameText;
            _itemCount = existingEntry.CountText;
            existingEntry.enabled = false;
        }

        // 아이콘 설정
        if (_itemIcon != null && fuel.Icon != null)
        {
            _itemIcon.sprite = fuel.Icon;
            _itemIcon.enabled = true;
        }

        // 이름 설정 (연료 표시)
        if (_itemName != null)
        {
            _itemName.text = $"{fuel.Name} (연료)";
        }

        UpdateCount(count);
    }

    /// <summary>
    /// 수량을 갱신합니다.
    /// </summary>
    public void UpdateCount(int count)
    {
        _currentCount = count;

        if (_itemCount != null)
        {
            _itemCount.text = count.ToString();
        }

        if (_canvasGroup != null)
        {
            bool hasStock = count > 0;
            _canvasGroup.alpha = hasStock ? 1f : 0.3f;
            _canvasGroup.blocksRaycasts = hasStock;
        }
    }

    /// <summary>
    /// 인벤토리에서 현재 수량을 다시 조회하여 갱신합니다.
    /// </summary>
    public void RefreshCount()
    {
        if (_fuelData == null || InventoryManager.Instance == null) return;
        int count = InventoryManager.Instance.GetItemCount(_fuelData);
        UpdateCount(count);
    }

    public IngredientData FuelData => _fuelData;

    // ── 드래그 핸들러 ───────────────────────────────────────

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_fuelData == null || _currentCount <= 0) return;

        _isDragging = true;

        if (DragManager.Instance != null)
        {
            DragManager.Instance.StartDrag(_fuelData, _currentCount, null, _fuelData.Icon);
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
            DragManager.Instance.EndDrag();
        }
    }
}
