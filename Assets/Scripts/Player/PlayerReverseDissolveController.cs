using System.Collections;
using UnityEngine;

/// <summary>
/// 부활 역용해 애니메이션 컨트롤러.
/// DeathCutsceneController.Phase2 에서 호출됨.
///
/// 동작:
///   1. 원본 머티리얼 캐싱 (Awake)
///   2. 부활 시 PlayerResurrectDissolve 머티리얼로 교체
///   3. Threshold 1→0, TintAmount 1→0 을 unscaledTime 기준으로 애니메이션
///   4. 완료 후 원본 머티리얼 복원
/// </summary>
[DisallowMultipleComponent]
public class PlayerReverseDissolveController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpriteRenderer _spriteRenderer;

    [Tooltip("Custom/PlayerResurrectDissolve 셰이더를 사용하는 머티리얼.")]
    [SerializeField] private Material _resurrectMaterial;

    [Header("Curve")]
    [Tooltip("진행(0→1)에 따른 threshold 곡선. 기본은 선형(1→0).")]
    [SerializeField] private AnimationCurve _thresholdCurve = AnimationCurve.Linear(0, 1, 1, 0);

    // ── 캐시 ─────────────────────────────────────────────────────────────

    private Material          _originalMaterial;
    private MaterialPropertyBlock _mpb;

    private static readonly int _thresholdId  = Shader.PropertyToID("_Threshold");
    private static readonly int _tintAmountId = Shader.PropertyToID("_TintAmount");

    // ── 생명주기 ──────────────────────────────────────────────────────────

    private void Awake()
    {
        if (_spriteRenderer == null)
            _spriteRenderer = GetComponent<SpriteRenderer>();

        _mpb = new MaterialPropertyBlock();
    }

    private void Start()
    {
        // 최신 머티리얼 캐싱 (원본 복원용)
        if (_spriteRenderer != null && _originalMaterial == null)
        {
            _originalMaterial = _spriteRenderer.sharedMaterial;
        }
    }

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>
    /// 역용해를 재생합니다. DeathCutsceneController.Phase2에서 StartCoroutine으로 호출하세요.
    /// duration: 전체 재생 시간 (비스케일 초)
    /// </summary>
    public IEnumerator PlayReverseDissolve(float duration)
    {
        if (_resurrectMaterial == null || _spriteRenderer == null)
        {
            Debug.LogWarning("[PlayerReverseDissolveController] 참조가 설정되지 않았습니다.");
            yield break;
        }

        // 1. 상태 초기화 및 렌더러 활성화
        _spriteRenderer.enabled  = true;

        // E-6 부활음 (역용해 시작과 동시에 1회)
        if (AudioManager.Instance != null) AudioManager.Instance.PlayResurrect();
        
        // 2회차 부활 대비: 원본 머티리얼이 유실되었다면 현재 머티리얼을 백업 (Dissolve 머티리얼이 아니어야 함)
        if (_originalMaterial == null || _originalMaterial.name.Contains("Dissolve"))
        {
             // 기본 스프라이트 머티리얼이라도 찾아서 할당
             _originalMaterial = _spriteRenderer.sharedMaterial;
        }

        // 2. 머티리얼 교체 및 초기 Threshold 설정 (완전 투명)
        _spriteRenderer.material = _resurrectMaterial;
        _spriteRenderer.GetPropertyBlock(_mpb);
        _mpb.SetFloat(_thresholdId,  1f);
        _mpb.SetFloat(_tintAmountId, 1f);
        _spriteRenderer.SetPropertyBlock(_mpb);

        // 3. 애니메이션 진행
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // 커브 적용: 1(소멸) → 0(복구)
            float threshold  = _thresholdCurve.Evaluate(t);
            float tintAmount = 1f - t; 

            _spriteRenderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(_thresholdId,  threshold);
            _mpb.SetFloat(_tintAmountId, tintAmount);
            _spriteRenderer.SetPropertyBlock(_mpb);

            yield return null;
        }

        // 4. 완료: 결과 확정 및 원본 머티리얼 복원
        _spriteRenderer.GetPropertyBlock(_mpb);
        _mpb.SetFloat(_thresholdId,  0f);
        _mpb.SetFloat(_tintAmountId, 0f);
        _spriteRenderer.SetPropertyBlock(_mpb);

        if (_originalMaterial != null)
            _spriteRenderer.material = _originalMaterial;
        
        _spriteRenderer.enabled = true; // 가시성 최종 보장
    }
}
