using UnityEngine;

public class PortalController : MonoBehaviour
{
    [Header("Particle Systems")]
    public ParticleSystem rimParticle;
    public ParticleSystem sparkParticle;

    [Header("Scale Settings")]
    public float startScale = 0.1f;    // 시작 스케일
    public float targetScale = 2.5f;   // 최종 스케일
    public float expandDuration = 10.0f; 
    private float timer = 0f;

    [Header("Color Range Settings")]
    [Tooltip("원하는 중심 색상을 고르세요")]
    public Color baseColor = Color.red; 

    void Start()
    {
        // 시작 시 스케일 초기화
        transform.localScale = Vector3.one * startScale;

        // 색상 적용 (Hue 기반 30도 범위)
        ApplyHueGradient(rimParticle, baseColor);
        ApplyHueGradient(sparkParticle, baseColor);
    }

    void Update()
    {
        // 1. 스케일을 XY로 균일하게 확장 (Z는 보통 1 유지)
        if (timer < expandDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / expandDuration;
            
            float currentScale = Mathf.Lerp(startScale, targetScale, progress);
            transform.localScale = new Vector3(currentScale, currentScale, 1f);
        }
    }

    // 색상 팔레트의 Hue(색상) 값을 기준으로 그라데이션을 생성
    void ApplyHueGradient(ParticleSystem ps, Color centerColor)
    {
        var colorModule = ps.colorOverLifetime;
        colorModule.enabled = true;

        float h, s, v;
        Color.RGBToHSV(centerColor, out h, out s, out v);

        // 색상 범위를 30도에서 50도로 확장 (더 노란/연한 색까지 도달)
        float hueOffset = 50f / 360f;
    
        // 시작 색상: 선택한 원본 색상
        Color startColor = centerColor; 

        // 끝 색상: Hue를 50도 옮기고, 채도(s)를 20% 정도 낮춰서 더 연하게 만듦
        Color endColor = Color.HSVToRGB(Mathf.Repeat(h + hueOffset, 1f), s * 0.8f, v); 

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { 
                new GradientColorKey(startColor, 0.0f), // 0% 지점: 원본 (빨강)
                new GradientColorKey(endColor, 0.6f)    // 60% 지점: 연해진 색 (노랑/연주황)
            },
            new GradientAlphaKey[] { 
                new GradientAlphaKey(1.0f, 0.0f), // 시작은 진하게
                new GradientAlphaKey(1.0f, 0.5f), // 중간까지 유지
                new GradientAlphaKey(0.0f, 1.0f)  // 끝에서 소멸
            }
        );

        colorModule.color = gradient;
    }
}