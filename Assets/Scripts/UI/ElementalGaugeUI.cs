using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 무기 속성 게이지 UI를 전담하는 클래스.
/// SRP를 준수하여 UI 업데이트 기능만 처리하며, ElementalWeaponSystem의 이벤트에 반응합니다.
/// </summary>
public class ElementalGaugeUI : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("게이지 데이터 및 이벤트를 제공할 ElementalWeaponSystem")]
    [SerializeField] private ElementalWeaponSystem _weaponSystem;

    [Header("UI References")]
    [Tooltip("게이지 상태를 반영할 단일 UI 이미지")]
    [SerializeField] private Image _gaugeImage;
    [Tooltip("게이지의 그라데이션을 조절할 UIGradient 컴포넌트")]
    [SerializeField] private UIGradient _gaugeGradient;

    [Header("Gradient Colors")]
    [Tooltip("대지(Earth) - 왼쪽(연함)")]
    [SerializeField] private Color _earthLight = new Color(1f, 0.95f, 0.6f);
    [Tooltip("대지(Earth) - 오른쪽(진함)")]
    [SerializeField] private Color _earthDark = new Color(0.7f, 0.5f, 0.1f);
    
    [Tooltip("화염(Fire) - 왼쪽(연함)")]
    [SerializeField] private Color _fireLight = new Color(1f, 0.8f, 0.3f);
    [Tooltip("화염(Fire) - 오른쪽(진함)")]
    [SerializeField] private Color _fireDark = new Color(0.9f, 0.2f, 0.05f);
    
    [Tooltip("얼음(Ice) - 왼쪽(연함)")]
    [SerializeField] private Color _iceLight = new Color(0.7f, 0.9f, 1f);
    [Tooltip("얼음(Ice) - 오른쪽(진함)")]
    [SerializeField] private Color _iceDark = new Color(0.1f, 0.4f, 0.9f);

    private void Awake()
    {
        // _weaponSystem이 인스펙터에서 할당되지 않았다면 자동 탐색
        if (_weaponSystem == null)
        {
            _weaponSystem = GetComponentInParent<ElementalWeaponSystem>();
            if (_weaponSystem == null)
            {
                _weaponSystem = ElementalWeaponSystem.Instance;
            }
        }
    }

    private void Start()
    {
        if (_weaponSystem != null)
        {
            float initialRatio = _weaponSystem.CurrentGauge / 300f; // GAUGE_MAX is 300
            UpdateGaugeUI(initialRatio, _weaponSystem.CurrentElement);
        }
    }

    private void OnEnable()
    {
        if (_weaponSystem != null)
        {
            // 이벤트 구독
            _weaponSystem.OnGaugeChanged += UpdateGaugeUI;
        }
    }

    private void OnDisable()
    {
        if (_weaponSystem != null)
        {
            // 메모리 누수 방지를 위한 구독 해제
            _weaponSystem.OnGaugeChanged -= UpdateGaugeUI;
        }
    }

    /// <summary>
    /// 게이지 이벤트에 반응하여 UI를 업데이트합니다.
    /// </summary>
    /// <param name="fillRatio">현재 게이지의 비율 (0.0 ~ 1.0)</param>
    /// <param name="currentElement">현재 선택된 속성</param>
    private void UpdateGaugeUI(float fillRatio, ElementType currentElement)
    {
        if (_gaugeImage == null || _weaponSystem == null) return;

        // 게이지 채우기 업데이트
        _gaugeImage.fillAmount = fillRatio;

        // 속성에 따른 그라데이션 색상 업데이트
        if (_gaugeGradient != null)
        {
            switch (currentElement)
            {
                case ElementType.Earth:
                    _gaugeGradient.colorBottom = _earthLight;
                    _gaugeGradient.colorTop = _earthDark;
                    break;
                case ElementType.Fire:
                    _gaugeGradient.colorBottom = _fireLight;
                    _gaugeGradient.colorTop = _fireDark;
                    break;
                case ElementType.Ice:
                    _gaugeGradient.colorBottom = _iceLight;
                    _gaugeGradient.colorTop = _iceDark;
                    break;
            }
            
            // 변경된 색상 즉시 반영을 위해 메쉬 갱신 플래그 세팅
            _gaugeImage.SetVerticesDirty();
        }
    }
}
