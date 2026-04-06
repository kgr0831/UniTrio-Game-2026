using UnityEngine;

/// <summary>
/// 잔상 VFX 1개. Setup() 호출 시 활성화, FadeDuration 후 자동 비활성화.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class AfterimageGhost : MonoBehaviour
{
    private const float FadeDuration = 0.3f;
    private const float InitialAlpha = 0.85f;

    private SpriteRenderer _sr;
    private float          _timer;

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
    }

    public void Setup(Sprite sprite, Vector3 worldPos, Vector3 worldScale,
                      Color baseColor, int sortingLayerID, int sortingOrder)
    {
        transform.position   = worldPos;
        transform.localScale = worldScale;

        _sr.sprite         = sprite;
        _sr.sortingLayerID = sortingLayerID;
        _sr.sortingOrder   = sortingOrder - 1;

        baseColor.a = InitialAlpha;
        _sr.color   = baseColor;

        _timer = 0f;
        gameObject.SetActive(true);
    }

    private void Update()
    {
        _timer += Time.deltaTime;

        Color c = _sr.color;
        c.a       = Mathf.Lerp(InitialAlpha, 0f, _timer / FadeDuration);
        _sr.color = c;

        if (_timer >= FadeDuration)
            gameObject.SetActive(false);
    }
}
