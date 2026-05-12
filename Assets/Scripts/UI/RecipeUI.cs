using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 크래프팅 레시피 UI 프리팹에 부착되어 개별 레시피의 시각적 요소를 제어합니다.
/// </summary>
public class RecipeUI : MonoBehaviour
{
    [SerializeField] private Image _resultIcon;
    [SerializeField] private TextMeshProUGUI _resultNameText;
    [SerializeField] private TextMeshProUGUI _ingredientsText;
    [SerializeField] private Button _craftButton;

    private CraftingRecipeSO _currentRecipe;
    private System.Action<CraftingRecipeSO> _onCraftButtonClicked;

    private void Awake()
    {
        if (_craftButton != null)
        {
            _craftButton.onClick.AddListener(OnCraftClick);
        }
    }

    /// <summary>
    /// UI 초기화 및 레시피 할당
    /// </summary>
    public void Setup(CraftingRecipeSO recipe, System.Action<CraftingRecipeSO> onCraftAction)
    {
        _currentRecipe = recipe;
        _onCraftButtonClicked = onCraftAction;

        if (_currentRecipe != null && _currentRecipe.ResultItem != null)
        {
            if (_resultIcon != null) _resultIcon.sprite = _currentRecipe.ResultItem.Icon;
            if (_resultNameText != null) _resultNameText.text = $"{_currentRecipe.ResultItem.Name} x{_currentRecipe.ResultCount}";
        }
    }

    /// <summary>
    /// 인벤토리 스캔 결과를 바탕으로 현재 레시피의 재료가 충분한지 텍스트 갱신 및 버튼 활성화 처리
    /// </summary>
    public void UpdateValidation(bool canCraft, string ingredientDescription)
    {
        if (_ingredientsText != null)
        {
            _ingredientsText.text = ingredientDescription;
        }

        if (_craftButton != null)
        {
            _craftButton.interactable = canCraft;
        }
    }

    private void OnCraftClick()
    {
        if (_currentRecipe != null)
        {
            _onCraftButtonClicked?.Invoke(_currentRecipe);
        }
    }
}
