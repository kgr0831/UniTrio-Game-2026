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

        // 2방향(좌우) 애니메이션 제어: 상하 애니메이션 제거
        float dirX = _runtime.CurrentDirection.x;
        
        // 좌우 방향으로 조금이라도 움직이면 DirX 업데이트 (1 또는 -1)
        if (Mathf.Abs(dirX) > 0.01f)
        {
            _animator.SetFloat(_hashDirX, Mathf.Sign(dirX));
        }
        // 수직으로만 이동할 때는 이전 좌우 방향(DirX)을 그대로 유지

        // 상하 애니메이션(DirY)은 무조건 0으로 고정하여 재생되지 않게 차단
        _animator.SetFloat(_hashDirY, 0f);
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
