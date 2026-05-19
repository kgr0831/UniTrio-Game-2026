using UnityEngine;

/// <summary>
/// 몹 애니메이션 전담 컴포넌트 (SRP).
/// MonsterRuntimeData의 State를 모니터링하여 파라미터 제어.
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(MonsterRuntimeData))]
public sealed class MonsterAnimatorController : MonoBehaviour
{
    [Tooltip("true: 상하좌우 4방향 BlendTree 사용 / false: 좌우 2방향만 사용")]
    [SerializeField] private bool _use4Direction = false;

    private Animator           _animator;
    private MonsterRuntimeData _runtime;

    private static readonly int _hashIsMoving  = Animator.StringToHash("isMoving");
    private static readonly int _hashDirX      = Animator.StringToHash("DirX");
    private static readonly int _hashDirY      = Animator.StringToHash("DirY");
    private static readonly int _hashAttack    = Animator.StringToHash("Attack");
    private static readonly int _hashStagger   = Animator.StringToHash("Hit");
    private static readonly int _hashAnimSpeed = Animator.StringToHash("AnimSpeed");

    private float _baseAnimSpeed;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _runtime  = GetComponent<MonsterRuntimeData>();
    }

    private void Update()
    {
        if (_runtime == null || _runtime.Data == null) return;

        if (_animator.runtimeAnimatorController != _runtime.Data.AnimController)
            _animator.runtimeAnimatorController = _runtime.Data.AnimController;

        bool isMoving = _runtime.CurrentState == MonsterState.Wander ||
                        _runtime.CurrentState == MonsterState.Chase ||
                        _runtime.CurrentState == MonsterState.Flee;

        _animator.SetBool(_hashIsMoving, isMoving);

        // 이동속도에 비례하여 애니메이션 속도 조절
        float baseSpeed = _runtime.Data.Speed * 0.01f;
        float speedRatio = baseSpeed > 0.001f ? _runtime.CurrentSpeed / baseSpeed : 1f;
        _animator.speed = Mathf.Max(speedRatio, 0.5f);

        if (_use4Direction)
            ApplyDirection4();
        else
            ApplyDirection2();
    }

    private void ApplyDirection4()
    {
        Vector2 dir = _runtime.CurrentDirection;
        if (dir.sqrMagnitude < 0.001f) return;

        // 주축(dominant axis) 기반 4방향 양자화
        if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
        {
            _animator.SetFloat(_hashDirX, Mathf.Sign(dir.x));
            _animator.SetFloat(_hashDirY, 0f);
        }
        else
        {
            _animator.SetFloat(_hashDirX, 0f);
            _animator.SetFloat(_hashDirY, Mathf.Sign(dir.y));
        }
    }

    private void ApplyDirection2()
    {
        float dirX = _runtime.CurrentDirection.x;
        if (Mathf.Abs(dirX) > 0.01f)
            _animator.SetFloat(_hashDirX, Mathf.Sign(dirX));

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
