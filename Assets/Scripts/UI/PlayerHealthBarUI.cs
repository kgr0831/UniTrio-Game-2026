using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어 HP바 뷰. HealthSystem.OnHpChanged 를 구독해
/// Filled(Horizontal) 이미지의 fillAmount를 현재/최대 HP 비율로 갱신합니다. (SRP)
/// </summary>
public class PlayerHealthBarUI : MonoBehaviour
{
    [Header("References (비우면 자동 탐색)")]
    [Tooltip("Image Type=Filled, Horizontal 로 설정된 채움 이미지")]
    [SerializeField] private Image _fillImage;
    [Tooltip("플레이어의 HealthSystem")]
    [SerializeField] private HealthSystem _health;

    private void Awake()
    {
        if (_fillImage == null) _fillImage = GetComponent<Image>();
        if (_health == null) ResolveHealth();
    }

    private void ResolveHealth()
    {
        var pe = FindObjectOfType<PlayerEntity>();
        if (pe != null) _health = pe.GetComponent<HealthSystem>();
    }

    private void OnEnable()
    {
        if (_health != null) _health.OnHpChanged += UpdateBar;
    }

    private void OnDisable()
    {
        if (_health != null) _health.OnHpChanged -= UpdateBar;
    }

    private void Start()
    {
        // 늦게 생성된 경우 대비해 한 번 더 시도
        if (_health == null)
        {
            ResolveHealth();
            if (_health != null) _health.OnHpChanged += UpdateBar;
        }
        if (_health != null) UpdateBar(_health.CurrentHp, _health.MaxHp);
    }

    private void UpdateBar(float current, float max)
    {
        if (_fillImage == null) return;
        _fillImage.fillAmount = max > 0f ? Mathf.Clamp01(current / max) : 0f;
    }
}
