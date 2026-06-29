using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인벤토리/제작 패널이 열렸을 때 동적으로 레시피 UI를 생성하고,
/// 재료의 유효성을 검사하여 제작 트랜잭션을 처리하는 매니저 클래스.
/// ScrollRect 레이아웃과 완벽히 호환되도록 구성.
/// </summary>
public class CraftingUIManager : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("RecipeUI 프리팹. ScrollRect의 Content 아래에 동적으로 생성됨")]
    [SerializeField] private GameObject _recipeUIPrefab;
    [Tooltip("프리팹들이 생성될 부모 컨테이너 (예: ScrollRect의 Content)")]
    [SerializeField] private Transform _contentParent;

    [Header("Data")]
    [Tooltip("게임 내 모든 크래프팅 레시피 목록")]
    [SerializeField] private CraftingRecipeSO[] _recipes;

    // 생성된 UI 인스턴스들을 관리하기 위한 리스트
    private List<RecipeUI> _activeUIs = new List<RecipeUI>();

    public event System.Action<CraftingRecipeSO> OnItemCrafted;
    public event System.Action OnPanelOpened;

    private void Awake()
    {
        // ItemDatabase 로드가 더 이상 필요 없습니다.
    }

    private void OnEnable()
    {
        // UI가 열릴 때마다 목록을 갱신
        RefreshUI();
        Core.ItemEvents.OnInventoryChanged += OnInventoryChanged;
        OnPanelOpened?.Invoke();
    }

    private void OnDisable()
    {
        Core.ItemEvents.OnInventoryChanged -= OnInventoryChanged;
    }

    private void OnInventoryChanged()
    {
        // 인벤토리 내용물(재료)이 변동되었으므로 동적으로 UI를 재검증합니다.
        ValidateAllRecipes();
    }

    /// <summary>
    /// 레시피 UI들을 동적으로 생성하고, 현재 인벤토리 상태를 바탕으로 검증합니다.
    /// </summary>
    public void RefreshUI()
    {
        if (_recipeUIPrefab == null || _contentParent == null) return;

        // 기존 생성된 UI가 있다면 제거
        foreach (Transform child in _contentParent)
        {
            Destroy(child.gameObject);
        }
        _activeUIs.Clear();

        if (_recipes == null) return;

        foreach (var recipe in _recipes)
        {
            if (recipe == null) continue;

            // 스케일 및 레이아웃이 망가지지 않도록 SetParent(parent, false) 사용!
            GameObject go = Instantiate(_recipeUIPrefab);
            go.transform.SetParent(_contentParent, false);

            RecipeUI ui = go.GetComponent<RecipeUI>();
            if (ui != null)
            {
                // UI 셋업 및 람다(Lambda)로 클릭 이벤트(OnCraftClicked)를 넘김
                ui.Setup(recipe, OnCraftClicked);
                _activeUIs.Add(ui);
            }
        }

        // 각 UI의 조건 충족 여부 검증
        ValidateAllRecipes();
    }

    /// <summary>
    /// 모든 레시피의 재료 유효성을 재검사합니다. 인벤토리에 변화가 생길 때 호출해야 함.
    /// </summary>
    public void ValidateAllRecipes()
    {
        for (int i = 0; i < _activeUIs.Count; i++)
        {
            var recipe = _recipes[i];
            bool canCraft = true;
            string description = "";

            // 각 재료별로 인벤토리 수량 검사
            foreach (var req in recipe.RequiredIngredients)
            {
                if (req.Item == null) continue;

                int currentCount = InventoryManager.Instance != null ? InventoryManager.Instance.GetItemCount(req.Item) : 0;
                string itemName = req.Item.Name;

                // 텍스트 구성: 예) "나무: 5/10\n돌: 3/2"
                description += $"{itemName} : {currentCount} / {req.Count}\n";

                // 하나라도 모자라면 제작 불가
                if (currentCount < req.Count)
                {
                    canCraft = false;
                }
            }

            _activeUIs[i].UpdateValidation(canCraft, description.TrimEnd());
        }
    }

    /// <summary>
    /// 제작 버튼 클릭 시 처리되는 트랜잭션 로직
    /// </summary>
    private void OnCraftClicked(CraftingRecipeSO recipe)
    {
        if (InventoryManager.Instance == null) return;

        // 1. 재검증 (Double-check): 다중 클릭 등 비정상 상황 방지
        foreach (var req in recipe.RequiredIngredients)
        {
            if (req.Item == null) continue;
            if (InventoryManager.Instance.GetItemCount(req.Item) < req.Count)
            {
                Debug.LogWarning("[Crafting] 재료가 부족하여 제작이 취소되었습니다.");
                return;
            }
        }

        // 2. 재료 차감 (트랜잭션)
        foreach (var req in recipe.RequiredIngredients)
        {
            if (req.Item == null) continue;
            bool success = InventoryManager.Instance.ConsumeItems(req.Item, req.Count);
            if (!success)
            {
                Debug.LogError($"[Crafting] 치명적 오류: 재료 차감 중 문제가 발생했습니다! (Item: {req.Item.Name})");
                return; // 롤백 처리를 구현하려면 여기에 추가해야 하나, 단일 스레드 구조상 위 검증을 통과했다면 발생 가능성 낮음.
            }
        }

        // 3. 결과물 획득
        InventoryManager.Instance.AddItem(recipe.ResultItem, recipe.ResultCount);
        Debug.Log($"[Crafting] 제작 성공! 획득: {recipe.ResultItem.Name} x{recipe.ResultCount}");

        // 4. 퀘스트 시스템에 제작 보고
        QuestEventBridge.ReportItemCrafted(recipe.ResultItem.Name);

        // 5. 인벤토리 상태가 변했으므로 UI 상태 갱신
        ValidateAllRecipes();

        // 6. 제작 완료 이벤트 호출
        OnItemCrafted?.Invoke(recipe);
    }

    /// <summary>결과 아이템이 resultItem인 레시피 UI 슬롯의 RectTransform을 반환합니다. 패널이 닫혀 있으면 null.</summary>
    public RectTransform GetRecipeSlotRectByResult(ItemData resultItem)
    {
        if (resultItem == null || _recipes == null) return null;
        for (int i = 0; i < _activeUIs.Count && i < _recipes.Length; i++)
        {
            if (_recipes[i] != null && _recipes[i].ResultItem == resultItem)
                return _activeUIs[i] != null ? _activeUIs[i].GetComponent<RectTransform>() : null;
        }
        return null;
    }
}
