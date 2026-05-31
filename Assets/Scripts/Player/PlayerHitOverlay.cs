using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어 피격 시 화면 전체를 반투명 빨간색으로 덮은 뒤 페이드 아웃시킵니다.
/// PlayerStateMachine.Start()에서 자동 생성되므로 프리팹 불필요.
/// </summary>
public class PlayerHitOverlay : MonoBehaviour
{
    private Image _overlay;
    private float _timer;

    private const float FADE_DURATION = 0.45f;
    private static readonly Color HIT_COLOR = new Color(1f, 0f, 0f, 0.4f);

    public static PlayerHitOverlay Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        BuildOverlay();
    }

    private void BuildOverlay()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        gameObject.AddComponent<CanvasScaler>();

        GameObject imgObj = new GameObject("RedOverlay");
        imgObj.transform.SetParent(transform, false);

        _overlay = imgObj.AddComponent<Image>();
        _overlay.color = Color.clear;
        _overlay.raycastTarget = false;

        RectTransform rt = _overlay.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>피격 시 호출 — 빨간 오버레이를 보여주고 페이드 아웃 시작.</summary>
    public void TriggerHit()
    {
        _timer = FADE_DURATION;
        if (_overlay != null) _overlay.color = HIT_COLOR;
    }

    private void Update()
    {
        if (_timer <= 0f) return;

        _timer -= Time.deltaTime;
        float alpha = Mathf.Clamp01(_timer / FADE_DURATION);
        // 제곱 감쇠로 빠르게 사라지는 느낌
        if (_overlay != null)
            _overlay.color = new Color(1f, 0f, 0f, 0.4f * (alpha * alpha));

        if (_timer <= 0f && _overlay != null)
            _overlay.color = Color.clear;
    }
}
