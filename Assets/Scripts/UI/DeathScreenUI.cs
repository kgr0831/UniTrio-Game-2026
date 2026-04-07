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
        
        Hide();
    }

    // ── Public API ─────────────────────────────────────────

    /// <summary>죽음 UI를 표시합니다. timeScale=0에서 호출 가능.</summary>
    public void Show()
    {
        // 1. 부활 관련 요소 개별 활성화 (카르마 알림 시 꺼졌을 수도 있음)
        if (_deadText != null) _deadText.gameObject.SetActive(true);
        if (_resurrectButton != null) _resurrectButton.gameObject.SetActive(true);

        // 2. 모든 요소 즉시 가리고 상호작용 차단
        if (_rootCanvasGroup != null) _rootCanvasGroup.alpha = 1f;

        if (_blackPanel != null) SetAlpha(_blackPanel, 0f);
        if (_deadText   != null) SetAlpha(_deadText,   0f);
        
        if (_resurrectButton != null)
        {
            _resurrectButton.interactable = false;
            // 버튼 배경(targetGraphic)과 텍스트 모두 알파 0
            if (_resurrectButton.targetGraphic != null) SetAlpha(_resurrectButton.targetGraphic, 0f);
            if (_resurrectButtonText           != null) SetAlpha(_resurrectButtonText,           0f);

            // 이전 선택 상태 초기화
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        gameObject.SetActive(true);

        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeInSequence());
    }

    /// <summary>죽음 UI를 즉시 숨깁니다. 모든 요소를 alpha=0으로 초기화.</summary>
    public void Hide()
    {
        if (_fadeCoroutine != null) { StopCoroutine(_fadeCoroutine); _fadeCoroutine = null; }
        
        // 가시성 즉시 제거
        SetRootAlpha(0f);
        if (_blackPanel      != null) SetAlpha(_blackPanel, 0f);
        HideResurrectionElements();
        
        gameObject.SetActive(false);
    }

    /// <summary>캔버스 자체의 알파값을 설정합니다. 카르마 알림 등 개별 요소 노출 시 필요합니다.</summary>
    public void SetRootAlpha(float alpha)
    {
        if (_rootCanvasGroup != null) _rootCanvasGroup.alpha = alpha;
    }

    // ── 버튼 이벤트 (Inspector에서도 연결 가능) ──────────

    public void OnResurrectButtonClicked()
    {
        // 1. 중복 클릭 방지
        if (_resurrectButton != null) _resurrectButton.interactable = false;

        // 2. 즉시 가시성 제거 (카르마 알림 시 버튼이 잔존하는 문제 해결)
        Hide();

        OnResurrectClicked?.Invoke();
    }

    /// <summary>버튼과 "Dead" 텍스트만 비활성화합니다. 카르마 알림 시 유용합니다.</summary>
    public void HideResurrectionElements()
    {
        if (_deadText != null) 
        {
            SetAlpha(_deadText, 0f);
            _deadText.gameObject.SetActive(false);
        }

        if (_resurrectButton != null)
        {
            _resurrectButton.interactable = false;
            if (_resurrectButton.targetGraphic != null) SetAlpha(_resurrectButton.targetGraphic, 0f);
            if (_resurrectButtonText           != null) SetAlpha(_resurrectButtonText,           0f);
            
            // 버튼 자체를 꺼버림으로써 잔존 가시성/상호작용 차단
            _resurrectButton.gameObject.SetActive(false);
        }
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
        // [Step 1] 검은 패널 페이드인 (배경)
        float elapsed = 0f;
        while (elapsed < _panelFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / _panelFadeDuration);
            if (_blackPanel != null) SetAlpha(_blackPanel, t);
            yield return null;
        }
        if (_blackPanel != null) SetAlpha(_blackPanel, 1f);

        // [Step 2] 텍스트 등장 전 대기
        elapsed = 0f;
        while (elapsed < _textFadeDelay) { elapsed += Time.unscaledDeltaTime; yield return null; }

        // [Step 3] "Dead" 텍스트 페이드인 (이 단계가 끝나야 버튼 조작 가능 준비)
        elapsed = 0f;
        while (elapsed < _textFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / _textFadeDuration);
            if (_deadText != null) SetAlpha(_deadText, t);
            yield return null;
        }
        if (_deadText != null) SetAlpha(_deadText, 1f);

        // [New] 텍스트가 100%가 된 시점에 마우스가 이미 버튼 위에 있는지 체크하여 색상 선제 적용
        UpdateHoverStateManually();

        // [Step 4] "부활" 버튼 페이드인 (텍스트 완료 후 시작)
        elapsed = 0f;
        while (elapsed < _textFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / _textFadeDuration);
            
            // 버튼 배경 + 텍스트 동시 페이드
            if (_resurrectButton != null && _resurrectButton.targetGraphic != null)
                SetAlpha(_resurrectButton.targetGraphic, t);
            if (_resurrectButtonText != null)
                SetAlpha(_resurrectButtonText, t);
            
            yield return null;
        }

        // 결과 확정
        if (_resurrectButton != null && _resurrectButton.targetGraphic != null)
            SetAlpha(_resurrectButton.targetGraphic, 1f);

        // [Step 5] 버튼 인터랙션 활성화 (모든 연출이 끝난 후)
        if (_resurrectButton != null) _resurrectButton.interactable = true;
    }

    /// <summary>마우스 위치를 강제로 체크하여 버튼의 호버 상태를 시각적으로 업데이트합니다.</summary>
    private void UpdateHoverStateManually()
    {
        if (_resurrectButton == null || _resurrectButtonText == null) return;
        
        bool isOver = false;
        // RectTransform 기반 마우스 포인터 중첩 확인 (ScreenSpace-Overlay 기준)
        RectTransform rt = _resurrectButton.GetComponent<RectTransform>();
        if (rt != null)
        {
            isOver = RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition);
        }

        if (isOver)
            _resurrectButtonText.color = _buttonHoverColor;
        else
            _resurrectButtonText.color = _buttonNormalColor;
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
