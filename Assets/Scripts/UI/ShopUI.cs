using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 상인 NPC의 상점 UI.
/// 아이템 구매/판매를 처리합니다.
/// </summary>
public class ShopUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject _root;
    [SerializeField] private Transform _itemListParent;
    [SerializeField] private GameObject _shopItemPrefab;
    [SerializeField] private TextMeshProUGUI _shopTitleText;
    [SerializeField] private Button _closeButton;

    [Header("상품 목록")]
    [SerializeField] private List<ShopItem> _shopItems = new List<ShopItem>();

    private readonly List<GameObject> _itemEntries = new List<GameObject>();

    private void Awake()
    {
        if (_closeButton != null)
            _closeButton.onClick.AddListener(Close);
    }

    private void OnEnable()
    {
        RefreshUI();
        // 골드 변경 시 갱신
        if (GoldManager.Instance != null)
            GoldManager.Instance.OnGoldChanged += OnGoldChanged;
    }

    private void OnDisable()
    {
        if (GoldManager.Instance != null)
            GoldManager.Instance.OnGoldChanged -= OnGoldChanged;
    }

    private void OnGoldChanged(int _) => RefreshUI();

    public void Open(string shopTitle = "상점")
    {
        if (_shopTitleText != null) _shopTitleText.text = shopTitle;
        if (_root != null) _root.SetActive(true);
        RefreshUI();
    }

    public void Close()
    {
        if (_root != null) _root.SetActive(false);
    }

    /// <summary>상품 목록 UI를 갱신합니다.</summary>
    public void RefreshUI()
    {
        // 기존 항목 삭제
        foreach (var e in _itemEntries)
            if (e != null) Destroy(e);
        _itemEntries.Clear();

        if (_itemListParent == null || _shopItemPrefab == null) return;

        foreach (var item in _shopItems)
        {
            if (item.ItemData == null) continue;
            if (item.RequiresUnlock && !item.IsUnlocked) continue;

            var go = Instantiate(_shopItemPrefab, _itemListParent);
            _itemEntries.Add(go);

            // UI 구성
            var texts = go.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length >= 2)
            {
                texts[0].text = item.ItemData.Name;
                texts[1].text = $"{item.Price}G";
            }

            // 아이콘
            var images = go.GetComponentsInChildren<Image>();
            if (images.Length >= 2 && item.ItemData.Icon != null)
                images[1].sprite = item.ItemData.Icon;

            // 구매 버튼
            var btn = go.GetComponentInChildren<Button>();
            if (btn != null)
            {
                var capturedItem = item;
                bool canBuy = GoldManager.Instance != null && GoldManager.Instance.HasEnoughGold(item.Price);
                btn.interactable = canBuy;
                btn.onClick.AddListener(() => Buy(capturedItem));
            }
        }
    }

    private void Buy(ShopItem item)
    {
        if (GoldManager.Instance == null || InventoryManager.Instance == null) return;

        if (!GoldManager.Instance.SpendGold(item.Price))
        {
            if (NotificationUI.Instance != null)
                NotificationUI.Instance.ShowMessage("골드가 부족합니다!");
            return;
        }

        InventoryManager.Instance.AddItem(item.ItemData, 1);
        QuestEventBridge.ReportItemBought(item.ItemData.Name);

        if (NotificationUI.Instance != null)
            NotificationUI.Instance.ShowMessage($"{item.ItemData.Name}을(를) 구매했습니다!");

        RefreshUI();
    }

    /// <summary>특정 상품을 해금합니다 (퀘스트 완료 시 호출).</summary>
    public void UnlockItem(string itemName)
    {
        foreach (var item in _shopItems)
        {
            if (item.ItemData != null && item.ItemData.Name == itemName)
            {
                item.IsUnlocked = true;
                break;
            }
        }
        RefreshUI();
    }
}

[System.Serializable]
public class ShopItem
{
    public ItemData ItemData;
    public int Price;
    [Tooltip("해금 조건이 있는 상품인지")]
    public bool RequiresUnlock;
    [HideInInspector]
    public bool IsUnlocked;
}
