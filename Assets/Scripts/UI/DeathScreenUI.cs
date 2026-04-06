using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 죽음 화면 UI 컨트롤러.
/// - 검은 패널 페이드인 후 "Dead" 텍스트와 "부활" 버튼 표시
/// - "부활" 버튼: 커서 포커스인 → 붉은색, 포커스아웃 → 흰색
/// - 버튼 클릭 시 OnResurrectClicked 이벤트 발생
/// - timeScale=0 환경에서도 동작 (unscaledTime 사용)
/// </summary>
public class DeathScreenUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CanvasGroup _rootCanvasGroup;
    [SerializeField] private Image       _blackPanel;
    [SerializeField] private TextMeshProUGUI _deadText;
    [SerializeField] private Button          _resurrectButton;
    [SerializeField] private TextMeshProUGUI _resurrectButtonText;

    [Header("Fade Settings")]
    [SerializeField] private float _panelFadeDuration  = 0.8f;  // 검은 패널 페이드인
    [SerializeField] private float _textFadeDelay      = 0.5f;  // 패널 페이드 완료 후 텍스트/버튼 등장까지 대기
    [SerializeField] private float _textFadeDuration   = 0.5f;

    [Header("Colors")]
    [SerializeField] private Color _buttonNormalColor = Color.white;
    [SerializeField] private Color _buttonHoverColor  = new Color(0.85f, 0.1f, 0.1f, 1f);

    /// <summary>부활 버튼 클릭 시 발생하는 이벤트.</summary>
    public event Action OnResurrectClicked;

    private Coroutine _fadeCoroutine;

    // ── 초기화 ────────────────────────────────────────────

    private void Awake()
    {
        // 시작 시 완전히 숨김
        if (_rootCanvasGroup != null) _rootCanvasGroup.alpha = 0f;
        if (_blackPanel      != null) SetAlpha(_blackPanel, 0f);
        if (_deadText        != null) SetAlpha(_deadText,   0f);
        if (_resurrectButton != null)
        {
            SetAlpha(_resurrectButton.targetGraphic, 0f);
            if (_resurrectButtonText != null) SetAlpha(_resurrectButtonText, 0f);
        }

        gameObject.SetActive(false);
    }

    // ── Public API ─────────────────────────────────────────

    /// <summary>죽음 UI를 표시합니다. timeScale=0에서 호출 가능.</summary>
    public void Show()
    {
        gameObject.SetActive(true);
        if (_rootCanvasGroup != null) _rootCanvasGroup.alpha = 1f;

        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeInSequence());
    }

    /// <summary>죽음 UI를 즉시 숨깁니다.</summary>
    public void Hide()
    {
        if (_fadeCoroutine != null) { StopCoroutine(_fadeCoroutine); _fadeCoroutine = null; }
        gameObject.SetActive(false);
    }

    // ── 버튼 이벤트 (Inspector에서도 연결 가능) ──────────

    public void OnResurrectButtonClicked()
    {
        OnResurrectClicked?.Invoke();
    }

    // ── 호버 효과 (EventTrigger 대신 코드로 처리) ────────

    public void OnButtonPointerEnter()
    {
        if (_resurrectButtonText != null)
            _resurrectButtonText.color = _buttonHoverColor;
    }

    public void OnButtonPointerExit()
    {
        if (_resurrectButtonText != null)
        {
            Color c = _buttonNormalColor;
            c.a = _resurrectButtonText.color.a; // 알파 유지
            _resurrectButtonText.color = c;
        }
    }

    // ── 페이드인 시퀀스 ───────────────────────────────────

    private IEnumerator FadeInSequence()
    {
        // 1. 검은 패널 페이드인
        float elapsed = 0f;
        while (elapsed < _panelFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / _panelFadeDuration);
            if (_blackPanel != null) SetAlpha(_blackPanel, t);
            yield return null;
        }
        if (_blackPanel != null) SetAlpha(_blackPanel, 1f);

        // 2. 텍스트 등장 전 대기
        elapsed = 0f;
        while (elapsed < _textFadeDelay)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // 3. "Dead" + 버튼 동시 페이드인
        elapsed = 0f;
        while (elapsed < _textFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / _textFadeDuration);
            if (_deadText != null) SetAlpha(_deadText, t);
            if (_resurrectButtonText != null)
            {
                Color c = _buttonNormalColor;
                c.a = t;
                _resurrectButtonText.color = c;
            }
            yield return null;
        }

        if (_deadText        != null) SetAlpha(_deadText, 1f);
        if (_resurrectButtonText != null)
        {
            Color c = _buttonNormalColor;
            c.a = 1f;
            _resurrectButtonText.color = c;
        }

        // 버튼 인터랙션 활성화
        if (_resurrectButton != null) _resurrectButton.interactable = true;
    }

    // ── 유틸 ─────────────────────────────────────────────

    private static void SetAlpha(Graphic graphic, float a)
    {
        if (graphic == null) return;
        Color c = graphic.color;
        c.a = a;
        graphic.color = c;
    }

    private static void SetAlpha(TextMeshProUGUI tmp, float a)
    {
        if (tmp == null) return;
        Color c = tmp.color;
        c.a = a;
        tmp.color = c;
    }
}
