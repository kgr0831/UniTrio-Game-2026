using UnityEngine;

/// <summary>
/// 보관 상자 UI 패널 매니저.
/// BonfireUIPanel 패턴을 참고한 설계:
///   - Open/Close로 특정 상자 인스턴스와 바인딩/해제
///   - StorageBoxInteractable의 이벤트를 구독하여 슬롯 UI 갱신
///   - InventoryToggle과 연동하여 패널 열림 상태 관리
///
/// SRP: 이 클래스는 패널 활성화/슬롯 바인딩만 담당.
///      데이터 변경은 StorageBoxInteractable, 개별 드래그는 StorageBoxSlotUI가 처리.
/// </summary>
public class StorageBoxUIPanel : MonoBehaviour
{
    public static StorageBoxUIPanel Instance { get; private set; }

    [Header("Panel")]
    [Tooltip("BoxPanel 루트 GameObject (활성화/비활성화 대상)")]
    [SerializeField] private GameObject _panelRoot;

    [Header("Slot Container")]
    [Tooltip("BoxObject 하위의 슬롯 프리팹이 배치될 컨테이너 (Grid Layout Group 부착 권장)")]
    [SerializeField] private Transform _slotContainer;

    [Header("Slot Prefab")]
    [Tooltip("StorageBoxSlotUI가 부착된 슬롯 프리팹")]
    [SerializeField] private GameObject _slotPrefab;

    // 현재 바인딩된 상자
    private StorageBoxInteractable _currentBox;

    // 동적 생성된 슬롯 UI 배열 (재사용을 위해 캐싱)
    private StorageBoxSlotUI[] _slotUIs;
    private int _activeSlotCount;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (_panelRoot != null) _panelRoot.SetActive(false);
    }

    // ── 패널 열기 ───────────────────────────────────────────

    /// <summary>
    /// 특정 상자와 연결하여 UI를 엽니다.
    /// </summary>
    public void Open(StorageBoxInteractable box)
    {
        if (box == null) return;

        // 이미 열려있는 상태에서 다른 상자를 열면 기존 것 닫기
        if (_currentBox != null && _currentBox != box)
        {
            Close();
        }

        _currentBox = box;

        // 이벤트 구독
        _currentBox.OnSlotChanged += OnSlotChanged;

        if (_panelRoot != null) _panelRoot.SetActive(true);

        // InventoryToggle과 연동 — 인벤토리도 함께 열기
        if (InventoryToggle.Instance != null)
        {
            InventoryToggle.Instance.OpenBoxPanel();
        }

        // 슬롯 UI 생성/바인딩
        SetupSlots();

        Debug.Log($"[StorageBoxUI] 패널 열림 (슬롯 수: {_currentBox.SlotCount})");
    }

    /// <summary>
    /// UI를 닫고 상자와의 바인딩을 해제합니다.
    /// </summary>
    public void Close()
    {
        if (_currentBox != null)
        {
            _currentBox.OnSlotChanged -= OnSlotChanged;
            _currentBox = null;
        }

        // 모든 슬롯 언바인드
        UnbindAllSlots();

        if (_panelRoot != null) _panelRoot.SetActive(false);

        Debug.Log("[StorageBoxUI] 패널 닫힘");
    }

    public bool IsOpen()
    {
        return _panelRoot != null && _panelRoot.activeSelf;
    }

    public StorageBoxInteractable GetCurrentBox()
    {
        return _currentBox;
    }

    // ── 슬롯 초기화 ────────────────────────────────────────

    /// <summary>
    /// 상자의 슬롯 수에 맞춰 슬롯 UI를 동적 생성하거나 재사용합니다.
    /// 이미 생성된 슬롯이 있으면 부족한 만큼만 추가 생성합니다.
    /// </summary>
    private void SetupSlots()
    {
        if (_currentBox == null || _slotContainer == null || _slotPrefab == null) return;

        int requiredCount = _currentBox.SlotCount;

        // 슬롯 배열이 없거나 부족하면 확장
        if (_slotUIs == null || _slotUIs.Length < requiredCount)
        {
            StorageBoxSlotUI[] newArray = new StorageBoxSlotUI[requiredCount];

            // 기존 슬롯 복사
            if (_slotUIs != null)
            {
                for (int i = 0; i < _slotUIs.Length; i++)
                {
                    newArray[i] = _slotUIs[i];
                }
            }

            // 부족분 생성
            int startIndex = _slotUIs != null ? _slotUIs.Length : 0;
            for (int i = startIndex; i < requiredCount; i++)
            {
                GameObject go = Instantiate(_slotPrefab, _slotContainer);
                go.name = $"BoxSlot_{i}";

                StorageBoxSlotUI slotUI = go.GetComponent<StorageBoxSlotUI>();
                if (slotUI == null)
                {
                    Debug.LogError($"[StorageBoxUI] 슬롯 프리팹에 StorageBoxSlotUI 컴포넌트가 없습니다!");
                    continue;
                }

                newArray[i] = slotUI;
            }

            _slotUIs = newArray;
        }

        // 모든 슬롯 활성화 & 바인딩
        for (int i = 0; i < requiredCount; i++)
        {
            if (_slotUIs[i] != null)
            {
                _slotUIs[i].gameObject.SetActive(true);
                _slotUIs[i].Bind(_currentBox, i);
            }
        }

        // 초과분 비활성화 (이전에 더 큰 상자를 열었을 경우)
        for (int i = requiredCount; i < _slotUIs.Length; i++)
        {
            if (_slotUIs[i] != null)
            {
                _slotUIs[i].Unbind();
                _slotUIs[i].gameObject.SetActive(false);
            }
        }

        _activeSlotCount = requiredCount;
    }

    private void UnbindAllSlots()
    {
        if (_slotUIs == null) return;

        for (int i = 0; i < _slotUIs.Length; i++)
        {
            if (_slotUIs[i] != null)
            {
                _slotUIs[i].Unbind();
            }
        }
    }

    // ── 이벤트 콜백 ────────────────────────────────────────

    /// <summary>
    /// StorageBoxInteractable의 OnSlotChanged 이벤트 핸들러.
    /// 특정 슬롯의 UI만 갱신합니다.
    /// </summary>
    private void OnSlotChanged(int slotIndex)
    {
        if (_slotUIs == null || slotIndex < 0 || slotIndex >= _activeSlotCount) return;

        if (_slotUIs[slotIndex] != null)
        {
            _slotUIs[slotIndex].RefreshDisplay();
        }
    }
}
