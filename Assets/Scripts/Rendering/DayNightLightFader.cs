using UnityEngine;

/// <summary>
/// DayNightManager의 NightFactor에 따라 Light 강도를 페이드합니다.
/// 낮에는 꺼지고(0) 밤이 깊어질수록 최대 강도까지 밝아집니다.
/// 플레이어 주변광처럼 '밤에만 의미 있는' 광원에 부착하세요.
/// 매니저가 없는 씬에서는 항상 최대 강도로 동작합니다.
/// </summary>
[RequireComponent(typeof(Light))]
public class DayNightLightFader : MonoBehaviour
{
    [Tooltip("완전한 밤일 때의 광원 강도")]
    [SerializeField, Min(0f)] private float _maxIntensity = 0.8f;

    private Light _light;

    private void Awake()
    {
        _light = GetComponent<Light>();
    }

    private void Update()
    {
        float nightFactor = DayNightManager.Instance != null
            ? DayNightManager.Instance.NightFactor
            : 1f;

        _light.intensity = _maxIntensity * nightFactor;
    }
}
