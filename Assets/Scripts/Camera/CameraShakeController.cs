using System.Collections;
using UnityEngine;

/// <summary>
/// 카메라 흔들림(Camera Shake) 효과를 제공합니다.
///
/// 사용법:
///   1. 메인 카메라 GameObject에 이 컴포넌트를 추가합니다.
///   2. CameraShakeController.Instance.Shake(duration, magnitude) 로 호출합니다.
///
/// 구현:
///   timeScale 영향 없이 동작하도록 Time.unscaledDeltaTime 기준으로 실행됩니다.
///   카메라 자체 위치를 수정하는 대신 localPosition 오프셋만 변경하므로
///   CinemachineCamera나 다른 카메라 컨트롤러와 충돌하지 않으려면
///   이 컴포넌트를 카메라의 '부모' 더미 오브젝트에 붙이세요.
/// </summary>
[DisallowMultipleComponent]
public class CameraShakeController : MonoBehaviour
{
    public static CameraShakeController Instance { get; private set; }

    [Header("Default Shake Params")]
    [Tooltip("흔들림 강도(월드 유닛). 작은 값(0.05~0.15)이 자연스럽습니다.")]
    [SerializeField] private float _defaultMagnitude = 0.08f;

    [Tooltip("흔들림 지속 시간(초, 비스케일).")]
    [SerializeField] private float _defaultDuration = 0.25f;

    [Tooltip("흔들림 감쇠 커브. 처음에 강하고 → 끝에 0이 되도록 설정하세요.")]
    [SerializeField] private AnimationCurve _dampingCurve = new AnimationCurve(
        new Keyframe(0f, 1f, 0f, -2f),
        new Keyframe(1f, 0f, -2f, 0f)
    );

    private Vector3 _originalLocalPos;
    private Coroutine _shakeRoutine;

    private void Awake()
    {
        // 싱글톤 — 씬에 하나만 존재해야 합니다.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _originalLocalPos = transform.localPosition;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── 퍼블릭 API ──────────────────────────────────────────────────────────

    /// <summary>기본 파라미터로 카메라를 흔듭니다.</summary>
    public void Shake()
        => Shake(_defaultDuration, _defaultMagnitude);

    /// <summary>지정한 지속 시간과 강도로 카메라를 흔듭니다. timeScale과 무관하게 동작합니다.</summary>
    /// <param name="duration">흔들림 지속 시간(초, 비스케일)</param>
    /// <param name="magnitude">최대 오프셋(월드 유닛)</param>
    public void Shake(float duration, float magnitude)
    {
        // 이미 흔들리는 중이면 중단 후 새로 시작
        if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
        _shakeRoutine = StartCoroutine(ShakeRoutine(duration, magnitude));
    }

    // ── 코루틴 ──────────────────────────────────────────────────────────────

    private IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t       = Mathf.Clamp01(elapsed / duration);
            float damping = _dampingCurve.Evaluate(t);

            // 완전 랜덤 오프셋 — 매 프레임 새로운 위치
            float offsetX = Random.Range(-1f, 1f) * magnitude * damping;
            float offsetY = Random.Range(-1f, 1f) * magnitude * damping;
            transform.localPosition = _originalLocalPos + new Vector3(offsetX, offsetY, 0f);

            yield return null;
        }

        // 흔들림 종료 후 원위치 복원
        transform.localPosition = _originalLocalPos;
        _shakeRoutine            = null;
    }
}
