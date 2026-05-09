using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Bonfire 인풋 슬롯 UI.
/// BonfireIngredientEntry에서 드래그된 아이템을 수신(IDropHandler)하여 조리를 시작합니다.
///
/// 드롭 로직:
///   - 빈 슬롯: 연료 1개 + 재료 1개 소모 → 조리 시작
///   - 같은 음식 조리 중: 재료 1개만 소모 → 대기열 추가 (카운트 +1)
///   - 다른 음식 조리 중: 거부
///
/// 카운트 텍스트: 대기열 수량을 표시합니다.
/// </summary>
public class BonfireInputSlotUI : MonoBehaviour, IDropHandler, IPointerClickHandler
{
    [Header("UI References")]
    [SerializeField] private Image _slotIcon;
    [SerializeField] private Text _countText;         // 대기열 카운트 표시
    [SerializeField] private GameObject _emptyState;   // 비어있을 때 표시할 UI (선택)

    // 이 슬롯의 인덱스 (0~4). BonfireUIPanel에서 설정.
    private int _slotIndex;
    private BonfireInteractable _bonfire;

    /// <summary>
    /// 슬롯을 초기화합니다.
    /// </summary>
    public void Setup(int index, BonfireInteractable bonfire)
    {
        _slotIndex = index;
        _bonfire = bonfire;
        RefreshDisplay();
    }

    // 캐싱: 아이콘의 원래 색상 (Awake 시점에 저장)
    private Color _iconOriginalColor = Color.white;

    private void Awake()
    {
        // 슬롯 아이콘의 원래 색상을 캐싱 (Inspector에서 설정한 색상 유지)
        if (_slotIcon != null)
        {
            _iconOriginalColor = _slotIcon.color;
        }
    }

    /// <summary>
    /// 현재 Bonfire 슬롯 상태에 따라 UI를 갱신합니다.
    /// Image.enabled는 건드리지 않고 sprite + color alpha로 제어하여
    /// 슬롯 영역(배경)이 항상 보이도록 합니다.
    /// </summary>
    public void RefreshDisplay()
    {
        if (_bonfire == null) return;

        BonfireInteractable.CookSlot slot = _bonfire.GetSlot(_slotIndex);

        if (slot.IsActive && slot.SourceFood != null)
        {
            // 조리 중 또는 대기열 존재: 원재료 아이콘 표시
            if (_slotIcon != null)
            {
                _slotIcon.sprite = slot.SourceFood.Icon;
                _slotIcon.color = _iconOriginalColor; // 원래 색상 복원 (불투명)
            }

            // 대기열 카운트 표시
            if (_countText != null)
            {
                if (slot.QueuedCount > 0)
                {
                    _countText.text = slot.QueuedCount.ToString();
                    _countText.enabled = true;
                }
                else
                {
                    _countText.enabled = false;
                }
            }

            if (_emptyState != null) _emptyState.SetActive(false);
        }
        else
        {
            // 비어있음 — Image를 비활성화하지 않고 아이콘만 투명 처리
            if (_slotIcon != null)
            {
                _slotIcon.sprite = null;
                _slotIcon.color = new Color(_iconOriginalColor.r, _iconOriginalColor.g, _iconOriginalColor.b, 0f);
            }
            if (_countText != null) _countText.enabled = false;
            if (_emptyState != null) _emptyState.SetActive(true);
        }
    }

    // ── IDropHandler ────────────────────────────────────────

    public void OnDrop(PointerEventData eventData)
    {
        if (_bonfire == null) return;

        DragManager dm = DragManager.Instance;
        if (dm == null || dm.draggingData == null) return;

        // ConsumableData인지 확인
        ConsumableData food = dm.draggingData as ConsumableData;
        if (food == null || food.CookMethod != CookingMethod.Bonfire)
        {
            if (NotificationUI.Instance != null)
                NotificationUI.Instance.ShowMessage("이 아이템은 조리할 수 없습니다!");
            return;
        }

        BonfireInteractable.CookSlot currentSlot = _bonfire.GetSlot(_slotIndex);

        // 다른 음식이 조리 중이면 거부
        if (currentSlot.IsActive && currentSlot.SourceFood != food)
        {
            if (NotificationUI.Instance != null)
                NotificationUI.Instance.ShowMessage("다른 음식이 이미 조리 중입니다!");
            return;
        }

        // TryAddFood: 빈 슬롯이면 즉시 조리, 같은 음식이면 대기열 추가
        bool success = _bonfire.TryAddFood(_slotIndex, food);

        if (success)
        {
            dm.dragConsumed = true;
            // 재료 목록 수량 갱신
            Core.ItemEvents.TriggerInventoryChanged();
        }
        else
        {
            if (NotificationUI.Instance != null)
                NotificationUI.Instance.ShowMessage("연료가 부족하거나 재료가 없습니다!");
        }
    }

    // ── IPointerClickHandler ────────────────────────────────

    public void OnPointerClick(PointerEventData eventData)
    {
        // 향후 확장: 대기열 취소 등
    }
}
