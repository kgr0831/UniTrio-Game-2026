using UnityEngine;
using System.Collections;

/// <summary>
/// 적에게 부착되어 일정 시간 동안 이동속도를 감소시킵니다.
/// MonsterRuntimeData.CurrentSpeed를 직접 조작합니다.
/// </summary>
public class EnemySlowEffect : MonoBehaviour
{
    private MonsterRuntimeData _runtime;
    private Coroutine _slowCoroutine;

    private void Awake()
    {
        _runtime = GetComponentInParent<MonsterRuntimeData>();
        if (_runtime == null)
            _runtime = GetComponent<MonsterRuntimeData>();
    }

    /// <summary>
    /// 슬로우를 적용합니다. 이미 슬로우 중이면 기존 슬로우를 덮어씁니다.
    /// </summary>
    /// <param name="slowMultiplier">남길 속도 비율 (0.25 = 75% 감소)</param>
    /// <param name="duration">지속 시간(초)</param>
    public void Apply(float slowMultiplier, float duration)
    {
        if (_runtime == null) return;

        if (_slowCoroutine != null)
            StopCoroutine(_slowCoroutine);

        _slowCoroutine = StartCoroutine(SlowRoutine(slowMultiplier, duration));
    }

    private IEnumerator SlowRoutine(float slowMultiplier, float duration)
    {
        float originalSpeed = _runtime.Data != null ? _runtime.Data.Speed : _runtime.CurrentSpeed;
        _runtime.CurrentSpeed = originalSpeed * slowMultiplier;

        yield return new WaitForSeconds(duration);

        // 아직 이 컴포넌트가 살아있으면 속도 복원
        if (_runtime != null)
            _runtime.CurrentSpeed = originalSpeed;

        _slowCoroutine = null;
        Destroy(this);
    }

    private void OnDestroy()
    {
        // 도중에 적이 죽거나 오브젝트가 파괴될 경우 코루틴 정리
        if (_slowCoroutine != null)
            StopCoroutine(_slowCoroutine);
    }
}
