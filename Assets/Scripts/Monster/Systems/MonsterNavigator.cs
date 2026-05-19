using UnityEngine;

/// <summary>
/// 경량 2D 네비게이션 시스템 (SRP).
/// NavMesh 대신 Rigidbody2D + 간단한 장애물 회피를 사용.
/// </summary>
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

    /// <summary>목표 위치를 향해 이동 (적대적 추격용)</summary>
    public void MoveToward(Vector2 targetPosition)
    {
        _isDecelerating = false;
        Vector2 desiredDir = ((Vector2)targetPosition - (Vector2)transform.position).normalized;
        Vector2 finalDir = ApplyAvoidance(desiredDir);

        if (finalDir.sqrMagnitude > 0.01f)
            _runtime.CurrentDirection = finalDir;

        _rb.linearVelocity = finalDir * _runtime.CurrentSpeed;
    }

    /// <summary>지정 방향으로 이동 (중립 도망용)</summary>
    public void MoveInDirection(Vector2 direction)
    {
        _isDecelerating = false;
        Vector2 finalDir = ApplyAvoidance(direction.normalized);

        if (finalDir.sqrMagnitude > 0.01f)
            _runtime.CurrentDirection = finalDir;

        _rb.linearVelocity = finalDir * _runtime.CurrentSpeed;
    }

    /// <summary>
    /// 타일 중앙을 향해 주축(상하좌우) 한 방향으로만 이동.
    /// 비주축은 부드럽게 타일 중앙선으로 보정하여 drift 방지.
    /// 도달 시 true 반환.
    /// </summary>
    public bool MoveToTileCenter(Vector2 tileCenter, float reachThreshold)
    {
        _isDecelerating = false;
        Vector2 currentPos = _rb.position;
        Vector2 diff = tileCenter - currentPos;

        if (diff.sqrMagnitude <= reachThreshold * reachThreshold)
        {
            _rb.position = tileCenter;
            _rb.linearVelocity = Vector2.zero;
            return true;
        }

        Vector2 moveDir;
        float correctionSpeed = 10f * Time.deltaTime;

        if (Mathf.Abs(diff.x) > Mathf.Abs(diff.y))
        {
            moveDir = new Vector2(Mathf.Sign(diff.x), 0f);
            float correctedY = Mathf.MoveTowards(currentPos.y, tileCenter.y, correctionSpeed);
            _rb.position = new Vector2(currentPos.x, correctedY);
        }
        else
        {
            moveDir = new Vector2(0f, Mathf.Sign(diff.y));
            float correctedX = Mathf.MoveTowards(currentPos.x, tileCenter.x, correctionSpeed);
            _rb.position = new Vector2(correctedX, currentPos.y);
        }

        _runtime.CurrentDirection = moveDir;
        _rb.linearVelocity = moveDir * _runtime.CurrentSpeed;
        return false;
    }

    /// <summary>현재 위치를 가장 가까운 타일 중앙으로 즉시 스냅</summary>
    public void SnapToTileCenter()
    {
        _rb.position = TileGridHelper.GetTileCenter(_rb.position);
        _rb.linearVelocity = Vector2.zero;
        _isDecelerating = false;
    }

    /// <summary>감속하면서 정지</summary>
    public void Decelerate()
    {
        _isDecelerating = true;
    }

    /// <summary>이동 즉시 정지</summary>
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
