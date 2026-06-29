using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 깨어남 인트로 연출: 전용 URP Volume(가우시안 DoF로 화면 전체 블러)을
/// weight 1→0으로 감쇠시켜 "블러→선명"을 만듭니다 (Life is Strange 풍).
/// Volume의 프로파일에는 화면 전체가 흐려지도록 튜닝된 DoF만 들어 있으면 됩니다.
/// </summary>
public class IntroBlurController : MonoBehaviour
{
    [Tooltip("인트로 블러 전용 글로벌 Volume (DoF 오버라이드 포함)")]
    [SerializeField] private Volume _blurVolume;

    [Tooltip("선명해지기 전 블러를 유지하는 시간 (초)")]
    [SerializeField] private float _holdDuration = 0.6f;

    [Tooltip("블러가 완전히 사라지기까지 걸리는 시간 (초)")]
    [SerializeField] private float _clearDuration = 1.8f;

    /// <summary>블러를 최대로 켠 뒤 서서히 걷어냅니다. 코루틴으로 대기 가능.</summary>
    public IEnumerator PlayClear()
    {
        if (_blurVolume == null) yield break;

        _blurVolume.weight = 1f;
        yield return new WaitForSecondsRealtime(_holdDuration);

        float t = 0f;
        while (t < _clearDuration)
        {
            t += Time.unscaledDeltaTime;
            _blurVolume.weight = Mathf.Clamp01(1f - t / _clearDuration);
            yield return null;
        }
        _blurVolume.weight = 0f;
    }

    public void SetWeight(float w)
    {
        if (_blurVolume != null) _blurVolume.weight = Mathf.Clamp01(w);
    }

    /// <summary>씬 시작 즉시 블러를 최대로 켜 둡니다 (인트로 시작 전 한 프레임 노출 방지).</summary>
    private void Awake()
    {
        if (_blurVolume != null)
        {
            // 동적으로 프로필과 효과를 생성하여 무조건 적용되게 함
            if (_blurVolume.profile == null) _blurVolume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            
            if (!_blurVolume.profile.TryGet(out UnityEngine.Rendering.Universal.DepthOfField dof))
            {
                dof = _blurVolume.profile.Add<UnityEngine.Rendering.Universal.DepthOfField>(true);
                dof.mode.overrideState = true;
                dof.mode.value = UnityEngine.Rendering.Universal.DepthOfFieldMode.Gaussian;
                dof.gaussianStart.overrideState = true;
                dof.gaussianStart.value = 0f;
                dof.gaussianEnd.overrideState = true;
                dof.gaussianEnd.value = 1f;
                dof.gaussianMaxRadius.overrideState = true;
                dof.gaussianMaxRadius.value = 2f;
            }

            if (!_blurVolume.profile.TryGet(out UnityEngine.Rendering.Universal.ColorAdjustments colorAdj))
            {
                colorAdj = _blurVolume.profile.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(true);
                colorAdj.postExposure.overrideState = true;
                colorAdj.postExposure.value = -4f; // 어둡게 시작
            }

            _blurVolume.weight = 1f;
        }
    }
}
