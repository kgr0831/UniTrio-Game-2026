using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bonfire 아웃풋 칸 UI.
/// 각 인풋 슬롯에 대응하는 아웃풋 정보를 표시합니다:
///   - 완성될 아이템 이미지
///   - 완성될 아이템 이름
///   - 조리 진행 프로그레스 바
/// 조리 완료 시 자동으로 인벤토리에 추가되므로 별도 드래그 불필요.
/// </summary>
public class BonfireOutputDisplayUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image _outputIcon;
    [SerializeField] private Text _outputName;
    [SerializeField] private Image _progressBarFill;
    [SerializeField] private GameObject _displayRoot;  // 전체 칸 표시/숨김

    private int _slotIndex;

    /// <summary>
    /// 슬롯 인덱스를 설정합니다.
    /// </summary>
    public void Setup(int index)
    {
        _slotIndex = index;
        ClearDisplay();
    }

    /// <summary>
    /// Bonfire 슬롯 상태에 따라 UI를 갱신합니다.
    /// </summary>
    public void Refresh(BonfireInteractable.CookSlot slot)
    {
        if (!slot.IsCooking || slot.ResultItem == null)
        {
            // 조리 중이 아님 → 모든 표시 초기화
            ClearDisplay();
            return;
        }

        SetVisible(true);

        // 완성될 아이템 아이콘
        if (_outputIcon != null)
        {
            _outputIcon.sprite = slot.ResultItem.Icon;
            _outputIcon.enabled = slot.ResultItem.Icon != null;
        }

        // 완성될 아이템 이름
        if (_outputName != null)
        {
            _outputName.text = slot.ResultItem.Name;
        }

        // 프로그레스 바
        if (_progressBarFill != null)
        {
            _progressBarFill.fillAmount = slot.Progress;
        }
    }

    /// <summary>
    /// 아웃풋 칸의 모든 UI 요소를 초기화합니다.
    /// _displayRoot 유무와 관계없이 아이콘/이름/프로그레스바를 확실히 비웁니다.
    /// </summary>
    private void ClearDisplay()
    {
        // 아이콘 초기화
        if (_outputIcon != null)
        {
            _outputIcon.sprite = null;
            _outputIcon.enabled = false;
        }

        // 이름 초기화
        if (_outputName != null)
        {
            _outputName.text = "";
        }

        // 프로그레스 바 초기화
        if (_progressBarFill != null)
        {
            _progressBarFill.fillAmount = 0f;
        }

        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        if (_displayRoot != null)
        {
            _displayRoot.SetActive(visible);
        }
    }
}
