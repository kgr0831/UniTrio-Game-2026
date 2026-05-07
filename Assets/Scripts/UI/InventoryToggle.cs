using UnityEngine;

/// <summary>
/// I키로 인벤토리 패널을 열고 닫습니다.
/// 항상 켜져 있는 부모(Canvas 등)에 부착하고
/// _inventoryPanel에 InventoryPanel을 연결하세요.
/// </summary>
public class InventoryToggle : MonoBehaviour
{
    public static InventoryToggle Instance { get; private set; }

    [SerializeField] private KeyCode _toggleKey = KeyCode.I;
    [SerializeField] private KeyCode _switchKey = KeyCode.Q;
    [SerializeField] private KeyCode _recipeKey = KeyCode.K;
    
    [SerializeField] private GameObject _inventoryPanel;
    [SerializeField] private GameObject _skillTreePanel;
    [SerializeField] private GameObject _recipePanel;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (_inventoryPanel != null) _inventoryPanel.SetActive(false);
        if (_skillTreePanel != null) _skillTreePanel.SetActive(false);
        if (_recipePanel != null) _recipePanel.SetActive(false);
    }

    public bool IsAnyPanelOpen()
    {
        return (_inventoryPanel != null && _inventoryPanel.activeSelf) || 
               (_skillTreePanel != null && _skillTreePanel.activeSelf) ||
               (_recipePanel != null && _recipePanel.activeSelf);
    }

    private void Update()
    {
        // ESC키: 패널 닫기
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (IsAnyPanelOpen())
            {
                CloseAllPanels();
                return;
            }
        }

        // I키: 토글
        if (Input.GetKeyDown(_toggleKey))
        {
            if (IsAnyPanelOpen())
            {
                CloseAllPanels();
            }
            else
            {
                // 열 때는 항상 인벤토리
                if (_inventoryPanel != null) _inventoryPanel.SetActive(true);
                if (_skillTreePanel != null) _skillTreePanel.SetActive(false);
                if (_recipePanel != null) _recipePanel.SetActive(false);
                SetPaused(true);
            }
        }

        // Q키: 인벤토리 ↔ 스킬트리 전환 (하나가 열려있을 때만)
        if (Input.GetKeyDown(_switchKey))
        {
            if (_inventoryPanel != null && _inventoryPanel.activeSelf)
            {
                _inventoryPanel.SetActive(false);
                if (_skillTreePanel != null) _skillTreePanel.SetActive(true);
            }
            else if (_skillTreePanel != null && _skillTreePanel.activeSelf)
            {
                _skillTreePanel.SetActive(false);
                if (_inventoryPanel != null) _inventoryPanel.SetActive(true);
            }
            // 둘 다 닫혀있으면 Q키 무시
        }

        // K키: 인벤토리가 열려있을 때 레시피 패널 토글
        if (Input.GetKeyDown(_recipeKey))
        {
            if (_inventoryPanel != null && _inventoryPanel.activeSelf)
            {
                if (_recipePanel != null)
                {
                    _recipePanel.SetActive(!_recipePanel.activeSelf);
                }
            }
        }
    }

    private void CloseAllPanels()
    {
        if (_inventoryPanel != null) _inventoryPanel.SetActive(false);
        if (_skillTreePanel != null) _skillTreePanel.SetActive(false);
        if (_recipePanel != null) _recipePanel.SetActive(false);
        SetPaused(false);
    }

    private void SetPaused(bool paused)
    {
        Time.timeScale = paused ? 0f : 1f;
    }
}
