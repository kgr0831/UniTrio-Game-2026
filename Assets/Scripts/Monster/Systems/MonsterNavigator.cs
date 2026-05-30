using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(MonsterRuntimeData))]
public sealed class MonsterNavigator : MonoBehaviour
{
    [Header("Obstacle Avoidance")]
    [SerializeField] private LayerMask _obstacleMask;
    [SerializeField] private float     _avoidanceRayLength = 1.5f;
    [SerializeField] private float     _avoidanceAngle     = 45f;

    [Header("Deceleration")]
    [SerializeField] private float _deceleration = 8f;


    private Rigidbody2D        _rb;
    private MonsterRuntimeData _runtime;
    private bool               _isDecelerating;

    // 충돌 밀림 방지: 매 FixedUpdate에서 의도한 속도로 덮어쓰기
    private Vector2 _intendedVelocity;

    private static PhysicsMaterial2D _sharedNoPushMat;

    private void Awake()
    {
        _rb      = GetComponent<Rigidbody2D>();
        _runtime = GetComponent<MonsterRuntimeData>();

        // ── 충돌 밀림 방지 설정 ──
        _rb.mass         = 100f;
        _rb.linearDamping = 0f;
        _rb.gravityScale  = 0f;
        _rb.constraints   = RigidbodyConstraints2D.FreezeRotation;

        if (_sharedNoPushMat == null)
            _sharedNoPushMat = new PhysicsMaterial2D("NoPush")
                { friction = 0f, bounciness = 0f };

        foreach (var col in GetComponentsInChildren<Collider2D>())
            col.sharedMaterial = _sharedNoPushMat;
    }

    private void FixedUpdate()
    {
        // 스태거 중에는 이동 완전 차단 (AI가 MoveToward 등을 호출해도 무시)
        if (_runtime.IsStaggered)
        {
            _intendedVelocity      = Vector2.zero;
            _rb.linearVelocity     = Vector2.zero;
            return;
        }

        // ── 충돌 밀림 방지: 매 물리 스텝마다 의도한 속도로 강제 복원 ──
        _rb.linearVelocity = _intendedVelocity;

        if (!_isDecelerating) return;

        if (_intendedVelocity.sqrMagnitude < 0.01f)
        {
            _intendedVelocity = Vector2.zero;
            _rb.linearVelocity = Vector2.zero;
            _isDecelerating = false;
            return;
        }

        _intendedVelocity = Vector2.MoveTowards(
            _intendedVelocity, Vector2.zero, _deceleration * Time.fixedDeltaTime);
        _rb.linearVelocity = _intendedVelocity;
    }

    public void MoveToward(Vector2 targetPosition)
    {
        _isDecelerating = false;
        Vector2 desiredDir = ((Vector2)targetPosition - (Vector2)transform.position).normalized;
        Vector2 finalDir = ApplyAvoidance(desiredDir);

        if (finalDir.sqrMagnitude > 0.01f)
            _runtime.CurrentDirection = finalDir;

        _intendedVelocity = finalDir * _runtime.CurrentSpeed;
        _rb.linearVelocity = _intendedVelocity;
    }

    public void MoveInDirection(Vector2 direction)
    {
        _isDecelerating = false;
        Vector2 finalDir = ApplyAvoidance(direction.normalized);

        if (finalDir.sqrMagnitude > 0.01f)
            _runtime.CurrentDirection = finalDir;

        _intendedVelocity = finalDir * _runtime.CurrentSpeed;
        _rb.linearVelocity = _intendedVelocity;
    }

    public bool MoveToTarget(Vector2 target, float reachThreshold)
    {
        _isDecelerating = false;
        Vector2 currentPos = _rb.position;
        Vector2 diff = target - currentPos;
        float dist = diff.magnitude;

        if (dist <= reachThreshold * 0.3f)
        {
            _rb.position = target;
            _intendedVelocity = Vector2.zero;
            _rb.linearVelocity = Vector2.zero;
            return true;
        }

        if (dist <= reachThreshold)
        {
            _rb.position = Vector2.MoveTowards(currentPos, target, _runtime.CurrentSpeed * Time.deltaTime);
            _intendedVelocity = Vector2.zero;
            _rb.linearVelocity = Vector2.zero;
            return (Vector2)_rb.position == target;
        }

        Vector2 desiredDir = diff.normalized;
        Vector2 finalDir = ApplyAvoidance(desiredDir);

        if (finalDir.sqrMagnitude > 0.01f)
            _runtime.CurrentDirection = finalDir;

        _intendedVelocity = finalDir * _runtime.CurrentSpeed;
        _rb.linearVelocity = _intendedVelocity;
        return false;
    }

    public void SnapToTileCenter()
    {
        if (_rb == null) _rb = GetComponent<Rigidbody2D>();
        _rb.position = TileGridHelper.GetTileCenter(_rb.position);
        _intendedVelocity = Vector2.zero;
        _rb.linearVelocity = Vector2.zero;
        _isDecelerating = false;
    }

    public void Decelerate()
    {
        _isDecelerating = true;
    }

    public void Stop()
    {
        _intendedVelocity = Vector2.zero;
        _rb.linearVelocity = Vector2.zero;
        _isDecelerating = false;
    }

    private Vector2 ApplyAvoidance(Vector2 desiredDir)
    {
        Vector2 origin = (Vector2)transform.position + (desiredDir * 0.1f);

        if (!Physics2D.Raycast(origin, desiredDir,
                               _avoidanceRayLength, _obstacleMask))
        {
            return desiredDir;
        }

        Vector2 leftDir = Quaternion.Euler(0, 0, _avoidanceAngle) * desiredDir;
        if (!Physics2D.Raycast(transform.position, leftDir,
                               _avoidanceRayLength, _obstacleMask))
        {
            return leftDir;
        }

        Vector2 rightDir = Quaternion.Euler(0, 0, -_avoidanceAngle) * desiredDir;
        if (!Physics2D.Raycast(transform.position, rightDir,
                               _avoidanceRayLength, _obstacleMask))
        {
            return rightDir;
        }

        return Vector2.zero;
    }
}
