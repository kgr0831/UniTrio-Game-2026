using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Bonfire 조리 UI 패널.
/// 인벤토리 없이 독립적으로 동작합니다.
///
/// 구성:
///   - 재료 스크롤뷰: 인벤토리에서 Bonfire 조리 가능한 아이템 + 연료 아이템을 표시
///   - 연료 슬롯: 연료 아이템 드래그&드롭으로 투입
///   - 인풋 슬롯 5개: 재료 드래그&드롭으로 투입 → 조리 시작
///   - 아웃풋 칸 5개: 조리 진행 상태 표시 (이미지+이름+프로그레스바)
///   - 조리 완료 시 자동으로 인벤토리에 추가
///
/// SRP: 이 클래스는 UI 렌더링/이벤트 바인딩만 담당.
///      데이터/로직은 BonfireInteractable이 담당.
/// </summary>
public class BonfireUIPanel : MonoBehaviour
{
    public static BonfireUIPanel Instance { get; private set; }

    [Header("Panel")]
    [SerializeField] private GameObject _panelRoot;

    [Header("Ingredient ScrollView")]
    [Tooltip("스크롤뷰 Content (Vertical Layout Group 부착)")]
    [SerializeField] private Transform _ingredientScrollContent;

    [Tooltip("재료/연료 엔트리 프리팹 (BonfireIngredientEntry 부착)")]
    [SerializeField] private GameObject _ingredientEntryPrefab;

    [Header("Fuel Slot")]
    [SerializeField] private BonfireFuelSlotUI _fuelSlot;

    [Header("Input Slots (5개)")]
    [SerializeField] private BonfireInputSlotUI[] _inputSlots = new BonfireInputSlotUI[BonfireInteractable.SLOT_COUNT];

    [Header("Output Displays (5개)")]
    [SerializeField] private BonfireOutputDisplayUI[] _outputDisplays = new BonfireOutputDisplayUI[BonfireInteractable.SLOT_COUNT];

    // 현재 바인딩된 Bonfire
    private BonfireInteractable _currentBonfire;

