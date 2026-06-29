using UnityEngine;
using TMPro;

/// <summary>
/// 화면에 현재 보유 골드를 표시하는 UI.
/// GoldManager.OnGoldChanged를 구독하여 자동 갱신합니다.
/// </summary>
public class GoldUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI _goldText;
    [SerializeField] private GameObject _root;

    private void OnEnable()
    {
        if (GoldManager.Instance != null)
        {
            GoldManager.Instance.OnGoldChanged += UpdateDisplay;
            UpdateDisplay(GoldManager.Instance.Gold);
        }
    }

    private void OnDisable()
    {
        if (GoldManager.Instance != null)
            GoldManager.Instance.OnGoldChanged -= UpdateDisplay;
    }

    private void Start()
    {
        if (GoldManager.Instance != null)
            UpdateDisplay(GoldManager.Instance.Gold);
        else
            UpdateDisplay(0);
    }

    private void UpdateDisplay(int gold)
    {
        if (_goldText != null)
            _goldText.text = $"{gold:N0}";
    }

    public void Show()
    {
        if (_root != null) _root.SetActive(true);
    }

    public void Hide()
    {
        if (_root != null) _root.SetActive(false);
    }
}
