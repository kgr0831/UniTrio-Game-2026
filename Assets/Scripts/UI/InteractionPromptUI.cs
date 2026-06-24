using UnityEngine;
using TMPro;

/// <summary>
/// 상호작용 가능한 오브젝트 위에 "Press F" 프롬프트를 월드 좌표 기반으로 표시하는 UI.
/// Canvas(World Space 또는 Screen Space) 하위에 배치하여 사용합니다.
/// PlayerInteractionDetector에서 Show/Hide를 호출합니다.
/// </summary>
public class InteractionPromptUI : MonoBehaviour
{
    public static InteractionPromptUI Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject _promptRoot;
    [SerializeField] private TextMeshProUGUI _promptText;

    // 프롬프트가 추적할 대상의 Transform (캐싱)
    private Transform _targetTransform;
    private Vector3 _worldOffset = new Vector3(0f, 1.5f, 0f);

    // 카메라 캐싱 (매 프레임 Camera.main 호출 방지)
    private Camera _mainCamera;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        _mainCamera = Camera.main;

        if (_promptRoot != null) _promptRoot.SetActive(false);
    }

    /// <summary>
    /// 프롬프트를 특정 월드 위치의 대상 위에 표시합니다.
    /// </summary>
    public void Show(Transform target, string message)
    {
        if (_promptRoot == null || _promptText == null) return;

        _targetTransform = target;
        _promptText.text = message;
        _promptRoot.SetActive(true);
    }

    /// <summary>
    /// 프롬프트를 숨깁니다.
    /// </summary>
    public void Hide()
    {
        if (_promptRoot != null) _promptRoot.SetActive(false);
        _targetTransform = null;
    }

    private void LateUpdate()
    {
        // 대상이 없거나 UI가 비활성이면 스킵
        if (_targetTransform == null || _promptRoot == null || !_promptRoot.activeSelf) return;

        if (_mainCamera == null) return;

        // 월드 좌표 → 스크린 좌표로 변환하여 UI 위치 갱신
        Vector3 worldPos = _targetTransform.position + _worldOffset;
        Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPos);

        // 카메라 뒤에 있으면 숨기기
        if (screenPos.z < 0f)
        {
            _promptRoot.SetActive(false);
            return;
        }

        if (!_promptRoot.activeSelf) _promptRoot.SetActive(true);
        _promptRoot.transform.position = screenPos;
    }
}
