using UnityEngine;

/// <summary>
/// 잔상 VFX 1개. Setup() 호출 시 활성화, FadeDuration 후 자동 비활성화.
/// VFXLit2D 셰이더의 _Alpha와 _EmissionIntensity를 MaterialPropertyBlock으로 애니메이션.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class AfterimageGhost : MonoBehaviour
{
    private const float FadeDuration = 0.3f;
    private const float InitialAlpha = 0.85f;
    private const float InitialEmission = 2f;

    private static readonly int AlphaID = Shader.PropertyToID("_Alpha");
    private static readonly int EmissionIntensityID = Shader.PropertyToID("_EmissionIntensity");

    private SpriteRenderer       _sr;
    private MaterialPropertyBlock _mpb;
    private float                 _timer;

    private void Awake()
    {
        _sr  = GetComponent<SpriteRenderer>();
        _mpb = new MaterialPropertyBlock();
    }

    public void Setup(Sprite sprite, Vector3 worldPos, Vector3 worldScale,
                      Color baseColor, int sortingLayerID, int sortingOrder, bool flipX = false)
    {
        transform.position   = worldPos;
        transform.localScale = worldScale;

        _sr.sprite         = sprite;
        _sr.flipX          = flipX;
        _sr.sortingLayerID = sortingLayerID;
        _sr.sortingOrder   = sortingOrder - 1;

        baseColor.a = InitialAlpha;
        _sr.color   = baseColor;

        _sr.GetPropertyBlock(_mpb);
        _mpb.SetFloat(AlphaID, 1f);
        _mpb.SetFloat(EmissionIntensityID, InitialEmission);
        _sr.SetPropertyBlock(_mpb);

        _timer = 0f;
        gameObject.SetActive(true);
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        float t = _timer / FadeDuration;

        Color c = _sr.color;
        c.a       = Mathf.Lerp(InitialAlpha, 0f, t);
        _sr.color = c;

        _sr.GetPropertyBlock(_mpb);
        _mpb.SetFloat(AlphaID, Mathf.Lerp(1f, 0f, t));
        _mpb.SetFloat(EmissionIntensityID, Mathf.Lerp(InitialEmission, 0f, t));
        _sr.SetPropertyBlock(_mpb);

        if (_timer >= FadeDuration)
            gameObject.SetActive(false);
    }
}
