using UnityEngine;

/// <summary>
/// HP 바 왼쪽 아래에 디버프 아이콘을 표시합니다 (SRP).
/// DebuffReceiver의 이벤트를 구독하여 아이콘을 추가/제거/갱신합니다.
/// 
/// 각 아이콘은 SpriteRenderer + DebuffIconWipe 쉐이더로 구성되며,
/// 지속시간에 따라 시계방향으로 반투명화됩니다.
/// 디버프 중첩 시 왼쪽에서 오른쪽으로 정렬됩니다.
/// </summary>
[RequireComponent(typeof(DebuffReceiver))]
[RequireComponent(typeof(MonsterHPBar))]
public sealed class DebuffIconDisplay : MonoBehaviour
{
    [Header("Icon Settings")]
    [Tooltip("아이콘 크기 (유닛)")]
    [SerializeField] private float _iconSize = 0.2f;
    [Tooltip("아이콘 간 간격")]
    [SerializeField] private float _iconSpacing = 0.02f;

    // 컴포넌트 캐시
    private DebuffReceiver _debuffReceiver;
    private MonsterHPBar _hpBar;

    // 아이콘 오브젝트 (인덱스 = ElementType 값)
    private GameObject[] _iconObjects = new GameObject[3];
    private SpriteRenderer[] _iconRenderers = new SpriteRenderer[3];
    private Material[] _iconMaterials = new Material[3];
    private MaterialPropertyBlock[] _iconMPBs = new MaterialPropertyBlock[3];

    // 아이콘 스프라이트 캐시 (Resources에서 로드)
    private static Sprite _fireSprite;
    private static Sprite _iceSprite;
    private static Sprite _earthSprite;
    private static bool _spritesLoaded;

    // 쉐이더 참조
    private static Shader _wipeShader;
    private static readonly int _ID_Progress = Shader.PropertyToID("_Progress");

    private void Awake()
    {
        _debuffReceiver = GetComponent<DebuffReceiver>();
        _hpBar = GetComponent<MonsterHPBar>();

        if (_wipeShader == null)
            _wipeShader = Shader.Find("Custom/DebuffIconWipe");

        LoadSprites();

        for (int i = 0; i < 3; i++)
            _iconMPBs[i] = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        if (_debuffReceiver != null)
        {
            _debuffReceiver.OnDebuffApplied   += OnDebuffApplied;
            _debuffReceiver.OnDebuffRemoved   += OnDebuffRemoved;
            _debuffReceiver.OnDebuffRefreshed += OnDebuffRefreshed;
        }
    }

    private void OnDisable()
    {
        if (_debuffReceiver != null)
        {
            _debuffReceiver.OnDebuffApplied   -= OnDebuffApplied;
            _debuffReceiver.OnDebuffRemoved   -= OnDebuffRemoved;
            _debuffReceiver.OnDebuffRefreshed -= OnDebuffRefreshed;
        }

        // 모든 아이콘 숨김
        for (int i = 0; i < 3; i++)
        {
            if (_iconObjects[i] != null)
                _iconObjects[i].SetActive(false);
        }
    }

    private void Update()
    {
        // 활성 디버프의 wipe 진행도를 매 프레임 갱신
        IDebuff[] debuffs = _debuffReceiver.GetActiveDebuffs();
        for (int i = 0; i < 3; i++)
        {
            if (_iconRenderers[i] == null || _iconObjects[i] == null) continue;
            if (!_iconObjects[i].activeSelf) continue;

            IDebuff debuff = debuffs[i];
            if (debuff == null || !debuff.IsActive)
            {
                _iconObjects[i].SetActive(false);
                continue;
            }

            // Progress: 0(풀) → 1(소멸) = 1 - (remaining / duration)
            float progress = 1f - Mathf.Clamp01(debuff.RemainingTime / debuff.Duration);

            _iconRenderers[i].GetPropertyBlock(_iconMPBs[i]);
            _iconMPBs[i].SetFloat(_ID_Progress, progress);
            _iconRenderers[i].SetPropertyBlock(_iconMPBs[i]);
        }

        // 아이콘 위치 갱신 (HP 바 기준)
        UpdateIconPositions();
    }

    // ── 이벤트 핸들러 ──────────────────────────────────────

    private void OnDebuffApplied(ElementType element)
    {
        int index = (int)element;
        EnsureIcon(index);
        _iconObjects[index].SetActive(true);
        RearrangeIcons();
    }

