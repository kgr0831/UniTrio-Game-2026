using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 현재 무기 속성에 따라 속성 아이콘 스프라이트를 표시하는 뷰.
/// ElementalWeaponSystem.OnGaugeChanged(속성 포함)를 구독해 Earth/Fire/Ice 아이콘을 스왑합니다. (SRP)
/// </summary>
public class ElementIconUI : MonoBehaviour
{
    [Header("References (비우면 자동 탐색)")]
    [SerializeField] private Image _iconImage;
    [SerializeField] private ElementalWeaponSystem _elementSystem;

    [Header("속성 아이콘 (비우면 Resources/Icons 에서 로드)")]
    [SerializeField] private Sprite _earthIcon;
    [SerializeField] private Sprite _fireIcon;
    [SerializeField] private Sprite _iceIcon;

    private void Awake()
    {
        if (_iconImage == null) _iconImage = GetComponent<Image>();

        if (_elementSystem == null) _elementSystem = ElementalWeaponSystem.Instance;
        if (_elementSystem == null) _elementSystem = FindObjectOfType<ElementalWeaponSystem>();

        if (_earthIcon == null) _earthIcon = Resources.Load<Sprite>("Icons/EarthIcon");
        if (_fireIcon  == null) _fireIcon  = Resources.Load<Sprite>("Icons/FireIcon");
        if (_iceIcon   == null) _iceIcon   = Resources.Load<Sprite>("Icons/IceIcon");
    }

    private void OnEnable()
    {
        if (_elementSystem != null) _elementSystem.OnGaugeChanged += OnElementGaugeChanged;
    }

    private void OnDisable()
    {
        if (_elementSystem != null) _elementSystem.OnGaugeChanged -= OnElementGaugeChanged;
    }

    private void Start()
    {
        if (_elementSystem != null) Apply(_elementSystem.CurrentElement);
    }

    private void OnElementGaugeChanged(float ratio, ElementType element) => Apply(element);

    private void Apply(ElementType element)
    {
        if (_iconImage == null) return;
        switch (element)
        {
            case ElementType.Earth: if (_earthIcon != null) _iconImage.sprite = _earthIcon; break;
            case ElementType.Fire:  if (_fireIcon  != null) _iconImage.sprite = _fireIcon;  break;
            case ElementType.Ice:   if (_iceIcon   != null) _iconImage.sprite = _iceIcon;   break;
        }
    }
}
