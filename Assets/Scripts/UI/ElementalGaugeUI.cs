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
    [Tooltip("게이지 상태를 반영할 UI 이미지 목록")]
    [SerializeField] private Image[] _gaugeImages;

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
        if (_weaponSystem == null)
        {
            _weaponSystem = GetComponentInParent<ElementalWeaponSystem>();
            if (_weaponSystem == null)
            {
                _weaponSystem = ElementalWeaponSystem.Instance;
            }
            if (_weaponSystem != null)
            {
                _weaponSystem.OnGaugesChanged += UpdateGaugeUI;
            }
        }

        // 시작 시 초기값 반영
        if (_weaponSystem != null)
        {
            float[] initialRatios = new float[3];
            for(int i=0; i<3; i++)
            {
                initialRatios[i] = _weaponSystem.CurrentGauges[i] / 100f; // GAUGE_MAX is 100
            }
            UpdateGaugeUI(initialRatios);
        }
    }

    private void OnEnable()
    {
        if (_weaponSystem != null)
        {
            // 이벤트 구독
            _weaponSystem.OnGaugesChanged += UpdateGaugeUI;
        }
    }

    private void OnDisable()
    {
        if (_weaponSystem != null)
        {
            // 메모리 누수 방지를 위한 구독 해제
            _weaponSystem.OnGaugesChanged -= UpdateGaugeUI;
        }
    }

    /// <summary>
    /// 게이지 이벤트에 반응하여 UI를 업데이트합니다.
    /// </summary>
    /// <param name="fillRatios">현재 게이지의 비율 배열 (0.0 ~ 1.0)</param>
    private void UpdateGaugeUI(float[] fillRatios)
    {
        if (_gaugeImages == null || _weaponSystem == null || fillRatios == null) return;

        // 현재 선택된 무기 속성의 인덱스를 가져옵니다 (Earth=0, Fire=1, Ice=2)
        int activeIndex = (int)_weaponSystem.CurrentElement;

        for (int i = 0; i < _gaugeImages.Length; i++)
        {
            if (_gaugeImages[i] != null)
            {
                // 각 속성의 실제 게이지 값을 반영합니다
                if (i < fillRatios.Length)
                {
                    _gaugeImages[i].fillAmount = fillRatios[i];
                }

                // 활성화/비활성화 시각적 효과만 변경
                if (i == activeIndex)
                {
                    // 활성화된 상태를 시각적으로 보여주기 위해 불투명도 100%
                    _gaugeImages[i].color = new Color(_gaugeImages[i].color.r, _gaugeImages[i].color.g, _gaugeImages[i].color.b, 1f);
                }
                else
                {
                    // 비활성화된 상태를 시각적으로 보여주기 위해 반투명 처리
                    _gaugeImages[i].color = new Color(_gaugeImages[i].color.r, _gaugeImages[i].color.g, _gaugeImages[i].color.b, 0.3f);
                }
            }
        }
    }
}
