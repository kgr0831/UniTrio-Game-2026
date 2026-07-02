using System;
using UnityEngine;

/// <summary>
/// Bonfire 건축물 프리팹에 부착. IInteractable을 구현하여
/// 플레이어가 접근 시 조리 UI를 열 수 있게 합니다.
///
/// 5개 조리 슬롯 + 연료 시스템 + 대기열(Queue) 시스템을 지원합니다.
/// 각 슬롯은 독립적으로 조리를 진행하며, 완료 시 자동으로 인벤토리에 추가됩니다.
/// 같은 슬롯에 같은 음식을 여러 번 드롭하면 대기열에 쌓이고 자동 연속 조리됩니다.
///
/// 조리 흐름:
///   1) 첫 드롭: 연료 1개 + 재료 1개 소모 → 즉시 조리 시작
///   2) 추가 드롭(같은 음식): 재료 1개만 소모 → 대기열 +1 (연료는 조리 시작 시 소모)
///   3) 조리 완료 → 결과물 인벤토리 추가 → 대기열에서 1개 꺼내 연료 소모 후 자동 조리
///
/// SRP: 이 클래스는 조리 데이터/로직만 담당. UI 렌더링은 BonfireUIPanel이 담당.
/// </summary>
public class BonfireInteractable : MonoBehaviour, IInteractable
{
    // ── 상수 ────────────────────────────────────────────────
    public const int SLOT_COUNT = 5;

    // ── IInteractable 구현 ──────────────────────────────────
    public string InteractionPrompt => "Press F";

    public bool CanInteract(GameObject player)
    {
        return player != null;
    }

    public void Interact(GameObject player)
    {
        if (BonfireUIPanel.Instance != null)
        {
            BonfireUIPanel.Instance.Open(this);
        }
    }

    // ── 조리 슬롯 데이터 ────────────────────────────────────

    /// <summary>
    /// 개별 조리 슬롯의 상태를 나타내는 구조체.
    /// QueuedCount: 현재 조리 중인 1개를 제외한 대기 중인 아이템 수
    /// </summary>
    [Serializable]
    public struct CookSlot
    {
        public ConsumableData SourceFood;   // 조리 중(또는 대기 중)인 원재료
        public ItemData ResultItem;         // 완성될 아이템
        public float CookDuration;          // 총 조리 시간
        public float CookElapsed;           // 경과 시간
        public bool IsCooking;              // 조리 중 여부
        public int QueuedCount;             // 대기열 수량 (조리 중인 1개 제외)

        /// <summary>조리 진행률 (0~1)</summary>
        public float Progress => CookDuration > 0f ? Mathf.Clamp01(CookElapsed / CookDuration) : 0f;

        /// <summary>슬롯이 활성 상태인지 (조리 중이거나 대기열이 있음)</summary>
        public bool IsActive => IsCooking || QueuedCount > 0;

        /// <summary>슬롯을 초기 상태로 리셋</summary>
        public void Clear()
        {
            SourceFood = null;
            ResultItem = null;
            CookDuration = 0f;
            CookElapsed = 0f;
            IsCooking = false;
            QueuedCount = 0;
        }
    }

    // 5개 슬롯 배열 (인스턴스별 영속 데이터)
    private CookSlot[] _slots = new CookSlot[SLOT_COUNT];

    // I-9 조리 진행 루프음 재생 상태 (엣지 감지용)
    private bool _cookLoopActive;

    // ── 연료 상태 ───────────────────────────────────────────

    /// <summary>현재 장착된 연료 데이터</summary>
    public IngredientData FuelItem { get; private set; }

    /// <summary>연료 잔여 수량</summary>
    public int FuelCount { get; private set; }

    // ── 이벤트 (UI 갱신용 — 데이터-렌더링 분리) ─────────────

    /// <summary>슬롯 상태 변경 시 UI에 알림 (슬롯 인덱스 전달)</summary>
    public event Action<int> OnSlotStateChanged;

    /// <summary>연료 상태 변경 시 UI에 알림</summary>
    public event Action OnFuelChanged;

    /// <summary>조리 완료 시 발생 (슬롯 인덱스, 완성 아이템)</summary>
    public event Action<int, ItemData> OnCookingCompleted;

    // ── 읽기 전용 접근자 ────────────────────────────────────

    /// <summary>특정 슬롯의 현재 상태를 반환합니다.</summary>
    public CookSlot GetSlot(int index)
    {
        if (index < 0 || index >= SLOT_COUNT) return default;
        return _slots[index];
    }

    // ── 연료 관리 ───────────────────────────────────────────

