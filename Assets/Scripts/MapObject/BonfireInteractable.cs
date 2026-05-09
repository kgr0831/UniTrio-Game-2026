using System;
using UnityEngine;

/// <summary>
/// Bonfire 건축물 프리팹에 부착. IInteractable을 구현하여
/// 플레이어가 접근 시 조리 UI를 열 수 있게 합니다.
/// 
/// 조리 상태(입력/출력 아이템, 타이머)는 이 인스턴스에 영속적으로 보관되므로
/// UI를 닫았다가 다시 열어도 조리 상태가 유지됩니다.
/// 
/// SRP: 이 클래스는 조리 데이터/로직만 담당. UI 렌더링은 BonfireUIPanel이 담당.
/// 
/// 프리팹에 BoxCollider2D(IsTrigger=true)가 필요합니다.
/// BuildingEntity가 이미 RequireComponent로 BoxCollider2D를 보장하므로 여기서는 별도 선언하지 않습니다.
/// </summary>
public class BonfireInteractable : MonoBehaviour, IInteractable
{
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

    // ── 입력 상태 (인스턴스별 영속 데이터) ───────────────────
    // 전체 스택을 보관하여 하나씩 자동 조리합니다.

    /// <summary>입력 슬롯에 대기 중인 음식 데이터</summary>
    public ConsumableData InputFood { get; private set; }

    /// <summary>입력 슬롯에 남은 수량</summary>
    public int InputCount { get; private set; }

    // ── 출력 상태 ────────────────────────────────────────────

    /// <summary>현재 출력 슬롯에 보관된 아이템 데이터</summary>
    public ItemData OutputItem { get; private set; }

    /// <summary>출력 슬롯 아이템 수량</summary>
    public int OutputCount { get; private set; }

    // ── 조리 진행 상태 ──────────────────────────────────────

    /// <summary>현재 조리 중인지 여부</summary>
    public bool IsCooking { get; private set; }

    /// <summary>조리 진행 시간(초)</summary>
    public float CookElapsed { get; private set; }

    /// <summary>현재 조리 중인 음식의 총 조리 시간</summary>
    public float CookDuration { get; private set; }

    /// <summary>현재 조리 중인 원재료 데이터</summary>
    public ConsumableData CookingSourceFood { get; private set; }

    /// <summary>조리 진행률 (0~1)</summary>
    public float CookProgress => CookDuration > 0f ? Mathf.Clamp01(CookElapsed / CookDuration) : 0f;

    // ── 이벤트 (UI 갱신용 — 데이터-렌더링 분리 원칙) ────────
    public event Action OnCookingStateChanged;
    public event Action OnCookingCompleted;

    // ── 조리 로직 ───────────────────────────────────────────

    /// <summary>
    /// 입력 슬롯에 음식 스택을 넣어 자동 연속 조리를 시작합니다.
    /// 1개씩 소모하며 조리가 끝나면 다음 아이템을 자동으로 조리합니다.
    /// </summary>
    /// <param name="food">조리할 음식 데이터 (CookMethod != None 필수)</param>
    /// <param name="count">투입할 수량 (전체 스택)</param>
    /// <returns>조리 시작 성공 여부</returns>
    public bool StartCooking(ConsumableData food, int count)
    {
        if (food == null || food.CookMethod == CookingMethod.None) return false;
        if (food.CookedResult == null) return false;
        if (count <= 0) return false;

        // 이미 조리 중이면 추가 투입 불가
        if (IsCooking) return false;

        // 출력 슬롯에 아이템이 있으면, 동일 레시피의 원재료만 허용
        if (OutputItem != null)
        {
            if (OutputItem != food.CookedResult) return false;
        }

        // 입력 스택 설정
        InputFood = food;
        InputCount = count;

        // 첫 번째 조리 시작
        BeginNextCook();
        return true;
    }

    /// <summary>
    /// 입력 스택에서 1개를 꺼내 조리를 시작합니다.
    /// </summary>
    private void BeginNextCook()
    {
        if (InputFood == null || InputCount <= 0)
        {
            InputFood = null;
            InputCount = 0;
            OnCookingStateChanged?.Invoke();
            return;
        }

        IsCooking = true;
        CookElapsed = 0f;
        CookDuration = InputFood.CookingTime;
        CookingSourceFood = InputFood;

        OnCookingStateChanged?.Invoke();
    }

    /// <summary>
    /// 출력 슬롯에서 아이템을 꺼냅니다.
    /// </summary>
    public ItemData TakeOutput(out int count)
    {
        ItemData item = OutputItem;
        count = OutputCount;
        OutputItem = null;
        OutputCount = 0;
        OnCookingStateChanged?.Invoke();
        return item;
    }

    /// <summary>
    /// 외부에서 출력 슬롯의 아이템을 직접 설정합니다.
    /// </summary>
    public void SetOutput(ItemData item, int count)
    {
        OutputItem = item;
        OutputCount = count;
        OnCookingStateChanged?.Invoke();
    }

    /// <summary>
    /// 입력 슬롯의 남은 아이템을 반환하고 비웁니다. (패널 닫힘 시 인벤토리 복구용)
    /// </summary>
    public ConsumableData TakeInput(out int count)
    {
        ConsumableData food = InputFood;
        count = InputCount;
        InputFood = null;
        InputCount = 0;
        return food;
    }

    private void Update()
    {
        if (!IsCooking) return;

        // unscaledDeltaTime 사용: UI가 열려 timeScale=0 이어도 조리 진행
        CookElapsed += Time.unscaledDeltaTime;
        OnCookingStateChanged?.Invoke();

        if (CookElapsed >= CookDuration)
        {
            CompleteCooking();
        }
    }

    private void CompleteCooking()
    {
        if (CookingSourceFood == null || CookingSourceFood.CookedResult == null)
        {
            IsCooking = false;
            CookingSourceFood = null;
            OnCookingStateChanged?.Invoke();
            return;
        }

        // 출력 슬롯에 같은 아이템이 있으면 스태킹, 없으면 새로 할당
        if (OutputItem == CookingSourceFood.CookedResult)
        {
            OutputCount += 1;
        }
        else
        {
            OutputItem = CookingSourceFood.CookedResult;
            OutputCount = 1;
        }

        // 입력 스택에서 1개 소모
        InputCount -= 1;
        if (InputCount <= 0)
        {
            InputFood = null;
            InputCount = 0;
        }

        IsCooking = false;
        CookElapsed = 0f;
        CookingSourceFood = null;

        OnCookingStateChanged?.Invoke();
        OnCookingCompleted?.Invoke();

        Debug.Log($"[Bonfire] 조리 완료! 결과: {OutputItem.Name} x{OutputCount}, 입력 잔여: {InputCount}");

        // 입력 스택에 남은 아이템이 있으면 자동으로 다음 조리 시작
        if (InputCount > 0)
        {
            BeginNextCook();
        }
    }
}
