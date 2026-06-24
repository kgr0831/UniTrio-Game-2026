using UnityEngine;

/// <summary>
/// 불빛 일렁임 효과. Perlin 노이즈로 Light의 강도와 범위를 흔들어
/// 화톳불·횃불 같은 자연스러운 불꽃 광원을 연출합니다. (GC 제로)
/// </summary>
[RequireComponent(typeof(Light))]
public class LightFlicker : MonoBehaviour
{
    [Header("강도")]
    [SerializeField, Min(0f)] private float _baseIntensity = 2.2f;
    [Tooltip("기본 강도 대비 흔들림 폭 (±)")]
    [SerializeField, Range(0f, 1f)] private float _intensityJitter = 0.25f;

    [Header("범위")]
    [SerializeField, Min(0f)] private float _baseRange = 6f;
    [SerializeField, Range(0f, 1f)] private float _rangeJitter = 0.08f;

    [Header("속도")]
    [Tooltip("노이즈 스크롤 속도. 클수록 빠르게 일렁임")]
    [SerializeField, Min(0.01f)] private float _flickerSpeed = 3f;

    private Light _light;
    private float _seed;

    private void Awake()
    {
        _light = GetComponent<Light>();
        _seed  = Random.Range(0f, 100f); // 광원마다 다른 위상으로 일렁이도록
    }

    private void Update()
    {
        float noise = Mathf.PerlinNoise(_seed, Time.time * _flickerSpeed) * 2f - 1f; // -1 ~ 1

        _light.intensity = _baseIntensity * (1f + noise * _intensityJitter);
        _light.range     = _baseRange    * (1f + noise * _rangeJitter);
    }
}
