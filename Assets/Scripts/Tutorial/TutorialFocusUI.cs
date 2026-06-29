using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 튜토리얼 포커스 오버레이.
/// 특정 월드 Transform 또는 UI 슬롯 위에 화살표(▼)와 힌트 텍스트를 표시합니다.
///
/// 사용법:
///   TutorialFocusUI.Instance.ShowWorldTarget(transform, "여기를 공격하세요");
///   TutorialFocusUI.Instance.ShowSlotTarget(slotRectTransform, "무기를 여기 넣으세요");
///   TutorialFocusUI.Instance.Hide();
/// </summary>
public class TutorialFocusUI : MonoBehaviour
{
    public static TutorialFocusUI Instance { get; private set; }

    // 월드 오브젝트 대상일 때 화면 위로 올리는 오프셋
    [SerializeField] private Vector3 _worldOffset = new Vector3(0f, 1.8f, 0f);

    private Camera _mainCamera;
    private Canvas _rootCanvas;
    private RectTransform _rootCanvasRect;

    // 화살표 + 텍스트 컨테이너
    private RectTransform _container;
    private Text _arrowText;
    private Text _hintText;

    // 추적 대상 (둘 중 하나만 사용)
    private Transform _worldTarget;
    private RectTransform _slotTarget;

    // 화살표 애니메이션 코루틴
    private Coroutine _bounceRoutine;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        _mainCamera = Camera.main;
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            _rootCanvas     = canvas.rootCanvas;
            _rootCanvasRect = _rootCanvas.GetComponent<RectTransform>();
        }

        BuildUI();
    }

    private void BuildUI()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var parent = _rootCanvas != null ? _rootCanvas.transform : transform;

        var go = new GameObject("_TutorialFocus");
        go.transform.SetParent(parent, false);

        _container = go.AddComponent<RectTransform>();
        _container.anchorMin = _container.anchorMax = Vector2.zero;
        _container.pivot     = new Vector2(0.5f, 0f);
        _container.sizeDelta = new Vector2(200f, 80f);

        // 화살표 "▼" 텍스트
        var arrowGO = new GameObject("Arrow");
        arrowGO.transform.SetParent(go.transform, false);
        var arrowRt = arrowGO.AddComponent<RectTransform>();
        arrowRt.anchorMin = arrowRt.anchorMax = new Vector2(0.5f, 0.5f);
        arrowRt.pivot     = new Vector2(0.5f, 0.5f);
        arrowRt.anchoredPosition = new Vector2(0f, 30f);
        arrowRt.sizeDelta = new Vector2(40f, 40f);

        _arrowText = arrowGO.AddComponent<Text>();
        _arrowText.font      = font;
        _arrowText.fontSize  = 30;
        _arrowText.alignment = TextAnchor.MiddleCenter;
        _arrowText.color     = new Color(1f, 0.9f, 0.2f, 1f); // 노란색
        _arrowText.text      = "▼";
        _arrowText.horizontalOverflow = HorizontalWrapMode.Overflow;

        // 힌트 텍스트
        var hintGO = new GameObject("Hint");
        hintGO.transform.SetParent(go.transform, false);
        var hintRt = hintGO.AddComponent<RectTransform>();
        hintRt.anchorMin = hintRt.anchorMax = new Vector2(0.5f, 0.5f);
        hintRt.pivot     = new Vector2(0.5f, 0.5f);
        hintRt.anchoredPosition = new Vector2(0f, 0f);
        hintRt.sizeDelta = new Vector2(200f, 40f);

        // 힌트 배경
        var bgGO = new GameObject("HintBg");
        bgGO.transform.SetParent(hintGO.transform, false);
        var bgRt = bgGO.AddComponent<RectTransform>();
        bgRt.anchorMin = bgRt.anchorMax = new Vector2(0.5f, 0.5f);
        bgRt.pivot     = new Vector2(0.5f, 0.5f);
        bgRt.anchoredPosition = Vector2.zero;
        bgRt.sizeDelta = new Vector2(200f, 40f);
        bgGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

        _hintText = hintGO.AddComponent<Text>();
        _hintText.font      = font;
        _hintText.fontSize  = 17;
        _hintText.alignment = TextAnchor.MiddleCenter;
        _hintText.color     = Color.white;
        _hintText.horizontalOverflow = HorizontalWrapMode.Overflow;
        _hintText.verticalOverflow   = VerticalWrapMode.Overflow;

        go.SetActive(false);
    }

    // ── 공개 API ──────────────────────────────────────────────────

    /// <summary>월드 오브젝트 위에 화살표를 표시합니다.</summary>
    public void ShowWorldTarget(Transform target, string hint = "")
    {
        _worldTarget = target;
        _slotTarget  = null;
        ShowInternal(hint);
    }

    /// <summary>UI 슬롯(RectTransform) 위에 화살표를 표시합니다.</summary>
    public void ShowSlotTarget(RectTransform slot, string hint = "")
    {
        _slotTarget  = slot;
        _worldTarget = null;
        ShowInternal(hint);
    }

    public void Hide()
    {
        _worldTarget = null;
        _slotTarget  = null;
        if (_container != null) _container.gameObject.SetActive(false);
        if (_bounceRoutine != null) { StopCoroutine(_bounceRoutine); _bounceRoutine = null; }
    }

    // ── 내부 ──────────────────────────────────────────────────────

    private void ShowInternal(string hint)
    {
        if (_container == null) return;
        if (_hintText != null) _hintText.text = hint;
        _container.gameObject.SetActive(true);

        if (_bounceRoutine != null) StopCoroutine(_bounceRoutine);
        _bounceRoutine = StartCoroutine(BounceArrow());
    }

    private void LateUpdate()
    {
        if (_container == null || !_container.gameObject.activeSelf) return;

        Vector2 screenPos;

        if (_worldTarget != null && _mainCamera != null)
        {
            Vector3 wp = _mainCamera.WorldToScreenPoint(_worldTarget.position + _worldOffset);
            if (wp.z < 0f) { _container.gameObject.SetActive(false); return; }
            screenPos = wp;
        }
        else if (_slotTarget != null)
        {
            // UI 슬롯의 스크린 중심 좌표
            Vector3[] corners = new Vector3[4];
            _slotTarget.GetWorldCorners(corners);
            Vector3 center = (corners[0] + corners[2]) * 0.5f;
            screenPos = RectTransformUtility.WorldToScreenPoint(null, center);
            screenPos.y += _slotTarget.rect.height * 0.5f; // 슬롯 상단
        }
        else return;

        // Canvas 좌표 변환
        if (_rootCanvasRect != null)
        {
            Camera uiCam = _rootCanvas != null && _rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _rootCanvas.worldCamera : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_rootCanvasRect, screenPos, uiCam, out Vector2 lp))
            {
                _container.localPosition = lp;
                return;
            }
        }
        _container.position = screenPos;
    }

    private IEnumerator BounceArrow()
    {
        if (_arrowText == null) yield break;
        var rt = _arrowText.GetComponent<RectTransform>();
        float baseY = rt.anchoredPosition.y;
        float elapsed = 0f;

        while (true)
        {
            elapsed += Time.unscaledDeltaTime * 3f;
            float offsetY = Mathf.Sin(elapsed) * 6f;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, baseY + offsetY);
            yield return null;
        }
    }
}
