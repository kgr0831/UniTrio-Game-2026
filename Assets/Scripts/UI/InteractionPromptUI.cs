using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// NPC/오브젝트 위에 "F + 설명" 상호작용 프롬프트를 표시하는 UI.
/// - DashUI(회피 저스트)와 동일한 원형 스프라이트/폰트 사용
/// - RectTransformUtility로 Canvas Scaler 배율 보정 → 카메라 이동 시 NPC 머리 위에 고정됨
/// </summary>
public class InteractionPromptUI : MonoBehaviour
{
    public static InteractionPromptUI Instance { get; private set; }

    // 씬 호환성 유지 (런타임 미사용)
    [SerializeField] private GameObject _promptRoot;
    [SerializeField] private TextMeshProUGUI _promptText;

    private RectTransform _container;
    private Text _keyLabel;
    private Text _descLabel;
    private Transform _targetTransform;

    // 타겟 기준 월드 오프셋 (NPC 우측 상단)
    private readonly Vector3 _worldOffset = new Vector3(0.5f, 1.5f, 0f);

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (_promptRoot != null) _promptRoot.SetActive(false);
        BuildUI();
    }

    private void BuildUI()
    {
        // DashUI 프리팹에서 스프라이트/폰트 추출
        Sprite circleSprite = null;
        Color circleColor = new Color(0.30f, 0.30f, 0.30f, 1f);
        Font font = null;

        var dashPrefab = Resources.Load<GameObject>("Prefabs/DashUI");
        if (dashPrefab != null)
        {
            foreach (var img in dashPrefab.GetComponentsInChildren<Image>(true))
            {
                if (img.sprite != null)
                {
                    circleSprite = img.sprite;
                    circleColor = img.color;
                    break;
                }
            }
            foreach (var t in dashPrefab.GetComponentsInChildren<Text>(true))
            {
                if (t.font != null) { font = t.font; break; }
            }
        }
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // World Space Canvas로 생성하여 MonsterHPBar와 동일하게 월드 좌표계를 따르게 함
        var containerGO = new GameObject("_InteractPrompt");
        containerGO.transform.SetParent(null, false);

        var canvas = containerGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main; // 카메라를 명시적으로 할당하여 투영 오차나 스냅핑을 방지합니다.
        canvas.sortingLayerName = "UI"; // 몬스터 체력바처럼 UI 소팅 레이어 사용
        canvas.sortingOrder = 100;

        _container = containerGO.GetComponent<RectTransform>();
        _container.pivot = new Vector2(0.5f, 0.5f);
        _container.sizeDelta = new Vector2(160f, 50f);
        // 월드 공간 크기에 맞게 스케일 조정 (1픽셀 = 0.025 유닛)
        _container.localScale = new Vector3(0.025f, 0.025f, 1f);

        // 원형 배경 (DashUI 동일 스프라이트)
        var circleRect = CreateRect("Circle", _container, new Vector2(-50f, 0f), new Vector2(50f, 50f));
        var circleImg = circleRect.gameObject.AddComponent<Image>();
        circleImg.sprite = circleSprite;
        circleImg.color = circleColor;

        // "F" 키 텍스트
        var keyRect = CreateRect("Key", circleRect, Vector2.zero, new Vector2(50f, 50f));
        _keyLabel = keyRect.gameObject.AddComponent<Text>();
        _keyLabel.font = font;
        _keyLabel.fontSize = 26;
        _keyLabel.fontStyle = FontStyle.Bold;
        _keyLabel.alignment = TextAnchor.MiddleCenter;
        _keyLabel.color = Color.white;
        _keyLabel.horizontalOverflow = HorizontalWrapMode.Overflow;

        // 설명 텍스트
        var descRect = CreateRect("Desc", _container, new Vector2(28f, 0f), new Vector2(90f, 50f));
        _descLabel = descRect.gameObject.AddComponent<Text>();
        _descLabel.font = font;
        _descLabel.fontSize = 22;
        _descLabel.alignment = TextAnchor.MiddleLeft;
        _descLabel.color = Color.white;
        _descLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        _descLabel.verticalOverflow = VerticalWrapMode.Overflow;

        containerGO.SetActive(false);
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        return rt;
    }

    public void Show(Transform target, string message)
    {
        if (_container == null) return;
        _targetTransform = target;

        string msg = message;
        if (message.StartsWith("F키: ")) msg = message.Substring(4);
        else if (message.StartsWith("[F] ")) msg = message.Substring(4);

        if (_keyLabel != null) _keyLabel.text = "F";
        if (_descLabel != null) _descLabel.text = msg;

        // 즉시 위치 동기화
        _container.position = _targetTransform.position + _worldOffset;
        _container.gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (_container != null) _container.gameObject.SetActive(false);
        _targetTransform = null;
    }

    private Vector3 _lastLoggedPos;

    private void LateUpdate()
    {
        // HP바와 완벽히 동일한 로직: 화면 변환 없이 월드 좌표에 다이렉트로 위치를 고정시킵니다.
        if (_targetTransform == null || _container == null || !_container.gameObject.activeSelf) return;
        
        Vector3 newPos = _targetTransform.position + _worldOffset;
        
        // 거리가 0.1 이상 갑자기 튀면 로그 출력
        if (Vector3.Distance(_lastLoggedPos, newPos) > 0.1f && _lastLoggedPos != Vector3.zero)
        {
            Debug.Log($"[InteractUI] 순간이동 감지! 이전위치:{_lastLoggedPos}, 새위치:{newPos}, 타겟:{_targetTransform.name} 타겟위치:{_targetTransform.position}");
        }
        _lastLoggedPos = newPos;

        _container.position = newPos;
    }
}
