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

    private void Awake()
    {
        _rb      = GetComponent<Rigidbody2D>();
        _runtime = GetComponent<MonsterRuntimeData>();
    }

    private void FixedUpdate()
    {
        if (!_isDecelerating) return;

        Vector2 vel = _rb.linearVelocity;
        if (vel.sqrMagnitude < 0.01f)
        {
            _rb.linearVelocity = Vector2.zero;
            _isDecelerating = false;
            return;
        }

        _rb.linearVelocity = Vector2.MoveTowards(vel, Vector2.zero, _deceleration * Time.fixedDeltaTime);
    }

    public void MoveToward(Vector2 targetPosition)
    {
        _isDecelerating = false;
        Vector2 desiredDir = ((Vector2)targetPosition - (Vector2)transform.position).normalized;
        Vector2 finalDir = ApplyAvoidance(desiredDir);

        if (finalDir.sqrMagnitude > 0.01f)
            _runtime.CurrentDirection = finalDir;

        _rb.linearVelocity = finalDir * _runtime.CurrentSpeed;
    }

    public void MoveInDirection(Vector2 direction)
    {
        _isDecelerating = false;
        Vector2 finalDir = ApplyAvoidance(direction.normalized);

        if (finalDir.sqrMagnitude > 0.01f)
            _runtime.CurrentDirection = finalDir;

        _rb.linearVelocity = finalDir * _runtime.CurrentSpeed;
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
            _rb.linearVelocity = Vector2.zero;
            return true;
        }

        if (dist <= reachThreshold)
        {
            _rb.position = Vector2.MoveTowards(currentPos, target, _runtime.CurrentSpeed * Time.deltaTime);
            _rb.linearVelocity = Vector2.zero;
            return (Vector2)_rb.position == target;
        }

        Vector2 desiredDir = diff.normalized;
        Vector2 finalDir = ApplyAvoidance(desiredDir);

        if (finalDir.sqrMagnitude > 0.01f)
            _runtime.CurrentDirection = finalDir;

        _rb.linearVelocity = finalDir * _runtime.CurrentSpeed;
        return false;
    }

    public void SnapToTileCenter()
    {
        if (_rb == null) _rb = GetComponent<Rigidbody2D>();
        _rb.position = TileGridHelper.GetTileCenter(_rb.position);
        _rb.linearVelocity = Vector2.zero;
        _isDecelerating = false;
    }

    public void Decelerate()
    {
        _isDecelerating = true;
    }

    public void Stop()
    {
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
