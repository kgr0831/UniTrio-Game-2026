using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// 화면 상단에 경고 메시지를 표시하고, 아래로 약간 내려가며 페이드 아웃되는 UI 컴포넌트.
/// </summary>
public class NotificationUI : MonoBehaviour
{
    public static NotificationUI Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI _messageText;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private RectTransform _messagePanel;

    [Header("Settings")]
    [SerializeField] private float _displayDuration = 1.0f;
    [SerializeField] private float _fadeDuration = 0.5f;
    [SerializeField] private float _moveDistance = 30f;

    private Vector2 _initialPosition;
    private Coroutine _currentCoroutine;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        if (_messagePanel != null) _initialPosition = _messagePanel.anchoredPosition;
    }

    public void ShowMessage(string message)
    {
        if (_messageText == null || _canvasGroup == null || _messagePanel == null) return;

        if (_currentCoroutine != null) StopCoroutine(_currentCoroutine);
        _currentCoroutine = StartCoroutine(ShowRoutine(message));
    }

    private IEnumerator ShowRoutine(string message)
    {
        _messageText.text = message;
        _canvasGroup.alpha = 1f;
        _messagePanel.anchoredPosition = _initialPosition;

        yield return new WaitForSeconds(_displayDuration);

        float elapsed = 0f;
        while (elapsed < _fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / _fadeDuration;

            _canvasGroup.alpha = 1f - t;
            _messagePanel.anchoredPosition = _initialPosition + new Vector2(0, -t * _moveDistance);

            yield return null;
        }

        _canvasGroup.alpha = 0f;
        _messagePanel.anchoredPosition = _initialPosition;
        _currentCoroutine = null;
    }
}
