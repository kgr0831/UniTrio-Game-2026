using System.Collections;
using UnityEngine;

public class GroundCrackDecal : MonoBehaviour
{
    [SerializeField] private float fadeDuration = 1f;
    [SerializeField] private float stayDuration = 1f;

    private SpriteRenderer _sr;

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        StartCoroutine(LifecycleRoutine());
    }

    private IEnumerator LifecycleRoutine()
    {
        // pop-in: 즉시 나타남
        _sr.color = new Color(_sr.color.r, _sr.color.g, _sr.color.b, 1f);

        yield return new WaitForSeconds(stayDuration);

        // fade out
        float elapsed = 0f;
        Color c = _sr.color;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float a = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            _sr.color = new Color(c.r, c.g, c.b, a);
            yield return null;
        }

        Destroy(gameObject);
    }
}
