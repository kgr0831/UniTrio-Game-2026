using UnityEngine;

/// <summary>
/// 무기 속성 전환 시스템.
/// 우클릭(Mouse1)으로 Earth → Fire → Ice → Earth 순환하며,
/// 현재 장착된 무기 스프라이트 위에 절차적 오버레이를 렌더링합니다.
///
/// v2: 궤적(TrailRenderer), 잔상(Ghost Trail), 무기 아우라 색상까지
///     속성에 맞춰 완전 동기화합니다.
/// </summary>
public class ElementalWeaponSystem : MonoBehaviour
{
    // ── Inspector References ──────────────────────────────
    [Header("Dependencies")]
    [SerializeField] private PlayerWeaponController _weaponController;

    [Header("Element Presets (Earth / Fire / Ice 순서)")]
    [Tooltip("ElementalShaderData SO를 Earth=0, Fire=1, Ice=2 순서대로 할당하세요.")]
    [SerializeField] private ElementalShaderData[] _elementPresets = new ElementalShaderData[3];

    [Header("Overlay Material")]
    [Tooltip("ElementalOverlay 셰이더를 사용하는 머티리얼을 할당하세요.")]
    [SerializeField] private Material _overlayBaseMaterial;

    [Header("Sorting")]
    [SerializeField] private string _sortingLayerName = "Weapons";
    [SerializeField] private int    _sortingOrderOffset = 1;

    // ── Runtime State ─────────────────────────────────────
    private ElementType _currentElement = ElementType.Earth;
    public  ElementType CurrentElement => _currentElement;

    private GameObject      _overlayObj;
    private SpriteRenderer  _overlayRenderer;
    private MaterialPropertyBlock _propBlock;

    // 현재 추적 중인 무기 렌더러 (무기 교체 감지용)
    private WeaponBehaviourBase _trackedBehaviour;
    private SpriteRenderer     _trackedRenderer;

    // 오버레이 머티리얼 인스턴스 (런타임 복제)
    private Material _overlayMatInstance;

    // ── 속성별 대표 색상 (궤적/잔상 동기화용) ──────────────
    // Aura: 스프라이트 틴트 기반 색상
    private static readonly Color[] _elementAuraColors = new Color[]
    {
        new Color(0.85f, 0.7f, 0.25f, 1f),   // Earth: 황금빛 토파즈 (마법적 대지)
        new Color(1.0f, 0.35f, 0.05f, 1f),   // Fire:  순수 붉은 주황
        new Color(0.15f, 0.85f, 1.0f, 1f)    // Ice:   밝은 시안
    };

    // HDR: 잔상/궤적 Glow에 주입할 HDR 색상 (픽셀아트 보존용 낮은 배율)
    private static readonly Color[] _elementHDRColors = new Color[]
    {
        new Color(1.0f, 0.8f, 0.2f, 1f) * 2.5f,      // Earth: 황금빛 앰버 HDR ×2.5
        new Color(1.0f, 0.25f, 0.0f, 1f) * 4f,        // Fire:  Red/Orange ×4
        new Color(0.1f, 0.7f, 1.0f, 1f) * 4f           // Ice:   Cyan ×4
    };

    // 속성별 Glow 강도 (픽셀 형태가 보이도록 억제)
    private static readonly float[] _elementGlowIntensity = new float[]
    {
        2.0f,    // Earth: 은은한 마법 발광
        3.5f,    // Fire:  적당한 발광
        3.0f     // Ice:   적당한 발광
    };

    // 캐시된 참조들 (매 프레임 탐색 방지)
    private TrailRenderer[]  _cachedTrails;
    private SpriteRenderer[] _cachedWeaponRenderers;
    private FloatingWeaponMotion _cachedMotion;

