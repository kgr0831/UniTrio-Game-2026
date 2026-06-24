using UnityEngine;

/// <summary>
/// 속성별 셰이더 파라미터 프리셋 (ScriptableObject).
/// Earth / Fire / Ice 각각 하나씩 생성하여 ElementalWeaponSystem에 할당합니다.
/// </summary>
[CreateAssetMenu(fileName = "NewElementalPreset", menuName = "UniTrio/Elemental Shader Data")]
public class ElementalShaderData : ScriptableObject
{
    public ElementType ElementType;

    [Header("Colors (HDR)")]
    [ColorUsage(true, true)]
    public Color PrimaryColor   = Color.white;
    [ColorUsage(true, true)]
    public Color SecondaryColor = Color.gray;
    [ColorUsage(true, true)]
    public Color TertiaryColor  = Color.black;

    [Header("Effect Parameters")]
    [Range(0f, 3f)]   public float EffectIntensity  = 1.0f;
    [Range(1f, 64f)]  public float PixelResolution  = 32.0f;
    [Range(1f, 16f)]  public float NoiseScale       = 4.0f;
    [Range(1f, 16f)]  public float VoronoiScale     = 6.0f;
    [Range(0f, 5f)]   public float ScrollSpeed      = 1.5f;
    [Range(0f, 0.5f)] public float EdgeThreshold    = 0.15f;
    [Range(1f, 8f)]   public float DitherScale      = 4.0f;
    [Range(0f, 5f)]   public float PulseSpeed       = 2.0f;
}
