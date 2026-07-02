using UnityEngine;

/// <summary>
/// Building 상태일 때 플레이어 주변 3x3 영역을 하이라이트하고,
/// 마우스 호버링 위치(유효 타일)에 프리뷰를 렌더링하며 클릭 시 실제 건축물을 배치합니다.
/// </summary>
public class BuildingPlacementController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("씬 내의 타일맵 Grid. (Awake/Start 시 캐싱)")]
    [SerializeField] private Grid _grid;
    [Tooltip("설치 방해물 판정을 위한 레이어 마스크 (Player, Enemy, Obstacle 등)")]
    [SerializeField] private LayerMask _obstacleMask;
    [Tooltip("플레이어 위치 참조 (기본값: 자신)")]
    [SerializeField] private Transform _playerTransform;

    private PlayerStateManager _stateManager;
    private BuildingData _currentBuildingData;
    
    private GameObject _ghostPreview;
    private SpriteRenderer[] _ghostRenderers;

    // 3x3 하이라이트 관련
    private GameObject[] _highlightTiles = new GameObject[8];
    private bool _isHighlightActive = false;

    private void Start()
    {
        if (_grid == null)
        {
            Debug.LogError("[BuildingPlacementController] Grid 참조가 누락되었습니다! 인스펙터에서 맵 Grid를 연결해주세요.");
        }
        if (_playerTransform == null) _playerTransform = transform;
        
        _stateManager = GetComponent<PlayerStateManager>();

        InitializeHighlights();
    }

    /// <summary>
    /// 1x1 픽셀의 Texture2D를 생성하여 동적으로 8개의 하이라이트 타일을 생성합니다.
    /// </summary>
    private void InitializeHighlights()
    {
        // 1x1 사이즈의 흰색 텍스처 생성 (메모리 최적화)
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();

        // PixelsPerUnit을 1로 설정하여 1유닛(1타일) 크기로 맞춤
        Sprite highlightSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

        Vector3 scale = _grid != null ? new Vector3(_grid.cellSize.x, _grid.cellSize.y, 1f) : Vector3.one;

        for (int i = 0; i < 8; i++)
        {
            GameObject tileObj = new GameObject($"HighlightTile_{i}");
            tileObj.transform.SetParent(transform);
            tileObj.transform.localScale = scale;
            
            SpriteRenderer sr = tileObj.AddComponent<SpriteRenderer>();
            sr.sprite = highlightSprite;
            // 연한 빨간색 반투명
            sr.color = new Color(1f, 0f, 0f, 0.3f);
            sr.sortingOrder = 100; // 맵 타일보다 확실히 위에 보이도록 높은 Order 설정
            
            tileObj.SetActive(false);
            _highlightTiles[i] = tileObj;
        }
    }

    private void SetHighlightsActive(bool isActive)
    {
        if (_isHighlightActive == isActive) return;
        _isHighlightActive = isActive;
        
        foreach (var tile in _highlightTiles)
        {
            if (tile != null) tile.SetActive(isActive);
        }
    }

    /// <summary>
    /// 핫바에서 아이템이 선택되었을 때 데이터 세팅 및 프리뷰 생성
    /// </summary>
    public void SetBuildingData(BuildingData data)
    {
        _currentBuildingData = data;
        
        if (_ghostPreview != null) 
        {
            Destroy(_ghostPreview);
            _ghostPreview = null;
        }

        if (_currentBuildingData == null || _currentBuildingData.BuildingPrefab == null)
        {
            SetHighlightsActive(false);
            return;
        }

        // 원본 프리팹 기반으로 고스트 프리뷰 생성
        _ghostPreview = Instantiate(_currentBuildingData.BuildingPrefab);
        
        // 고스트 객체이므로 물리 연산 및 로직 무효화 (컴포넌트 파괴)
        foreach(var comp in _ghostPreview.GetComponentsInChildren<MonoBehaviour>())
        {
            Destroy(comp);
        }
        foreach(var col in _ghostPreview.GetComponentsInChildren<Collider2D>())
        {
            Destroy(col);
        }

        // 프리팹이 여러 스프라이트로 구성되어 있을 수 있으므로 자식 포함 모두 가져옵니다.
        _ghostRenderers = _ghostPreview.GetComponentsInChildren<SpriteRenderer>();
        if (_ghostRenderers != null && _ghostRenderers.Length > 0)
        {
            foreach (var r in _ghostRenderers)
            {
                Color c = r.color;
                c.a = 0.5f;
                r.color = c;
                r.sortingOrder += 3000; // 가려지는 현상 방지를 위해 극단적으로 값을 높임
            }
        }

        SetHighlightsActive(true);
    }

    private void Update()
    {
        if (_stateManager == null || _stateManager.CurrentMode != PlayerMode.Building)
        {
            if (_ghostPreview != null && _ghostPreview.activeSelf) 
                _ghostPreview.SetActive(false);
            
            SetHighlightsActive(false);
            return;
        }

        if (_currentBuildingData == null || _ghostPreview == null || _grid == null) return;

        // 우클릭 취소 로직
        if (Input.GetMouseButtonDown(1))
        {
            CancelBuildingMode();
            return;
        }

        // 1. 마우스 및 플레이어 셀 좌표 계산
        Vector3 mouseWorldPos = GetMouseWorldPosition();
        mouseWorldPos.z = 0f;
        Vector3Int mouseCell = _grid.WorldToCell(mouseWorldPos);
        Vector3Int playerCell = _grid.WorldToCell(_playerTransform.position);

        // 2. 3x3 주변 타일 하이라이트 위치 갱신
        UpdateHighlightPositions(playerCell);

        // 3. 마우스 커서가 유효한 3x3 영역 내부인지 판별 (정중앙 제외)
        int dx = Mathf.Abs(mouseCell.x - playerCell.x);
        int dy = Mathf.Abs(mouseCell.y - playerCell.y);
        bool isValidHover = (dx <= 1 && dy <= 1) && !(dx == 0 && dy == 0);

        if (isValidHover)
        {
            if (!_ghostPreview.activeSelf) _ghostPreview.SetActive(true);

            Vector3 cellCenterPos = _grid.GetCellCenterWorld(mouseCell);
            cellCenterPos.z = 0f;
            _ghostPreview.transform.position = cellCenterPos;

            // 4. 충돌(설치 방해물) 판정 및 색상 변경
            bool canPlace = CheckCanPlace(cellCenterPos);
            UpdateGhostColor(canPlace);

            // 5. 좌클릭 설치 및 소모 처리
            if (Input.GetMouseButtonDown(0))
            {
                if (canPlace)
                {
                    PlaceBuilding(cellCenterPos);
                }
                else
                {
                    // H-9 배치 불가 거부음
                    if (AudioManager.Instance != null) AudioManager.Instance.PlayUIDenied();
                }
            }
        }
        else
        {
            // 유효 영역 밖이면 프리뷰 숨김
            if (_ghostPreview.activeSelf) _ghostPreview.SetActive(false);
        }
    }

    /// <summary>
    /// Z=0 평면을 기준으로 마우스의 정확한 월드 좌표를 계산합니다. (Perspective 카메라 환경 완벽 대응)
    /// </summary>
    private Vector3 GetMouseWorldPosition()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        
        // Z=0 인 2D 평면 정의 (앞/뒤 양면 모두 체크)
        Plane xyPlane = new Plane(Vector3.forward, Vector3.zero);
        if (xyPlane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }
        
        xyPlane = new Plane(Vector3.back, Vector3.zero);
        if (xyPlane.Raycast(ray, out distance))
        {
            return ray.GetPoint(distance);
        }

        // Raycast 실패 시 Fallback (기존 방식)
        Vector3 fallback = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        fallback.z = 0f;
        return fallback;
    }

    /// <summary>
    /// 플레이어 주변 3x3(자신 제외 8칸)에 하이라이트 타일을 배치합니다.
    /// </summary>
    private void UpdateHighlightPositions(Vector3Int playerCell)
    {
        int index = 0;
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0) continue; // 플레이어 위치 제외
                
                Vector3Int targetCell = new Vector3Int(playerCell.x + x, playerCell.y + y, 0);
                Vector3 centerWorld = _grid.GetCellCenterWorld(targetCell);
                centerWorld.z = 0f;

                if (index < _highlightTiles.Length && _highlightTiles[index] != null)
                {
                    _highlightTiles[index].transform.position = centerWorld;
                }
                index++;
            }
        }
    }

    /// <summary>
    /// 건축 모드를 취소하고 이전 무기 상태로 되돌립니다.
    /// </summary>
    private void CancelBuildingMode()
    {
        Debug.Log("[Building] 우클릭 - 건축 모드 취소");
        SetBuildingData(null);
        _stateManager.SetMode(PlayerMode.Combat);
        // 무기 재장착은 퀵슬롯 키 입력으로 수행 (WeaponSlotManager 의존 제거)
    }

    /// <summary>
    /// OverlapBox를 사용하여 타일 내 장애물을 검사합니다.
    /// </summary>
    private bool CheckCanPlace(Vector3 pos)
    {
        Vector2 size = new Vector2(_currentBuildingData.TileSize.x, _currentBuildingData.TileSize.y);
        Collider2D hit = Physics2D.OverlapBox(pos, size * 0.9f, 0f, _obstacleMask);
        return hit == null;
    }

    private void UpdateGhostColor(bool canPlace)
    {
        if (_ghostRenderers != null && _ghostRenderers.Length > 0)
        {
            Color c = canPlace ? Color.white : Color.red;
            c.a = 0.5f; // 반투명 유지
            
            foreach (var r in _ghostRenderers)
            {
                r.color = c;
            }
        }
    }

    /// <summary>
    /// 실제 건축물을 생성하고 아이템을 소모합니다.
    /// </summary>
    private void PlaceBuilding(Vector3 pos)
    {
        Instantiate(_currentBuildingData.BuildingPrefab, pos, Quaternion.identity);
        Debug.Log($"[Building] 건축물 설치 완료: {_currentBuildingData.Name}");

        // H-8 건물 배치 확정음
        if (AudioManager.Instance != null) AudioManager.Instance.PlayBuildPlace();

        // 아이템 소모 로직 연동
        if (InventoryManager.Instance != null)
        {
            bool consumed = InventoryManager.Instance.ConsumeItems(_currentBuildingData, 1);
            if (consumed)
            {
                int remaining = InventoryManager.Instance.GetItemCount(_currentBuildingData);
                if (remaining <= 0)
                {
                    Debug.Log($"[Building] {_currentBuildingData.Name} 아이템을 모두 소모하여 건축 모드를 자동 종료합니다.");
                    CancelBuildingMode();
                }
            }
        }
    }
}