    // ── Shader Property ID 캐시 (GC 방지) ──────────────────
    private static readonly int _ID_ElementType     = Shader.PropertyToID("_ElementType");
    private static readonly int _ID_EffectIntensity  = Shader.PropertyToID("_EffectIntensity");
    private static readonly int _ID_PixelRes         = Shader.PropertyToID("_PixelRes");
    private static readonly int _ID_ScrollSpeed      = Shader.PropertyToID("_ScrollSpeed");
    private static readonly int _ID_NoiseScale       = Shader.PropertyToID("_NoiseScale");
    private static readonly int _ID_VoronoiScale     = Shader.PropertyToID("_VoronoiScale");
    private static readonly int _ID_FireColor1       = Shader.PropertyToID("_FireColor1");
    private static readonly int _ID_FireColor2       = Shader.PropertyToID("_FireColor2");
    private static readonly int _ID_FireColor3       = Shader.PropertyToID("_FireColor3");
    private static readonly int _ID_IceVoronoiScale  = Shader.PropertyToID("_IceVoronoiScale");
    private static readonly int _ID_EdgeThreshold    = Shader.PropertyToID("_EdgeThreshold");
    private static readonly int _ID_IceColor         = Shader.PropertyToID("_IceColor");
    private static readonly int _ID_DitherScale      = Shader.PropertyToID("_DitherScale");
    private static readonly int _ID_PulseSpeed       = Shader.PropertyToID("_PulseSpeed");
    private static readonly int _ID_EarthColor       = Shader.PropertyToID("_EarthColor");

    // SpriteGlow / VFXLit2D 프로퍼티 ID
    private static readonly int _ID_GlowColor       = Shader.PropertyToID("_GlowColor");
    private static readonly int _ID_GlowIntensity   = Shader.PropertyToID("_GlowIntensity");
    private static readonly int _ID_EmissionColor    = Shader.PropertyToID("_EmissionColor");
    private static readonly int _ID_EmissionIntensity = Shader.PropertyToID("_EmissionIntensity");

    // ──────────────────────────────────────────────────────
    //  Lifecycle
    // ──────────────────────────────────────────────────────
    private void Start()
    {
        if (_weaponController == null)
            _weaponController = GetComponent<PlayerWeaponController>();
        if (_weaponController == null)
            _weaponController = FindObjectOfType<PlayerWeaponController>();

        _propBlock = new MaterialPropertyBlock();

        // 오버레이 머티리얼 인스턴스 생성
        if (_overlayBaseMaterial != null)
            _overlayMatInstance = new Material(_overlayBaseMaterial);

        // 무기 교체 이벤트 구독
        if (_weaponController != null)
            _weaponController.OnWeaponChanged += OnWeaponChanged;
    }

    private void OnDestroy()
    {
        if (_weaponController != null)
            _weaponController.OnWeaponChanged -= OnWeaponChanged;

        if (_overlayMatInstance != null)
            Destroy(_overlayMatInstance);

        DestroyOverlay();
    }

    private void Update()
    {
        HandleElementSwitch();
        SyncOverlay();
    }

    // ──────────────────────────────────────────────────────
    //  Input: 우클릭 속성 전환
    // ──────────────────────────────────────────────────────
    private void HandleElementSwitch()
    {
        if (!Input.GetMouseButtonDown(1)) return;

        // 무기 미장착 시 무시
        if (_weaponController == null || _weaponController.ActiveBehaviour == null) return;

        // 활(Bow) 차징 중이면 속성 전환 무시 (기존 차징 우선)
        if (_weaponController.ActiveBehaviour is BowBehaviour bow)
        {
            if (bow.IsAttacking) return;
        }

        // UI 위에서 클릭했으면 무시
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            return;

        // 순환: Earth → Fire → Ice → Earth
        _currentElement = (ElementType)(((int)_currentElement + 1) % 3);

        ApplyElementVisuals();
        ApplyTrailAndGhostColors();

        Debug.Log($"[ElementalWeapon] 속성 전환 → {_currentElement}");
    }

    // ──────────────────────────────────────────────────────
    //  Weapon Change Callback
    // ──────────────────────────────────────────────────────
    private void OnWeaponChanged(WeaponType newType)
    {
        // 무기가 교체되면 오버레이를 재구축
        _trackedBehaviour = null;
        _trackedRenderer  = null;
        _cachedTrails     = null;
        _cachedWeaponRenderers = null;
        _cachedMotion     = null;
        DestroyOverlay();
    }

