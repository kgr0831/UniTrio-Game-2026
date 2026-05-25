using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(MonsterRuntimeData))]
public sealed class MonsterAnimatorController : MonoBehaviour
{
    [Tooltip("true: 상하좌우 4방향 BlendTree 사용 / false: 좌우 2방향만 사용")]
    [SerializeField] private bool _use4Direction = false;

    private Animator           _animator;
    private MonsterRuntimeData _runtime;
    private bool               _hasDirY;

    private static readonly int _hashIsMoving       = Animator.StringToHash("isMoving");
    private static readonly int _hashIsEating        = Animator.StringToHash("isEating");
    private static readonly int _hashDirX            = Animator.StringToHash("DirX");
    private static readonly int _hashDirY            = Animator.StringToHash("DirY");
    private static readonly int _hashAttack          = Animator.StringToHash("Attack");
    private static readonly int _hashStagger         = Animator.StringToHash("Hit");
    private static readonly int _hashSpeedMultiplier = Animator.StringToHash("SpeedMultiplier");

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _runtime  = GetComponent<MonsterRuntimeData>();
    }

    private void Update()
    {
        if (_runtime == null || _runtime.Data == null) return;

        if (_animator.runtimeAnimatorController != _runtime.Data.AnimController)
        {
            _animator.runtimeAnimatorController = _runtime.Data.AnimController;
            _hasDirY = HasParameter(_animator, _hashDirY);
        }

        bool isMoving = _runtime.CurrentState == MonsterState.Wander ||
                        _runtime.CurrentState == MonsterState.Chase ||
                        _runtime.CurrentState == MonsterState.Flee;

        bool isEating = _runtime.CurrentState == MonsterState.Eat;

        _animator.SetBool(_hashIsMoving, isMoving);
        _animator.SetBool(_hashIsEating, isEating);

        float baseSpeed = _runtime.Data.Speed * 0.01f;
        float speedRatio = baseSpeed > 0.001f ? _runtime.CurrentSpeed / baseSpeed : 1f;
        _animator.SetFloat(_hashSpeedMultiplier, Mathf.Max(speedRatio, 0.5f));

        if (_use4Direction)
            ApplyDirection4();
        else
            ApplyDirection2();
    }

    private void ApplyDirection4()
    {
        Vector2 dir = _runtime.CurrentDirection;
        if (dir.sqrMagnitude < 0.001f) return;

        if (!_hasDirY)
        {
            ApplyDirection2();
            return;
        }

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
        Vector2 dir = _runtime.CurrentDirection;
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y) * 0.5f && Mathf.Abs(dir.x) > 0.1f)
            _animator.SetFloat(_hashDirX, dir.x > 0f ? 1f : -1f);
    }

    public void PlayAttack()
    {
        _animator.SetTrigger(_hashAttack);
    }

    public void PlayHit()
    {
        _animator.SetTrigger(_hashStagger);
    }

    private static bool HasParameter(Animator animator, int nameHash)
    {
        foreach (var p in animator.parameters)
        {
            if (p.nameHash == nameHash)
                return true;
        }
        return false;
    }
}
