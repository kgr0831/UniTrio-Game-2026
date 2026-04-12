using UnityEngine;

/// <summary>
/// 경량 2D 네비게이션 시스템 (SRP).
/// NavMesh 대신 Rigidbody2D.MovePosition + 간단한 장애물 회피를 사용.
/// 성능 최적화: Raycast 기반 전방 장애물 회피 (A* 없이).
///
/// 알고리즘:
/// 1. 목표 방향으로 직선 이동 (Rigidbody2D.MovePosition)
/// 2. 전방 Raycast로 장애물 감지 시, 좌/우 방향으로 우회
/// 3. 우회 중에도 목표 방향을 점진적으로 복원 (Steering Behavior)
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(MonsterRuntimeData))]
public sealed class MonsterNavigator : MonoBehaviour
{
    [Header("Obstacle Avoidance")]
    [SerializeField] private LayerMask _obstacleMask;
    [SerializeField] private float     _avoidanceRayLength = 1.5f;
    [SerializeField] private float     _avoidanceAngle     = 45f;

    private Rigidbody2D        _rb;
    private MonsterRuntimeData _runtime;
    private Vector2            _currentDirection;

    private void Awake()
    {
        _rb      = GetComponent<Rigidbody2D>();
        _runtime = GetComponent<MonsterRuntimeData>();
    }

    /// <summary>목표 위치를 향해 이동 (적대적 추격용)</summary>
    public void MoveToward(Vector2 targetPosition)
    {
        Vector2 desiredDir = ((Vector2)targetPosition - (Vector2)transform.position).normalized;
        Vector2 finalDir = ApplyAvoidance(desiredDir);

        if (finalDir.sqrMagnitude > 0.01f)
            _runtime.CurrentDirection = finalDir;

        _rb.linearVelocity = finalDir * _runtime.CurrentSpeed;
    }

    /// <summary>지정 방향으로 이동 (중립 도망용)</summary>
    public void MoveInDirection(Vector2 direction)
    {
        Vector2 finalDir = ApplyAvoidance(direction.normalized);

        if (finalDir.sqrMagnitude > 0.01f)
            _runtime.CurrentDirection = finalDir;

        _rb.linearVelocity = finalDir * _runtime.CurrentSpeed;
    }

    /// <summary>이동 즉시 정지</summary>
    public void Stop()
    {
        _rb.linearVelocity = Vector2.zero;
    }

    /// <summary>
    /// 전방 Raycast로 장애물 회피.
    /// 성능: Raycast 최대 3회 (정면 + 좌 + 우)
    /// </summary>
    private Vector2 ApplyAvoidance(Vector2 desiredDir)
    {
        // 정면 체크
        if (!Physics2D.Raycast(transform.position, desiredDir,
                               _avoidanceRayLength, _obstacleMask))
        {
            return desiredDir; // 장애물 없음 → 직진
        }

        // 좌측 우회
        Vector2 leftDir = Quaternion.Euler(0, 0, _avoidanceAngle) * desiredDir;
        if (!Physics2D.Raycast(transform.position, leftDir,
                               _avoidanceRayLength, _obstacleMask))
        {
            return leftDir;
        }

        // 우측 우회
        Vector2 rightDir = Quaternion.Euler(0, 0, -_avoidanceAngle) * desiredDir;
        if (!Physics2D.Raycast(transform.position, rightDir,
                               _avoidanceRayLength, _obstacleMask))
        {
            return rightDir;
        }

        return Vector2.zero;
    }
}