    // ──────────────────────────────────────────────────────
    //  Overlay Sync (매 프레임)
    // ──────────────────────────────────────────────────────
    private void SyncOverlay()
    {
        var activeBehaviour = _weaponController != null ? _weaponController.ActiveBehaviour : null;

        // 무기 미장착 → 오버레이 숨김
        if (activeBehaviour == null)
        {
            if (_overlayObj != null) _overlayObj.SetActive(false);
            return;
        }

        // 무기가 교체되었으면 오버레이 재구축
        if (activeBehaviour != _trackedBehaviour)
        {
            RebuildOverlay(activeBehaviour);
        }

        // 오버레이가 없으면 리턴
        if (_overlayRenderer == null || _trackedRenderer == null) return;

        // 스프라이트 동기화
        _overlayRenderer.sprite = _trackedRenderer.sprite;
        _overlayRenderer.flipX  = _trackedRenderer.flipX;
        _overlayRenderer.flipY  = _trackedRenderer.flipY;

        // 소팅 동기화
        _overlayRenderer.sortingLayerName = _trackedRenderer.sortingLayerName;
        _overlayRenderer.sortingOrder     = _trackedRenderer.sortingOrder + _sortingOrderOffset;

        // 무기 렌더러가 비활성이면 오버레이도 숨김
        bool visible = _trackedRenderer.enabled &&
                       _trackedRenderer.gameObject.activeInHierarchy;
        _overlayObj.SetActive(visible);
    }

    // ──────────────────────────────────────────────────────
    //  Overlay 구축 / 파괴
    // ──────────────────────────────────────────────────────
    private void RebuildOverlay(WeaponBehaviourBase behaviour)
    {
        DestroyOverlay();

        // 무기의 주 SpriteRenderer 탐색
        SpriteRenderer weaponRenderer = behaviour.GetComponentInChildren<SpriteRenderer>();
        if (weaponRenderer == null)
        {
            Debug.LogWarning("[ElementalWeapon] 무기에 SpriteRenderer를 찾을 수 없습니다.");
            _trackedBehaviour = behaviour;
            return;
        }

        _trackedBehaviour = behaviour;
        _trackedRenderer  = weaponRenderer;

        // TrailRenderer 및 모든 SpriteRenderer 캐싱
        _cachedTrails = behaviour.GetComponentsInChildren<TrailRenderer>(true);
        _cachedWeaponRenderers = behaviour.GetComponentsInChildren<SpriteRenderer>(true);
        _cachedMotion = behaviour.GetComponent<FloatingWeaponMotion>();

        // 오버레이 자식 오브젝트 생성
        _overlayObj = new GameObject("ElementalOverlay");
        _overlayObj.transform.SetParent(weaponRenderer.transform, false);
        _overlayObj.transform.localPosition = Vector3.zero;
        _overlayObj.transform.localRotation = Quaternion.identity;
        _overlayObj.transform.localScale    = Vector3.one;

        // SpriteRenderer 추가
        _overlayRenderer = _overlayObj.AddComponent<SpriteRenderer>();
        _overlayRenderer.sprite = weaponRenderer.sprite;
        _overlayRenderer.flipX  = weaponRenderer.flipX;
        _overlayRenderer.flipY  = weaponRenderer.flipY;
        _overlayRenderer.sortingLayerName = weaponRenderer.sortingLayerName;
        _overlayRenderer.sortingOrder     = weaponRenderer.sortingOrder + _sortingOrderOffset;

        // 오버레이 머티리얼 할당
        if (_overlayMatInstance != null)
            _overlayRenderer.material = _overlayMatInstance;

        // 현재 속성 시각 적용
        ApplyElementVisuals();
        ApplyTrailAndGhostColors();
    }

    private void DestroyOverlay()
    {
        if (_overlayObj != null)
        {
            Destroy(_overlayObj);
            _overlayObj      = null;
            _overlayRenderer = null;
        }
    }

