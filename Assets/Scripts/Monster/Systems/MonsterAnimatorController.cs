using UnityEngine;

/// <summary>
/// 몹 애니메이션 전담 컴포넌트 (SRP).
/// MonsterRuntimeData의 State를 모니터링하여 파라미터 제어.
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(MonsterRuntimeData))]
public sealed class MonsterAnimatorController : MonoBehaviour
{
    private Animator           _animator;
    private MonsterRuntimeData _runtime;

    private static readonly int _hashIsMoving = Animator.StringToHash("isMoving");
    private static readonly int _hashDirX     = Animator.StringToHash("DirX");
    private static readonly int _hashDirY     = Animator.StringToHash("DirY");
    private static readonly int _hashAttack   = Animator.StringToHash("Attack");
    private static readonly int _hashStagger  = Animator.StringToHash("Hit");

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _runtime  = GetComponent<MonsterRuntimeData>();
    }

    private void Update()
    {
        if (_runtime == null || _runtime.Data == null) return;

        // 런타임에 SO의 지정 컨트롤러로 덮어쓰기 (풀링 재사용 시 갱신)
        if (_animator.runtimeAnimatorController != _runtime.Data.AnimController)
            _animator.runtimeAnimatorController = _runtime.Data.AnimController;

        bool isMoving = _runtime.CurrentState == MonsterState.Wander || 
                        _runtime.CurrentState == MonsterState.Chase ||
                        _runtime.CurrentState == MonsterState.Flee;

        _animator.SetBool(_hashIsMoving, isMoving);

        // 4방향 애니메이션 제어를 위한 방향 전달
        _animator.SetFloat(_hashDirX, _runtime.CurrentDirection.x);
        _animator.SetFloat(_hashDirY, _runtime.CurrentDirection.y);
    }

    public void PlayAttack()
    {
        _animator.SetTrigger(_hashAttack);
    }

    public void PlayHit()
    {
        _animator.SetTrigger(_hashStagger);
    }
}
