using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Bonfire 연료 슬롯 UI.
/// 스크롤뷰의 연료 아이템을 드래그&드롭으로 투입합니다.
/// 클릭 시 연료를 인벤토리로 반환합니다.
/// </summary>
public class BonfireFuelSlotUI : MonoBehaviour, IDropHandler, IPointerClickHandler
{
    [Header("UI References")]
    [SerializeField] private Image _fuelIcon;
    [SerializeField] private Text _fuelCount;
    [SerializeField] private GameObject _emptyState;  // 비어있을 때 표시 (선택)

    private BonfireInteractable _bonfire;

    // 캐싱: 아이콘의 원래 색상
    private Color _iconOriginalColor = Color.white;

    private void Awake()
    {
        if (_fuelIcon != null)
        {
            _iconOriginalColor = _fuelIcon.color;
        }
    }

    /// <summary>
    /// Bonfire 인스턴스를 바인딩합니다.
    /// </summary>
    public void Setup(BonfireInteractable bonfire)
    {
        _bonfire = bonfire;
        RefreshDisplay();
    }

    /// <summary>
    /// 연료 상태에 따라 UI를 갱신합니다.
    /// Image.enabled는 건드리지 않고 sprite + color alpha로 제어합니다.
    /// </summary>
    public void RefreshDisplay()
    {
        if (_bonfire == null) return;

        if (_bonfire.FuelItem != null && _bonfire.FuelCount > 0)
        {
            if (_fuelIcon != null)
            {
                _fuelIcon.sprite = _bonfire.FuelItem.Icon;
                _fuelIcon.color = _iconOriginalColor; // 원래 색상 복원
            }
            if (_fuelCount != null)
            {
                _fuelCount.text = _bonfire.FuelCount.ToString();
                _fuelCount.enabled = true;
            }
            if (_emptyState != null) _emptyState.SetActive(false);
        }
        else
        {
            // 비어있음 — 아이콘만 투명 처리 (슬롯 영역은 유지)
            if (_fuelIcon != null)
            {
                _fuelIcon.sprite = null;
                _fuelIcon.color = new Color(_iconOriginalColor.r, _iconOriginalColor.g, _iconOriginalColor.b, 0f);
            }
            if (_fuelCount != null) _fuelCount.enabled = false;
            if (_emptyState != null) _emptyState.SetActive(true);
        }
    }

    // ── IDropHandler: 연료 투입 ─────────────────────────────

    public void OnDrop(PointerEventData eventData)
    {
        if (_bonfire == null) return;

        DragManager dm = DragManager.Instance;
        if (dm == null || dm.draggingData == null) return;

        // IngredientData + FuelCategory == Bonfire 검증
        IngredientData fuel = dm.draggingData as IngredientData;
        if (fuel == null || fuel.FuelCategory != FuelType.Bonfire)
        {
            if (NotificationUI.Instance != null)
                NotificationUI.Instance.ShowMessage("이 아이템은 연료로 사용할 수 없습니다!");
            return;
        }

        // 인벤토리에서 전체 수량을 가져와서 연료로 투입
        int availableCount = 0;
        if (InventoryManager.Instance != null)
        {
            availableCount = InventoryManager.Instance.GetItemCount(fuel);
        }

        if (availableCount <= 0)
        {
            if (NotificationUI.Instance != null)
                NotificationUI.Instance.ShowMessage("연료가 부족합니다!");
            return;
        }

        // 인벤토리에서 전체 소모 후 Bonfire에 투입
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.ConsumeItems(fuel, availableCount);
        }

        bool success = _bonfire.SetFuel(fuel, availableCount);
        if (success)
        {
            dm.dragConsumed = true;
            Core.ItemEvents.TriggerInventoryChanged();
        }
    }

    // ── IPointerClickHandler: 연료 회수 ─────────────────────

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_bonfire == null) return;
        if (_bonfire.FuelItem == null || _bonfire.FuelCount <= 0) return;

        int count;
        IngredientData fuel = _bonfire.TakeFuel(out count);

        if (fuel != null && count > 0 && InventoryManager.Instance != null)
        {
            InventoryManager.Instance.AddItem(fuel, count);
            Core.ItemEvents.TriggerInventoryChanged();
        }
    }
}