    // ──────────────────────────────────────────────────────
    //  MaterialPropertyBlock으로 속성 파라미터 적용
    // ──────────────────────────────────────────────────────
    private void ApplyElementVisuals()
    {
        if (_overlayRenderer == null || _propBlock == null) return;

        int elemIndex = (int)_currentElement;

        // 프리셋이 할당되어 있으면 SO 값 사용, 아니면 기본값
        ElementalShaderData preset = null;
        if (_elementPresets != null && elemIndex < _elementPresets.Length)
            preset = _elementPresets[elemIndex];

        _overlayRenderer.GetPropertyBlock(_propBlock);

        // 공통 파라미터
        _propBlock.SetFloat(_ID_ElementType, elemIndex);
        _propBlock.SetFloat(_ID_EffectIntensity, preset != null ? preset.EffectIntensity : 1.0f);
        _propBlock.SetFloat(_ID_PixelRes, preset != null ? preset.PixelResolution : 32.0f);

        if (preset != null)
        {
            // 속성별 파라미터 적용
            switch (_currentElement)
            {
                case ElementType.Fire:
                    _propBlock.SetFloat(_ID_ScrollSpeed, preset.ScrollSpeed);
                    _propBlock.SetFloat(_ID_NoiseScale, preset.NoiseScale);
                    _propBlock.SetFloat(_ID_VoronoiScale, preset.VoronoiScale);
                    _propBlock.SetColor(_ID_FireColor1, preset.PrimaryColor);
                    _propBlock.SetColor(_ID_FireColor2, preset.SecondaryColor);
                    _propBlock.SetColor(_ID_FireColor3, preset.TertiaryColor);
                    break;

                case ElementType.Ice:
                    _propBlock.SetFloat(_ID_IceVoronoiScale, preset.VoronoiScale);
                    _propBlock.SetFloat(_ID_EdgeThreshold, preset.EdgeThreshold);
                    _propBlock.SetColor(_ID_IceColor, preset.PrimaryColor);
                    break;

                case ElementType.Earth:
                    _propBlock.SetFloat(_ID_DitherScale, preset.DitherScale);
                    _propBlock.SetFloat(_ID_PulseSpeed, preset.PulseSpeed);
                    _propBlock.SetColor(_ID_EarthColor, preset.PrimaryColor);
                    break;
            }
        }

        _overlayRenderer.SetPropertyBlock(_propBlock);
    }

    // ──────────────────────────────────────────────────────
    //  궤적(TrailRenderer) + 잔상(GhostTrail) + 아우라 색상 동기화
    //  ★ 속성별 분기: Fire/Ice = 극한 HDR 번아웃, Earth = 무거운 흙먼지
    // ──────────────────────────────────────────────────────
    private void ApplyTrailAndGhostColors()
    {
        int idx = (int)_currentElement;
        Color aura      = _elementAuraColors[idx];
        Color hdr       = _elementHDRColors[idx];
        float glowPower = _elementGlowIntensity[idx];
        bool isEarth    = (_currentElement == ElementType.Earth);

        // ── 1. TrailRenderer 완전 초기화 후 속성 그래디언트 재할당 ──
        if (_cachedTrails != null)
        {
            foreach (var trail in _cachedTrails)
            {
                if (trail == null) continue;

                // 머티리얼 Emission/Glow 강제 덮어쓰기
                Material trailMat = trail.material;
                if (trailMat != null)
                {
                    if (trailMat.HasProperty("_EmissionColor"))
                        trailMat.SetColor(_ID_EmissionColor, hdr);
                    if (trailMat.HasProperty("_EmissionIntensity"))
                        trailMat.SetFloat(_ID_EmissionIntensity, isEarth ? 1.0f : 1.5f);
                    if (trailMat.HasProperty("_GlowColor"))
                        trailMat.SetColor(_ID_GlowColor, hdr);
                    if (trailMat.HasProperty("_GlowIntensity"))
                        trailMat.SetFloat(_ID_GlowIntensity, glowPower);
                }

                // ★ 기존 그래디언트 완전 폐기 → 속성별 새 그래디언트 생성
                Gradient g = new Gradient();
                if (isEarth)
                {
                    // 땅: 밝은 황금녹색(타격) → 묵직한 암석갈색 → 투명
                    g.SetKeys(
                        new GradientColorKey[] {
                            new GradientColorKey(new Color(0.95f, 0.85f, 0.3f), 0f),
                            new GradientColorKey(new Color(0.4f, 0.55f, 0.15f), 0.35f),
                            new GradientColorKey(new Color(0.25f, 0.18f, 0.1f), 0.7f),
                            new GradientColorKey(new Color(0.15f, 0.1f, 0.05f), 1f)
                        },
                        new GradientAlphaKey[] {
                            new GradientAlphaKey(0.8f, 0f),
                            new GradientAlphaKey(0.5f, 0.4f),
                            new GradientAlphaKey(0.0f, 1f)
                        }
                    );
                }
                else if (_currentElement == ElementType.Fire)
                {
                    // 불: 쨍한 노란불 → 붉은 주황 → 투명
                    g.SetKeys(
                        new GradientColorKey[] {
                            new GradientColorKey(new Color(1f, 0.9f, 0.3f), 0f),
                            new GradientColorKey(new Color(1f, 0.3f, 0.0f), 0.5f),
                            new GradientColorKey(new Color(0.6f, 0.05f, 0.0f), 1f)
                        },
                        new GradientAlphaKey[] {
                            new GradientAlphaKey(0.8f, 0f),
                            new GradientAlphaKey(0.5f, 0.4f),
                            new GradientAlphaKey(0.0f, 1f)
                        }
                    );
                }
                else // Ice
                {
                    // 얼음: 밝은 화이트시안 → 진한 파란색 → 투명
                    g.SetKeys(
                        new GradientColorKey[] {
                            new GradientColorKey(new Color(0.8f, 0.95f, 1f), 0f),
                            new GradientColorKey(new Color(0.1f, 0.6f, 1f), 0.5f),
                            new GradientColorKey(new Color(0.05f, 0.2f, 0.8f), 1f)
                        },
                        new GradientAlphaKey[] {
                            new GradientAlphaKey(0.8f, 0f),
                            new GradientAlphaKey(0.5f, 0.4f),
                            new GradientAlphaKey(0.0f, 1f)
                        }
                    );
                }
                trail.colorGradient = g;
            }
        }

        // ── 2. 무기 본체 SpriteRenderer 아우라 강제 덮어씌우기 ──
        if (_cachedWeaponRenderers != null)
        {
            foreach (var sr in _cachedWeaponRenderers)
            {
                if (sr == null) continue;
                if (_overlayRenderer != null && sr == _overlayRenderer) continue;

                Material mat = sr.material;
                if (mat != null)
                {
                    if (mat.HasProperty("_GlowColor"))
                        mat.SetColor(_ID_GlowColor, hdr);
                    if (mat.HasProperty("_GlowIntensity"))
                        mat.SetFloat(_ID_GlowIntensity, glowPower);
                }

                // 스프라이트 틴트
                float lerpFactor = 0.5f;
                Color tint = new Color(
                    Mathf.Lerp(aura.r, 1f, lerpFactor),
                    Mathf.Lerp(aura.g, 1f, lerpFactor),
                    Mathf.Lerp(aura.b, 1f, lerpFactor),
                    sr.color.a
                );
                sr.color = tint;
            }
        }

        // ── 3. 기존 잔상 강제 동기화 ──
        SyncExistingGhosts(idx);
    }

