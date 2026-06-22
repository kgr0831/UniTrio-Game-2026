using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 회피 저스트 UI 관리:
/// - 'F 누르기' 프롬프트(DashUI 프리팹) : 성공 시 화면 중앙 우측에 표시, F 입력 시 흰색 점멸+증발하며 사라짐.
/// - 무기 경고 텍스트 : 무기 미착용 상태로 F를 누르면 화면 상단에 붉은색으로 표시(DashUI와 같은 neodgm 폰트).
/// 씬의 Overlay Canvas 밑에 생성/배치한다.
/// </summary>
public class JustDodgeUI : MonoBehaviour
{
    [Tooltip("DashUI 프롬프트를 화면 중앙에서 얼마나 오른쪽/위로 옮길지(px)")]
    public Vector2 PromptOffset = new Vector2(320f, 0f);

    private Canvas      _canvas;
    private GameObject  _prompt;     // DashUI 인스턴스
    private Color[]     _promptOrigColors;
    private Text        _warning;
    private Font        _font;

    private Coroutine _hideCo;
    private Coroutine _warnCo;
    private bool       _hiding;

    public static JustDodgeUI GetOrCreate()
    {
        var inst = FindFirstObjectByType<JustDodgeUI>();
        if (inst != null) return inst;

        var go = new GameObject("JustDodgeUI");
        inst = go.AddComponent<JustDodgeUI>();
        inst.Init();
        return inst;
    }

    private void Init()
    {
        _canvas = FindCanvas();
        if (_canvas == null)
        {
            Debug.LogWarning("[JustDodgeUI] Overlay Canvas를 찾지 못해 UI를 표시할 수 없습니다.");
            return;
        }

        var prefab = Resources.Load<GameObject>("Prefabs/DashUI");
        if (prefab != null)
        {
            _prompt = Instantiate(prefab, _canvas.transform);
            _prompt.transform.localScale    = Vector3.one;
            _prompt.transform.localPosition = new Vector3(PromptOffset.x, PromptOffset.y, 0f);

            var graphics = _prompt.GetComponentsInChildren<Graphic>(true);
            _promptOrigColors = new Color[graphics.Length];
            for (int i = 0; i < graphics.Length; i++) _promptOrigColors[i] = graphics[i].color;

            var t = _prompt.GetComponentInChildren<Text>(true);
            if (t != null) _font = t.font;

            _prompt.SetActive(false);
        }
        else
        {
            Debug.LogWarning("[JustDodgeUI] Resources/Prefabs/DashUI 를 찾지 못했습니다.");
        }
    }

    private Canvas FindCanvas()
    {
        Canvas best = null;
        foreach (var cv in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (cv.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            if (cv.name == "DeathScreenCanvas") continue; // 죽음 UI 캔버스 제외
            if (best == null || cv.sortingOrder < best.sortingOrder) best = cv;
        }
        return best;
    }

    // ── 프롬프트 ──────────────────────────────────────────────────

    public void ShowPrompt()
    {
        if (_prompt == null) return;
        if (_hideCo != null) { StopCoroutine(_hideCo); _hideCo = null; }
        _hiding = false;

        _prompt.transform.localScale    = Vector3.one;
        _prompt.transform.localPosition = new Vector3(PromptOffset.x, PromptOffset.y, 0f);
        RestorePromptColors();
        _prompt.SetActive(true);
    }

    /// <summary>F 입력 시: 흰색으로 점멸하며 커지고 사라짐(증발).</summary>
    public void FlashHidePrompt()
    {
        if (_prompt == null || !_prompt.activeSelf) return;
        if (_hideCo != null) StopCoroutine(_hideCo);
        _hideCo = StartCoroutine(FlashHideRoutine());
    }

    /// <summary>애니메이션 없이 즉시 숨김(윈도우 만료 등). 증발 연출 중이면 건드리지 않음.</summary>
    public void HidePromptImmediate()
    {
        if (_prompt == null || _hiding) return;
        if (_hideCo != null) { StopCoroutine(_hideCo); _hideCo = null; }
        _prompt.SetActive(false);
    }

    private IEnumerator FlashHideRoutine()
    {
        _hiding = true;
        var graphics = _prompt.GetComponentsInChildren<Graphic>(true);

        // 흰색 점멸
        foreach (var g in graphics) g.color = Color.white;

        const float dur = 0.28f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            _prompt.transform.localScale = Vector3.one * (1f + 0.45f * k); // 증발하듯 커지며
            float a = 1f - k;
            foreach (var g in graphics) { var c = g.color; c.a = a; g.color = c; }
            yield return null;
        }

        _prompt.SetActive(false);
        _prompt.transform.localScale = Vector3.one;
        RestorePromptColors();
        _hiding = false;
        _hideCo = null;
    }

    private void RestorePromptColors()
    {
        if (_prompt == null || _promptOrigColors == null) return;
        var graphics = _prompt.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length && i < _promptOrigColors.Length; i++)
            graphics[i].color = _promptOrigColors[i];
    }

    // ── 무기 경고 ─────────────────────────────────────────────────

    public void ShowWarning(string msg = "무기를 착용해야 합니다!")
    {
        EnsureWarning();
        if (_warning == null) return;

        _warning.text = msg;
        _warning.gameObject.SetActive(true);
        if (_warnCo != null) StopCoroutine(_warnCo);
        _warnCo = StartCoroutine(WarnRoutine());
    }

    private IEnumerator WarnRoutine()
    {
        Color baseC = new Color(1f, 0.2f, 0.2f, 1f);
        _warning.color = baseC;

        float hold = 0.8f, fade = 0.4f, t = 0f;
        while (t < hold) { t += Time.unscaledDeltaTime; yield return null; }

        t = 0f;
        while (t < fade)
        {
            t += Time.unscaledDeltaTime;
            var c = baseC; c.a = 1f - (t / fade); _warning.color = c;
            yield return null;
        }
        _warning.gameObject.SetActive(false);
        _warnCo = null;
    }

    private void EnsureWarning()
    {
        if (_warning != null || _canvas == null) return;

        var go = new GameObject("JustDodgeWeaponWarning");
        go.transform.SetParent(_canvas.transform, false);

        _warning = go.AddComponent<Text>();
        _warning.font = (_font != null) ? _font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _warning.fontSize = 40;
        _warning.alignment = TextAnchor.UpperCenter;
        _warning.color = new Color(1f, 0.2f, 0.2f, 1f);
        _warning.horizontalOverflow = HorizontalWrapMode.Overflow;
        _warning.verticalOverflow   = VerticalWrapMode.Overflow;
        _warning.raycastTarget = false;

        var rt = _warning.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -90f);
        rt.sizeDelta = new Vector2(900f, 90f);

        go.SetActive(false);
    }
}