    // 생성된 재료 엔트리 캐시 (Destroy 대신 재사용)
    private readonly List<BonfireIngredientEntry> _ingredientEntries = new List<BonfireIngredientEntry>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (_panelRoot != null) _panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        // 인벤토리 변경 시 재료 목록 수량 갱신
        Core.ItemEvents.OnInventoryChanged += RefreshIngredientCounts;
    }

    private void OnDisable()
    {
        Core.ItemEvents.OnInventoryChanged -= RefreshIngredientCounts;
    }

    // ── 패널 열기/닫기 ──────────────────────────────────────

    /// <summary>
    /// 특정 Bonfire와 연결하여 UI를 엽니다. (인벤토리 패널 미표시)
    /// </summary>
    public void Open(BonfireInteractable bonfire)
    {
        if (bonfire == null) return;

        _currentBonfire = bonfire;

        // Bonfire 이벤트 구독
        _currentBonfire.OnSlotStateChanged += OnSlotStateChanged;
        _currentBonfire.OnFuelChanged += OnFuelChanged;
        _currentBonfire.OnCookingCompleted += OnCookingCompleted;

        if (_panelRoot != null) _panelRoot.SetActive(true);

        // InventoryToggle을 통해 Bonfire 패널만 열기
        if (InventoryToggle.Instance != null)
        {
            InventoryToggle.Instance.OpenBonfirePanel();
        }

        // UI 초기화
        SetupSlots();
        PopulateIngredientList();
        RefreshAllUI();

        Debug.Log("[BonfireUI] 패널 열림");
    }

    /// <summary>
    /// UI를 닫고 Bonfire와의 바인딩을 해제합니다.
    /// </summary>
    public void Close()
    {
        if (_currentBonfire != null)
        {
            _currentBonfire.OnSlotStateChanged -= OnSlotStateChanged;
            _currentBonfire.OnFuelChanged -= OnFuelChanged;
            _currentBonfire.OnCookingCompleted -= OnCookingCompleted;

            // 연료 반환
            ReturnFuelToInventory();

            // 모든 슬롯의 대기열 아이템도 인벤토리로 반환
            for (int i = 0; i < BonfireInteractable.SLOT_COUNT; i++)
            {
                _currentBonfire.ReturnQueuedItems(i);
            }

            _currentBonfire = null;
        }

        // 재료 엔트리 정리
        ClearIngredientList();

        if (_panelRoot != null) _panelRoot.SetActive(false);

        Debug.Log("[BonfireUI] 패널 닫힘");
    }

    public bool IsOpen()
    {
        return _panelRoot != null && _panelRoot.activeSelf;
    }

    public BonfireInteractable GetCurrentBonfire()
    {
        return _currentBonfire;
    }

    // ── 슬롯 초기화 ────────────────────────────────────────

    private void SetupSlots()
    {
        // 인풋 슬롯 초기화
        for (int i = 0; i < BonfireInteractable.SLOT_COUNT; i++)
        {
            if (i < _inputSlots.Length && _inputSlots[i] != null)
            {
                _inputSlots[i].Setup(i, _currentBonfire);
            }

            if (i < _outputDisplays.Length && _outputDisplays[i] != null)
            {
                _outputDisplays[i].Setup(i);
            }
        }

        // 연료 슬롯 초기화
        if (_fuelSlot != null)
        {
            _fuelSlot.Setup(_currentBonfire);
        }
    }

    // ── 재료 스크롤뷰 ──────────────────────────────────────

    /// <summary>
    /// 인벤토리를 스캔하여 Bonfire 조리 가능한 아이템 + 연료 아이템 목록을 생성합니다.
    /// </summary>
    private void PopulateIngredientList()
    {
        ClearIngredientList();

        if (_ingredientScrollContent == null || _ingredientEntryPrefab == null) return;
        if (InventoryManager.Instance == null) return;

        // 이미 추가한 아이템 추적 (중복 방지)
        HashSet<int> addedItemIds = new HashSet<int>();

        // 조리 가능 아이템 (ConsumableData with CookMethod == Bonfire) 수집
        // + 연료 아이템 (IngredientData with FuelCategory == Bonfire) 수집
        // InventoryManager의 모든 슬롯을 순회
        InventoryGenerator gen = FindInventoryGenerator();
        if (gen == null || gen.AllSlots == null) return;

        // 메인 인벤토리 + 퀵슬롯 순회
        foreach (InventorySlot slot in gen.AllSlots)
        {
            if (slot.currentData == null) continue;

            TryAddIngredientEntry(slot.currentData, addedItemIds);
        }

        // 퀵슬롯도 체크
        QuickSlotManager qsm = FindObjectOfType<QuickSlotManager>();
        if (qsm != null && qsm.quickSlots != null)
        {
            foreach (InventorySlot slot in qsm.quickSlots)
            {
                if (slot == null || slot.currentData == null) continue;
                TryAddIngredientEntry(slot.currentData, addedItemIds);
            }
        }
    }

    /// <summary>
    /// 아이템이 Bonfire 관련(조리 가능 또는 연료)이면 엔트리를 생성합니다.
    /// </summary>
    private void TryAddIngredientEntry(ItemData data, HashSet<int> addedIds)
    {
        if (addedIds.Contains(data.Id)) return;

        // Bonfire 조리 가능 아이템
        ConsumableData food = data as ConsumableData;
        if (food != null && food.CookMethod == CookingMethod.Bonfire && food.CookedResult != null)
        {
            addedIds.Add(data.Id);
            CreateIngredientEntry(food);
            return;
        }

        // Bonfire 연료 아이템
        IngredientData ingredient = data as IngredientData;
        if (ingredient != null && ingredient.FuelCategory == FuelType.Bonfire)
        {
            addedIds.Add(data.Id);
            CreateFuelEntry(ingredient);
            return;
        }
    }

    private void CreateIngredientEntry(ConsumableData food)
    {
        GameObject go = Instantiate(_ingredientEntryPrefab, _ingredientScrollContent);
        BonfireIngredientEntry entry = go.GetComponent<BonfireIngredientEntry>();
        if (entry != null)
        {
            int count = InventoryManager.Instance != null
                ? InventoryManager.Instance.GetItemCount(food)
                : 0;
            entry.Setup(food, count);
            _ingredientEntries.Add(entry);
        }
    }

    private void CreateFuelEntry(IngredientData fuel)
    {
        // 연료도 같은 프리팹을 사용하여 스크롤뷰에 표시
        // BonfireIngredientEntry는 ConsumableData만 드래그 가능하므로
        // 연료용 별도 엔트리를 생성합니다
        GameObject go = Instantiate(_ingredientEntryPrefab, _ingredientScrollContent);

        // 연료 엔트리에는 BonfireFuelEntry 컴포넌트를 동적으로 추가
        BonfireFuelEntry fuelEntry = go.GetComponent<BonfireFuelEntry>();
        if (fuelEntry == null)
        {
            fuelEntry = go.AddComponent<BonfireFuelEntry>();
        }

        int count = InventoryManager.Instance != null
            ? InventoryManager.Instance.GetItemCount(fuel)
            : 0;
        fuelEntry.Setup(fuel, count, go);
    }

    private void ClearIngredientList()
    {
        for (int i = _ingredientEntries.Count - 1; i >= 0; i--)
        {
            if (_ingredientEntries[i] != null)
            {
                Destroy(_ingredientEntries[i].gameObject);
            }
        }
        _ingredientEntries.Clear();

        // 연료 엔트리도 정리 (Content 자식 전체 정리)
        if (_ingredientScrollContent != null)
        {
            for (int i = _ingredientScrollContent.childCount - 1; i >= 0; i--)
            {
                Destroy(_ingredientScrollContent.GetChild(i).gameObject);
            }
        }
    }

    // ── 이벤트 콜백 ────────────────────────────────────────

    private void OnSlotStateChanged(int slotIndex)
    {
        RefreshSlotUI(slotIndex);
    }

    private void OnFuelChanged()
    {
        if (_fuelSlot != null)
        {
            _fuelSlot.RefreshDisplay();
        }
    }

    private void OnCookingCompleted(int slotIndex, ItemData resultItem)
    {
        if (resultItem != null)
        {
            if (NotificationUI.Instance != null)
            {
                NotificationUI.Instance.ShowMessage($"{resultItem.Name} 조리 완료!");
            }
        }

        // 재료 목록 수량 갱신
        RefreshIngredientCounts();
    }

    // ── UI 갱신 ─────────────────────────────────────────────

    private void RefreshAllUI()
    {
        for (int i = 0; i < BonfireInteractable.SLOT_COUNT; i++)
        {
            RefreshSlotUI(i);
        }
        OnFuelChanged();
    }

    private void RefreshSlotUI(int slotIndex)
    {
        if (_currentBonfire == null) return;

        BonfireInteractable.CookSlot slot = _currentBonfire.GetSlot(slotIndex);

        // 인풋 슬롯 갱신
        if (slotIndex < _inputSlots.Length && _inputSlots[slotIndex] != null)
        {
            _inputSlots[slotIndex].RefreshDisplay();
        }

        // 아웃풋 칸 갱신
        if (slotIndex < _outputDisplays.Length && _outputDisplays[slotIndex] != null)
        {
            _outputDisplays[slotIndex].Refresh(slot);
        }
    }

    /// <summary>
    /// 재료 스크롤뷰의 수량을 갱신합니다. (인벤토리 변경 이벤트 콜백)
    /// </summary>
    private void RefreshIngredientCounts()
    {
        if (InventoryManager.Instance == null) return;

        for (int i = 0; i < _ingredientEntries.Count; i++)
        {
            BonfireIngredientEntry entry = _ingredientEntries[i];
            if (entry == null || entry.FoodData == null) continue;

            int count = InventoryManager.Instance.GetItemCount(entry.FoodData);
            entry.UpdateCount(count);
        }

        // 연료 엔트리도 갱신
        if (_ingredientScrollContent != null)
        {
            for (int i = 0; i < _ingredientScrollContent.childCount; i++)
            {
                BonfireFuelEntry fuelEntry = _ingredientScrollContent.GetChild(i).GetComponent<BonfireFuelEntry>();
                if (fuelEntry != null)
                {
                    fuelEntry.RefreshCount();
                }
            }
        }
    }

    // ── 유틸리티 ────────────────────────────────────────────

    /// <summary>
    /// 연료를 인벤토리로 반환합니다. (패널 닫힘 시)
    /// </summary>
    private void ReturnFuelToInventory()
    {
        if (_currentBonfire == null) return;

        int count;
        IngredientData fuel = _currentBonfire.TakeFuel(out count);

        if (fuel != null && count > 0 && InventoryManager.Instance != null)
        {
            InventoryManager.Instance.AddItem(fuel, count);
        }
    }

    /// <summary>
    /// InventoryGenerator 인스턴스를 찾습니다. (Start 시점에 캐싱하면 좋지만 안전하게 매번 조회)
    /// </summary>
    private InventoryGenerator FindInventoryGenerator()
    {
        // Resources.FindObjectsOfTypeAll로 비활성화된 것도 찾기
        InventoryGenerator[] generators = Resources.FindObjectsOfTypeAll<InventoryGenerator>();
        for (int i = 0; i < generators.Length; i++)
        {
            if (generators[i].gameObject.scene.rootCount > 0)
            {
                return generators[i];
            }
        }
        return null;
    }
}
