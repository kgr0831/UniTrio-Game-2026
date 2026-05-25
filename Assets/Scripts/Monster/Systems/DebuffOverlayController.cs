using UnityEngine;

/// <summary>
/// DebuffReceiver의 이벤트를 구독하여 DebuffOverlay 쉐이더의 파라미터를 갱신합니다 (SRP).
/// 몬스터 스프라이트 위에 오버레이 SpriteRenderer를 생성하여 디버프 시각 효과를 표시합니다.
/// ElementalWeaponSystem의 오버레이 패턴과 동일한 구조입니다.
/// </summary>
[RequireComponent(typeof(DebuffReceiver))]
public sealed class DebuffOverlayController : MonoBehaviour
{
    private DebuffReceiver _debuffReceiver;
    private SpriteRenderer _mainRenderer;
    private SpriteRenderer _overlayRenderer;
    private GameObject _overlayObj;
    private Material _overlayMat;
    private MaterialPropertyBlock _mpb;
    private bool _overlayBuilt;

    // Shader Property ID 캐시 (GC 방지)
    private static readonly int _ID_FireActive   = Shader.PropertyToID("_FireActive");
    private static readonly int _ID_IceActive    = Shader.PropertyToID("_IceActive");
    private static readonly int _ID_EarthActive  = Shader.PropertyToID("_EarthActive");

    // 쉐이더 참조
    private static Shader _debuffOverlayShader;

    private void Awake()
    {
        _debuffReceiver = GetComponent<DebuffReceiver>();
        _mpb = new MaterialPropertyBlock();

        // SpriteRenderer는 같은 오브젝트 또는 자식에서 탐색
        _mainRenderer = GetComponent<SpriteRenderer>();
        if (_mainRenderer == null)
            _mainRenderer = GetComponentInChildren<SpriteRenderer>();

        EnsureShader();
        TryBuildOverlay();
    }

    private void OnEnable()
    {
        if (_debuffReceiver != null)
        {
            _debuffReceiver.OnDebuffApplied   += OnDebuffChanged;
            _debuffReceiver.OnDebuffRemoved   += OnDebuffChanged;
            _debuffReceiver.OnDebuffRefreshed += OnDebuffChanged;
        }
        // 초기 상태 동기화
        SyncOverlayState();
    }

    private void OnDisable()
    {
        if (_debuffReceiver != null)
        {
            _debuffReceiver.OnDebuffApplied   -= OnDebuffChanged;
            _debuffReceiver.OnDebuffRemoved   -= OnDebuffChanged;
            _debuffReceiver.OnDebuffRefreshed -= OnDebuffChanged;
        }
    }

    private void LateUpdate()
    {
        // 아직 오버레이가 없으면 다시 시도 (Awake 시점에 SpriteRenderer가 없었던 경우)
        if (!_overlayBuilt)
        {
            if (_mainRenderer == null)
            {
                _mainRenderer = GetComponent<SpriteRenderer>();
                if (_mainRenderer == null)
                    _mainRenderer = GetComponentInChildren<SpriteRenderer>();
            }
            if (_mainRenderer != null)
            {
                EnsureShader();
                TryBuildOverlay();
            }
            if (!_overlayBuilt) return;
        }

        if (_overlayRenderer == null || _mainRenderer == null) return;

        // 메인 스프라이트와 동기화
        _overlayRenderer.sprite = _mainRenderer.sprite;
        _overlayRenderer.flipX  = _mainRenderer.flipX;
        _overlayRenderer.flipY  = _mainRenderer.flipY;

        // 소팅 동기화 (메인보다 1 높게)
        _overlayRenderer.sortingLayerName = _mainRenderer.sortingLayerName;
        _overlayRenderer.sortingOrder     = _mainRenderer.sortingOrder + 1;

        // 메인 렌더러가 비활성이면 오버레이도 숨김
        bool visible = _mainRenderer.enabled && _mainRenderer.gameObject.activeInHierarchy;
        
        // 디버프가 하나도 없으면 오버레이 숨김
        bool hasAnyDebuff = _debuffReceiver.HasDebuff(ElementType.Fire) ||
                           _debuffReceiver.HasDebuff(ElementType.Ice) ||
                           _debuffReceiver.HasDebuff(ElementType.Earth);

        _overlayObj.SetActive(visible && hasAnyDebuff);
    }

    // ── 쉐이더 로드 ──────────────────────────────────────

    private static void EnsureShader()
    {
        if (_debuffOverlayShader != null) return;

        _debuffOverlayShader = Shader.Find("Custom/DebuffOverlay");

        if (_debuffOverlayShader == null)
        {
            Debug.LogWarning("[DebuffOverlayController] Shader 'Custom/DebuffOverlay' not found. " +
                             "셰이더가 프로젝트에 포함되어 있는지, 이름이 정확한지 확인하세요.");
        }
    }

    // ── 오버레이 구축 ──────────────────────────────────────

    private void TryBuildOverlay()
    {
        if (_overlayBuilt) return;
        if (_mainRenderer == null || _debuffOverlayShader == null) return;

        _overlayMat = new Material(_debuffOverlayShader);

        _overlayObj = new GameObject("DebuffOverlay");
        _overlayObj.transform.SetParent(_mainRenderer.transform, false);
        _overlayObj.transform.localPosition = Vector3.zero;
        _overlayObj.transform.localRotation = Quaternion.identity;
        _overlayObj.transform.localScale = Vector3.one;

        _overlayRenderer = _overlayObj.AddComponent<SpriteRenderer>();
        _overlayRenderer.sprite = _mainRenderer.sprite;
        _overlayRenderer.flipX  = _mainRenderer.flipX;
        _overlayRenderer.flipY  = _mainRenderer.flipY;
        _overlayRenderer.material = _overlayMat;
        _overlayRenderer.sortingLayerName = _mainRenderer.sortingLayerName;
        _overlayRenderer.sortingOrder     = _mainRenderer.sortingOrder + 1;

        _overlayObj.SetActive(false);
        _overlayBuilt = true;
    }

    // ── 이벤트 핸들러 ──────────────────────────────────────

    private void OnDebuffChanged(ElementType element)
    {
        SyncOverlayState();
    }

    private void SyncOverlayState()
    {
        if (_overlayMat == null) return;

        // URP의 SRP Batcher가 활성화되면 MaterialPropertyBlock이
        // CBUFFER_START(UnityPerMaterial) 안의 프로퍼티를 오버라이드하지 못합니다.
        // 이미 몬스터마다 고유 Material 인스턴스를 사용하므로 직접 설정합니다.
        _overlayMat.SetFloat(_ID_FireActive,  _debuffReceiver.HasDebuff(ElementType.Fire)  ? 1f : 0f);
        _overlayMat.SetFloat(_ID_IceActive,   _debuffReceiver.HasDebuff(ElementType.Ice)   ? 1f : 0f);
        _overlayMat.SetFloat(_ID_EarthActive, _debuffReceiver.HasDebuff(ElementType.Earth) ? 1f : 0f);
    }

    private void OnDestroy()
    {
        if (_overlayObj != null)
            Destroy(_overlayObj);
        if (_overlayMat != null)
            Destroy(_overlayMat);
    }
}
