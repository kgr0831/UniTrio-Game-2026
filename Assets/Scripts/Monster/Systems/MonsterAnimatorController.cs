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
        // 회피 저스트 카운터 중에는 애니 파라미터 갱신 정지 (speed=0은 MonsterFreezeManager가 처리)
        if (MonsterFreezeManager.IsFrozen) return;

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

    private int _facingX = 1; // 2방향 좌우 향함 (히스테리시스로만 변경)
    private void ApplyDirection2()
    {
        Vector2 dir = _runtime.CurrentDirection;
        // 히스테리시스: 현재 향한 방향과 '확실히' 반대로 움직일 때만 좌우를 뒤집는다(정바로 위/아래·미세 흔들림에 떨지 않음).
        const float flipThreshold = 0.35f; // 정규화 dir 기준
        if (_facingX >= 0 && dir.x < -flipThreshold)      _facingX = -1;
        else if (_facingX < 0 && dir.x >  flipThreshold)  _facingX =  1;
        _animator.SetFloat(_hashDirX, _facingX);
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
