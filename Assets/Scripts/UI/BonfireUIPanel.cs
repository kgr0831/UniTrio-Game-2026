using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bonfire 조리 UI 패널.
/// 인벤토리 패널과 함께 열려서 음식 드래그 드롭 → 조리 → 결과 회수 플로우를 제공합니다.
///
/// 슬롯 2개 + 프로그레스 바로 구성:
///   - Input Slot: 조리할 음식을 넣는 곳 (드래그 드롭으로 인벤토리에서)
///   - Output Slot: 조리 완료된 음식이 나오는 곳 (드래그 드롭으로 인벤토리로)
///   - Progress Bar: 조리 진행 상태 시각화
///
/// 렌더링 전용: 데이터/로직은 BonfireInteractable이 담당 (SRP)
/// </summary>
public class BonfireUIPanel : MonoBehaviour
{
    public static BonfireUIPanel Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject _panelRoot;

    [Tooltip("입력 슬롯 (조리할 음식을 넣는 곳) — SlotType을 BonfireInput으로 설정")]
    [SerializeField] private InventorySlot _inputSlot;

    [Tooltip("출력 슬롯 (조리 완료된 음식) — SlotType을 BonfireOutput으로 설정")]
    [SerializeField] private InventorySlot _outputSlot;

    [Header("Progress Bar")]
    [Tooltip("조리 진행 바 (Filled Image, FillAmount로 제어)")]
    [SerializeField] private Image _progressBarFill;

    [Tooltip("프로그레스 바 배경 (조리 중이 아닐 때 숨김)")]
    [SerializeField] private GameObject _progressBarRoot;

    // 현재 바인딩된 Bonfire
    private BonfireInteractable _currentBonfire;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (_panelRoot != null) _panelRoot.SetActive(false);
    }

    /// <summary>
    /// 특정 Bonfire와 연결하여 UI를 엽니다.
    /// </summary>
    public void Open(BonfireInteractable bonfire)
    {
        if (bonfire == null) return;

        _currentBonfire = bonfire;

        // Bonfire 이벤트 구독 (데이터 변경 → UI 갱신)
        _currentBonfire.OnCookingStateChanged += RefreshUI;
        _currentBonfire.OnCookingCompleted += OnCookingCompleted;

        if (_panelRoot != null) _panelRoot.SetActive(true);

        if (InventoryToggle.Instance != null)
        {
            InventoryToggle.Instance.OpenBonfirePanel();
        }

        RefreshUI();
        Debug.Log("[BonfireUI] 패널 열림");
    }

    /// <summary>
    /// UI를 닫고 Bonfire와의 바인딩을 해제합니다.
    /// 입력 슬롯에 남은 재료와 UI 슬롯의 잔여 아이템은 인벤토리로 반환합니다.
    /// </summary>
    public void Close()
    {
        // Bonfire 인스턴스의 입력 스택에 남은 재료를 인벤토리로 반환
        ReturnBonfireInputToInventory();

        // UI 입력 슬롯에 남은 아이템도 인벤토리로 반환
        ReturnInputSlotToInventory();

        if (_currentBonfire != null)
        {
            _currentBonfire.OnCookingStateChanged -= RefreshUI;
            _currentBonfire.OnCookingCompleted -= OnCookingCompleted;
            _currentBonfire = null;
        }

        if (_panelRoot != null) _panelRoot.SetActive(false);

        if (_inputSlot != null) _inputSlot.RefreshSlot(null, 0);
        if (_outputSlot != null) _outputSlot.RefreshSlot(null, 0);

        Debug.Log("[BonfireUI] 패널 닫힘");
    }

    /// <summary>
    /// 현재 Bonfire 상태에 따라 UI를 갱신합니다. (이벤트 콜백)
    /// </summary>
    private void RefreshUI()
    {
        if (_currentBonfire == null) return;

        // 입력 슬롯 갱신 — Bonfire 인스턴스의 입력 스택 반영
        if (_inputSlot != null)
        {
            _inputSlot.RefreshSlot(_currentBonfire.InputFood, _currentBonfire.InputCount);
        }

        // 출력 슬롯 갱신 — Bonfire 인스턴스의 영속 데이터 반영
        if (_outputSlot != null)
        {
            _outputSlot.RefreshSlot(_currentBonfire.OutputItem, _currentBonfire.OutputCount);
        }

        UpdateProgressBar();
    }

    private void UpdateProgressBar()
    {
        if (_progressBarRoot == null || _progressBarFill == null) return;

        if (_currentBonfire != null && _currentBonfire.IsCooking)
        {
            _progressBarRoot.SetActive(true);
            _progressBarFill.fillAmount = _currentBonfire.CookProgress;
        }
        else
        {
            _progressBarRoot.SetActive(false);
            _progressBarFill.fillAmount = 0f;
        }
    }

    private void OnCookingCompleted()
    {
        Debug.Log("[BonfireUI] 조리 완료 알림 수신");
    }

    /// <summary>
    /// 입력 슬롯에 아이템이 드래그&드롭으로 배치되었을 때 호출됩니다.
    /// 전체 스택을 Bonfire에 전달하여 연속 조리를 시작합니다.
    /// </summary>
    public void OnInputSlotItemPlaced(ConsumableData food, int count)
    {
        if (_currentBonfire == null || food == null) return;

        bool started = _currentBonfire.StartCooking(food, count);

        if (started)
        {
            // 입력 슬롯은 Bonfire의 상태를 반영하므로 RefreshUI가 자동 처리
            Debug.Log($"[BonfireUI] 조리 시작: {food.Name} x{count}");
        }
        else
        {
            // 조리 시작 실패 — 아이템을 인벤토리로 되돌림
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.AddItem(food, count);
            }
            if (_inputSlot != null) _inputSlot.RefreshSlot(null, 0);

            if (NotificationUI.Instance != null)
            {
                NotificationUI.Instance.ShowMessage("지금은 조리할 수 없습니다!");
            }
        }
    }

    /// <summary>
    /// 출력 슬롯에서 아이템을 꺼낼 때 Bonfire 데이터도 동기화합니다.
    /// </summary>
    public void OnOutputSlotItemTaken()
    {
        if (_currentBonfire == null) return;
        _currentBonfire.SetOutput(null, 0);
    }

    /// <summary>
    /// 현재 바인딩된 Bonfire를 반환합니다.
    /// </summary>
    public BonfireInteractable GetCurrentBonfire()
    {
        return _currentBonfire;
    }

    /// <summary>
    /// Bonfire 인스턴스의 입력 스택에 남은 재료를 인벤토리로 반환합니다.
    /// (조리 중이 아닌 대기 중인 재료만 반환)
    /// </summary>
    private void ReturnBonfireInputToInventory()
    {
        if (_currentBonfire == null) return;

        int inputRemaining;
        ConsumableData remainingFood = _currentBonfire.TakeInput(out inputRemaining);

        if (remainingFood != null && inputRemaining > 0 && InventoryManager.Instance != null)
        {
            InventoryManager.Instance.AddItem(remainingFood, inputRemaining);
        }
    }

    /// <summary>
    /// UI 입력 슬롯에 직접 남아있는 아이템을 인벤토리로 반환합니다.
    /// </summary>
    private void ReturnInputSlotToInventory()
    {
        if (_inputSlot == null || _inputSlot.currentData == null) return;

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.AddItem(_inputSlot.currentData, _inputSlot.currentCount);
        }
        _inputSlot.RefreshSlot(null, 0);
    }

    public bool IsOpen()
    {
        return _panelRoot != null && _panelRoot.activeSelf;
    }
}