    /// <summary>
    /// 연료를 투입합니다. 같은 종류의 연료만 스태킹 가능합니다.
    /// </summary>
    public bool SetFuel(IngredientData fuel, int count)
    {
        if (fuel == null || fuel.FuelCategory != FuelType.Bonfire) return false;
        if (count <= 0) return false;

        // 이미 다른 종류의 연료가 있으면 거부
        if (FuelItem != null && FuelItem != fuel) return false;

        if (FuelItem == fuel)
        {
            FuelCount += count;
        }
        else
        {
            FuelItem = fuel;
            FuelCount = count;
        }

        OnFuelChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 연료를 제거하고 반환합니다. (패널 닫힘 시 인벤토리 복구용)
    /// </summary>
    public IngredientData TakeFuel(out int count)
    {
        IngredientData fuel = FuelItem;
        count = FuelCount;
        FuelItem = null;
        FuelCount = 0;
        OnFuelChanged?.Invoke();
        return fuel;
    }

    /// <summary>
    /// 연료 1개를 소모합니다.
    /// 소모 전의 연료 참조를 fuelRef에 반환합니다 (실패 시 복구용).
    /// </summary>
    private bool ConsumeFuel(out IngredientData fuelRef)
    {
        fuelRef = FuelItem;
        if (FuelItem == null || FuelCount <= 0)
        {
            fuelRef = null;
            return false;
        }

        FuelCount -= 1;
        if (FuelCount <= 0)
        {
            FuelItem = null;
            FuelCount = 0;
        }
        OnFuelChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 소모했던 연료 1개를 복구합니다. (재료 소모 실패 시 롤백용)
    /// </summary>
    private void RestoreFuel(IngredientData fuelRef)
    {
        if (fuelRef == null) return;

        if (FuelItem == null)
        {
            FuelItem = fuelRef;
            FuelCount = 1;
        }
        else
        {
            FuelCount += 1;
        }
        OnFuelChanged?.Invoke();
    }

    // ── 조리 시작 / 대기열 추가 ─────────────────────────────

    /// <summary>
    /// 특정 슬롯에 음식을 추가합니다.
    /// - 빈 슬롯: 연료 1개 + 재료 1개 소모 → 즉시 조리 시작
    /// - 같은 음식 조리 중: 재료 1개만 소모 → 대기열 +1 (연료는 다음 조리 시작 시 소모)
    /// - 다른 음식 조리 중: 거부
    /// </summary>
    public bool TryAddFood(int slotIndex, ConsumableData food)
    {
        if (slotIndex < 0 || slotIndex >= SLOT_COUNT) return false;
        if (food == null || food.CookMethod != CookingMethod.Bonfire) return false;
        if (food.CookedResult == null) return false;

        CookSlot slot = _slots[slotIndex];

        // ── Case 1: 빈 슬롯 → 연료 + 재료 소모 후 즉시 조리 시작 ──
        if (!slot.IsActive)
        {
            IngredientData fuelRef;
            if (!ConsumeFuel(out fuelRef))
            {
                Debug.Log("[Bonfire] 연료가 부족합니다!");
                return false;
            }

            // 인벤토리에서 재료 1개 소모
            if (InventoryManager.Instance != null)
            {
                if (!InventoryManager.Instance.ConsumeItems(food, 1))
                {
                    // 재료 소모 실패 → 연료 복구
                    RestoreFuel(fuelRef);
                    return false;
                }
            }

            _slots[slotIndex].SourceFood = food;
            _slots[slotIndex].ResultItem = food.CookedResult;
            _slots[slotIndex].CookDuration = food.CookingTime;
            _slots[slotIndex].CookElapsed = 0f;
            _slots[slotIndex].IsCooking = true;
            _slots[slotIndex].QueuedCount = 0;

            OnSlotStateChanged?.Invoke(slotIndex);
            Debug.Log($"[Bonfire] 슬롯 {slotIndex} 조리 시작: {food.Name} → {food.CookedResult.Name}");
            return true;
        }

        // ── Case 2: 같은 음식 조리 중 → 재료만 소모, 대기열 +1 ──
        if (slot.SourceFood == food)
        {
            // 재료만 소모 (연료는 다음 조리 시작 시 소모)
            if (InventoryManager.Instance != null)
            {
                if (!InventoryManager.Instance.ConsumeItems(food, 1))
                {
                    return false;
                }
            }

            _slots[slotIndex].QueuedCount += 1;
            OnSlotStateChanged?.Invoke(slotIndex);
            Debug.Log($"[Bonfire] 슬롯 {slotIndex} 대기열 추가: {food.Name} (대기: {_slots[slotIndex].QueuedCount})");
            return true;
        }

        // ── Case 3: 다른 음식 조리 중 → 거부 ──
        Debug.Log("[Bonfire] 다른 음식이 이미 조리 중입니다!");
        return false;
    }

    /// <summary>
    /// 특정 슬롯의 대기열 아이템을 전부 인벤토리로 반환하고 대기열을 비웁니다.
    /// </summary>
    public void ReturnQueuedItems(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SLOT_COUNT) return;

        CookSlot slot = _slots[slotIndex];
        if (slot.QueuedCount > 0 && slot.SourceFood != null && InventoryManager.Instance != null)
        {
            InventoryManager.Instance.AddItem(slot.SourceFood, slot.QueuedCount);
            _slots[slotIndex].QueuedCount = 0;
            OnSlotStateChanged?.Invoke(slotIndex);
        }
    }

    // ── Update: 병렬 조리 타이머 ────────────────────────────

    private void Update()
    {
        for (int i = 0; i < SLOT_COUNT; i++)
        {
            if (!_slots[i].IsCooking) continue;

            // unscaledDeltaTime: timeScale=0 (UI 열림) 에서도 조리 진행
            _slots[i].CookElapsed += Time.unscaledDeltaTime;
            OnSlotStateChanged?.Invoke(i);

            if (_slots[i].CookElapsed >= _slots[i].CookDuration)
            {
                CompleteCooking(i);
            }
        }

        // I-9 조리 진행 루프음: 조리 중 슬롯 유무 엣지에서 start/stop
        UpdateCookingLoop();
    }

    /// <summary>조리 중인 슬롯 유무를 감지해 진행 루프음을 켜고 끕니다.</summary>
    private void UpdateCookingLoop()
    {
        bool anyCooking = false;
        for (int i = 0; i < SLOT_COUNT; i++)
        {
            if (_slots[i].IsCooking) { anyCooking = true; break; }
        }

        if (anyCooking == _cookLoopActive) return; // 상태 변화 없음
        _cookLoopActive = anyCooking;

        if (AudioManager.Instance == null) return;
        if (anyCooking) AudioManager.Instance.StartCookingLoop();
        else            AudioManager.Instance.StopCookingLoop();
    }

    private void OnDisable()
    {
        // 씬 전환/비활성화 시 조리 루프음이 남지 않도록 정지
        if (_cookLoopActive && AudioManager.Instance != null)
            AudioManager.Instance.StopCookingLoop();
        _cookLoopActive = false;
    }

    private void CompleteCooking(int slotIndex)
    {
        ItemData result = _slots[slotIndex].ResultItem;
        ConsumableData sourceFood = _slots[slotIndex].SourceFood;

        // 완성 아이템을 자동으로 인벤토리에 추가
        if (result != null && InventoryManager.Instance != null)
        {
            InventoryManager.Instance.AddItem(result, 1);
            Debug.Log($"[Bonfire] 슬롯 {slotIndex} 조리 완료! {result.Name}이(가) 인벤토리에 추가되었습니다.");
        }

        OnCookingCompleted?.Invoke(slotIndex, result);

        // I-10 모닥불 요리 완료음
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayCookComplete();

        // ── 대기열 처리 ──
        if (_slots[slotIndex].QueuedCount > 0)
        {
            // 대기열에서 1개 꺼내서 다음 조리 시작 (연료 소모)
            IngredientData fuelRef;
            if (ConsumeFuel(out fuelRef))
            {
                _slots[slotIndex].QueuedCount -= 1;
                _slots[slotIndex].CookElapsed = 0f;
                // IsCooking은 true 유지, SourceFood/ResultItem/CookDuration 동일
                OnSlotStateChanged?.Invoke(slotIndex);
                Debug.Log($"[Bonfire] 슬롯 {slotIndex} 자동 연속 조리 시작 (대기: {_slots[slotIndex].QueuedCount})");
            }
            else
            {
                // 연료 부족 → 대기열 아이템을 인벤토리로 반환
                Debug.Log($"[Bonfire] 슬롯 {slotIndex} 연료 부족! 대기열 아이템 {_slots[slotIndex].QueuedCount}개 인벤토리 반환.");
                ReturnQueuedItems(slotIndex);
                _slots[slotIndex].Clear();
                OnSlotStateChanged?.Invoke(slotIndex);
            }
        }
        else
        {
            // 대기열 없음 → 슬롯 완전 정리
            _slots[slotIndex].Clear();
            OnSlotStateChanged?.Invoke(slotIndex);
        }
    }
}
