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

    [Header("Bonfire")]
    [Tooltip("Bonfire 조리 패널 (BonfireUIPanel이 부착된 오브젝트)")]
    [SerializeField] private GameObject _bonfirePanel;

    [Header("StorageBox")]
    [Tooltip("보관 상자 패널 (StorageBoxUIPanel이 부착된 오브젝트)")]
    [SerializeField] private GameObject _boxPanel;

    // Bonfire 패널이 열려있는지 추적 (ESC로 닫을 때 BonfireUIPanel.Close()도 호출)
    private bool _isBonfireMode;
    // StorageBox 패널이 열려있는지 추적
    private bool _isBoxMode;

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
        if (_bonfirePanel != null) _bonfirePanel.SetActive(false);
        if (_boxPanel != null) _boxPanel.SetActive(false);
    }

    public bool IsAnyPanelOpen()
    {
        return (_inventoryPanel != null && _inventoryPanel.activeSelf) || 
               (_skillTreePanel != null && _skillTreePanel.activeSelf) ||
               (_recipePanel != null && _recipePanel.activeSelf) ||
               (_bonfirePanel != null && _bonfirePanel.activeSelf) ||
               (_boxPanel != null && _boxPanel.activeSelf);
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

        // Bonfire/Box 모드에서는 I/Q/K 키 입력을 차단 (해당 UI만 조작 가능)
        if (_isBonfireMode || _isBoxMode) return;

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

    /// <summary>
    /// Bonfire 상호작용 시 호출: Bonfire 패널만 엽니다. (인벤토리 미표시)
    /// timeScale은 0으로 설정하되, 조리 타이머는 unscaledDeltaTime으로 동작합니다.
    /// </summary>
    public void OpenBonfirePanel()
    {
        _isBonfireMode = true;

        // Bonfire 패널만 표시 — 인벤토리는 열지 않음 (스크롤뷰 재료 목록으로 대체)
        if (_bonfirePanel != null) _bonfirePanel.SetActive(true);

        // 다른 패널은 닫기
        if (_inventoryPanel != null) _inventoryPanel.SetActive(false);
        if (_skillTreePanel != null) _skillTreePanel.SetActive(false);
        if (_recipePanel != null) _recipePanel.SetActive(false);

        SetPaused(true);
    }

    /// <summary>
    /// StorageBox 상호작용 시 호출: 인벤토리 패널 + 상자 패널을 동시에 엽니다.
    /// 인벤토리 아이템을 상자로 드래그 드롭하기 위해 인벤토리도 함께 표시합니다.
    /// </summary>
    public void OpenBoxPanel()
    {
        _isBoxMode = true;

        // BoxPanel만 단독 표시
        if (_boxPanel != null) _boxPanel.SetActive(true);
        if (_inventoryPanel != null) _inventoryPanel.SetActive(false);

        // 나머지 패널은 닫기
        if (_skillTreePanel != null) _skillTreePanel.SetActive(false);
        if (_recipePanel != null) _recipePanel.SetActive(false);
        if (_bonfirePanel != null) _bonfirePanel.SetActive(false);

        SetPaused(true);
    }

    private void CloseAllPanels()
    {
        // Bonfire 모드였다면 BonfireUIPanel.Close() 호출하여 정리
        if (_isBonfireMode && BonfireUIPanel.Instance != null)
        {
            BonfireUIPanel.Instance.Close();
            _isBonfireMode = false;
        }

        // StorageBox 모드였다면 StorageBoxUIPanel.Close() 호출하여 바인딩 해제
        if (_isBoxMode && StorageBoxUIPanel.Instance != null)
        {
            StorageBoxUIPanel.Instance.Close();
            _isBoxMode = false;
        }

        if (_inventoryPanel != null) _inventoryPanel.SetActive(false);
        if (_skillTreePanel != null) _skillTreePanel.SetActive(false);
        if (_recipePanel != null) _recipePanel.SetActive(false);
        if (_bonfirePanel != null) _bonfirePanel.SetActive(false);
        if (_boxPanel != null) _boxPanel.SetActive(false);
        SetPaused(false);
    }

    private void SetPaused(bool paused)
    {
        Time.timeScale = paused ? 0f : 1f;
    }
}
