using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Cinemachine Impulse 기반 카메라 흔들림(Camera Shake) 효과.
///
/// 사용법:
///   1. CameraShakeController가 부착된 오브젝트에 CinemachineImpulseSource 컴포넌트를 함께 부착하세요.
///   2. Impulse Source의 Default Velocity나 Raw Signal 셋팅을 통해 흔들림 패턴을 지정하세요.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CinemachineImpulseSource))]
public class CameraShakeController : MonoBehaviour
{
    public static CameraShakeController Instance { get; private set; }

    private CinemachineImpulseSource _impulseSource;

    private void Awake()
    {
        // 싱글톤 — 씬에 하나만 존재해야 합니다.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── 퍼블릭 API ──────────────────────────────────────────────────────────

    /// <summary>기본 파라미터로 카메라를 흔듭니다.</summary>
    public void Shake()
    {
        Shake(0.2f, 0.1f);
    }

    /// <summary>지정한 지속 시간과 강도로 카메라를 흔듭니다. (CinemachineImpulseSource 활용)</summary>
    /// <param name="duration">기존 호환성을 위해 남겨둠. 실제로는 Impulse Signal 설정에 따릅니다.</param>
    /// <param name="magnitude">최대 임펄스 강도</param>
    public void Shake(float duration, float magnitude)
    {
        if (_impulseSource != null)
        {
            // Cinemachine의 GenerateImpulse는 설정된 Signal 속성을 기반으로 충격을 가함
            // force(magnitude) 값을 전달하여 강도를 조절
            _impulseSource.GenerateImpulse(magnitude);
        }
    }
}