    /// <summary>
    /// 씬의 모든 WeaponGhost 잔상에 속성별 극한 HDR / 무게감을 강제 적용합니다.
    /// </summary>
    private void SyncExistingGhosts(int elemIndex)
    {
        Color hdr       = _elementHDRColors[elemIndex];
        Color aura      = _elementAuraColors[elemIndex];
        float glowPower = _elementGlowIntensity[elemIndex];
        bool isEarth    = (elemIndex == 0);

        var allObjs = FindObjectsOfType<SpriteRenderer>(true);
        foreach (var sr in allObjs)
        {
            if (sr == null || !sr.gameObject.name.StartsWith("WeaponGhost")) continue;

            Material ghostMat = sr.material;
            if (ghostMat != null)
            {
                if (ghostMat.HasProperty("_GlowColor"))
                    ghostMat.SetColor(_ID_GlowColor, hdr);
                if (ghostMat.HasProperty("_GlowIntensity"))
                    ghostMat.SetFloat(_ID_GlowIntensity, glowPower);
            }

            // 잔상 base color: 속성 aura 색상 적용
            sr.color = new Color(aura.r, aura.g, aura.b, sr.color.a);
        }
    }

    // ──────────────────────────────────────────────────────
    //  속성별 대표 색상 접근자 (외부 시스템 연동용)
    // ──────────────────────────────────────────────────────

    /// <summary>현재 속성의 Aura 색상을 반환합니다. (잔상/궤적 생성 시 사용)</summary>
    public Color GetCurrentAuraColor()
    {
        return _elementAuraColors[(int)_currentElement];
    }

    /// <summary>현재 속성의 HDR Emission 색상을 반환합니다.</summary>
    public Color GetCurrentHDRColor()
    {
        return _elementHDRColors[(int)_currentElement];
    }
}