    private void OnDebuffRemoved(ElementType element)
    {
        int index = (int)element;
        if (_iconObjects[index] != null)
            _iconObjects[index].SetActive(false);
        RearrangeIcons();
    }

    private void OnDebuffRefreshed(ElementType element)
    {
        // 아이콘이 없으면 생성
        int index = (int)element;
        EnsureIcon(index);
        _iconObjects[index].SetActive(true);
    }

    // ── 아이콘 생성 ────────────────────────────────────────

    private void EnsureIcon(int index)
    {
        if (_iconObjects[index] != null) return;

        Sprite iconSprite = GetSpriteForElement((ElementType)index);
        if (iconSprite == null) return;

        // HP 바의 루트를 부모로 사용
        Transform parent = _hpBar != null && _hpBar.BarRoot != null ? _hpBar.BarRoot : transform;

        GameObject iconObj = new GameObject($"DebuffIcon_{(ElementType)index}");
        iconObj.transform.SetParent(parent, false);

        SpriteRenderer sr = iconObj.AddComponent<SpriteRenderer>();
        sr.sprite = iconSprite;
        sr.sortingLayerName = "UI";
        sr.sortingOrder = 102;

        // 스프라이트의 실제 월드 크기를 기준으로 목표 크기에 맞게 스케일 계산
        float spriteWorldSize = Mathf.Max(iconSprite.bounds.size.x, iconSprite.bounds.size.y);
        float scale = spriteWorldSize > 0.001f ? _iconSize / spriteWorldSize : _iconSize;
        iconObj.transform.localScale = new Vector3(scale, scale, 1f);

        // DebuffIconWipe 쉐이더 머티리얼
        if (_wipeShader != null)
        {
            Material mat = new Material(_wipeShader);
            sr.material = mat;
            _iconMaterials[index] = mat;
        }

        _iconObjects[index] = iconObj;
        _iconRenderers[index] = sr;
        iconObj.SetActive(false);
    }

    // ── 아이콘 위치 갱신 ───────────────────────────────────

    private void UpdateIconPositions()
    {
        if (_hpBar == null || _hpBar.BarRoot == null) return;

        // HP 바의 왼쪽 아래 기준점
        float barLeft = -_hpBar.BarWidth * 0.5f;
        float barBottom = -_hpBar.BarHeight * 0.5f - _iconSize * 0.5f - 0.02f;

        int activeCount = 0;
        for (int i = 0; i < 3; i++)
        {
            if (_iconObjects[i] == null || !_iconObjects[i].activeSelf) continue;

            // 스케일 갱신 (인스펙터에서 _iconSize 변경 시 실시간 반영)
            Sprite spr = _iconRenderers[i] != null ? _iconRenderers[i].sprite : null;
            if (spr != null)
            {
                float spriteWorldSize = Mathf.Max(spr.bounds.size.x, spr.bounds.size.y);
                float scale = spriteWorldSize > 0.001f ? _iconSize / spriteWorldSize : _iconSize;
                _iconObjects[i].transform.localScale = new Vector3(scale, scale, 1f);
            }

            float x = barLeft + activeCount * (_iconSize + _iconSpacing) + _iconSize * 0.5f;
            _iconObjects[i].transform.localPosition = new Vector3(x, barBottom, 0f);
            activeCount++;
        }
    }

    private void RearrangeIcons()
    {
        UpdateIconPositions();
    }

    // ── 스프라이트 로드 ────────────────────────────────────

    private static void LoadSprites()
    {
        if (_spritesLoaded) return;

        _fireSprite  = Resources.Load<Sprite>("Icons/FireIcon");
        _iceSprite   = Resources.Load<Sprite>("Icons/IceIcon");
        _earthSprite = Resources.Load<Sprite>("Icons/EarthIcon");

        _spritesLoaded = true;
    }

    private static Sprite GetSpriteForElement(ElementType element)
    {
        switch (element)
        {
            case ElementType.Fire:  return _fireSprite;
            case ElementType.Ice:   return _iceSprite;
            case ElementType.Earth: return _earthSprite;
            default:                return null;
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < 3; i++)
        {
            if (_iconObjects[i] != null)
                Destroy(_iconObjects[i]);
            if (_iconMaterials[i] != null)
                Destroy(_iconMaterials[i]);
        }
    }
}
