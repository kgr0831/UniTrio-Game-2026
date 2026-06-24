using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ScreenFlashEffect : MonoBehaviour
{
    public static ScreenFlashEffect Instance { get; private set; }

    [SerializeField] private Image flashImage;

    private Coroutine _activeFlash;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (flashImage == null)
        {
            CreateFlashUI();
        }
        flashImage.color = Color.clear;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void CreateFlashUI()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        var imgGO = new GameObject("FlashImage");
        imgGO.transform.SetParent(transform, false);

        flashImage = imgGO.AddComponent<Image>();
        flashImage.color = Color.clear;
        flashImage.raycastTarget = false;

        var rect = flashImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    public void Flash(Color color, float duration = 0.15f)
    {
        if (_activeFlash != null) StopCoroutine(_activeFlash);
        _activeFlash = StartCoroutine(FlashRoutine(color, duration));
    }

    private IEnumerator FlashRoutine(Color color, float duration)
    {
        flashImage.color = color;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Lerp(color.a, 0f, elapsed / duration);
            flashImage.color = new Color(color.r, color.g, color.b, alpha);
            yield return null;
        }
        flashImage.color = Color.clear;
        _activeFlash = null;
    }
}
