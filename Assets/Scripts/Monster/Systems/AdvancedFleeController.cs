using UnityEngine;

/// <summary>
/// [AdvancedFleeController]
/// 10년 차 시니어 프로그래머 관점에서 설계된 고성능 8방향 AI 도주 시스템.
/// 지터링 방지, 이산적 지그재그 기동, 8방향 컨텍스트 스티어링 장애물 회피를 통합 처리합니다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public sealed class AdvancedFleeController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _detectionRadius = 10f;

    [Header("Zigzag Settings")]
    [SerializeField] private float _zigzagInterval = 1.0f;

    [Header("Hysteresis Settings")]
    [SerializeField] private float _directionLockDuration = 0.25f;

    [Header("Obstacle Avoidance")]
    [SerializeField] private LayerMask _obstacleMask;
    [SerializeField] private float _rayDistance = 1.2f;

    // 8방향 정규화 벡터 (0:우, 1:우상, 2:상, 3:좌상, 4:좌, 5:좌하, 6:하, 7:우하)
    private static readonly Vector2[] DIRECTIONS = new Vector2[]
    {
        Vector2.right,
        new Vector2(1, 1).normalized,
        Vector2.up,
        new Vector2(-1, 1).normalized,
        Vector2.left,
        new Vector2(-1, -1).normalized,
        Vector2.down,
        new Vector2(1, -1).normalized
    };

    private Rigidbody2D _rb;
    private Transform _target;

    private int _lastCommittedIndex = -1;
    private int _zigzagShift = 1;
    private float _zigzagTimer;
    private float _directionLockTimer;
    private Vector2 _currentMoveDirection;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null) _target = player.transform;
    }

    private void FixedUpdate()
    {
        if (_target == null) return;
        ExecuteFleeLogic();
    }

    private void ExecuteFleeLogic()
    {
        Vector2 currentPos = transform.position;
        Vector2 targetPos = _target.position;
        Vector2 rawFleeVec = (currentPos - targetPos);

        if (rawFleeVec.sqrMagnitude > _detectionRadius * _detectionRadius)
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        // [Step 1] 순수 도주 벡터 및 8방향 양자화
        Vector2 pureFleeVector = rawFleeVec.normalized;
        float angle = Mathf.Atan2(pureFleeVector.y, pureFleeVector.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360f;
        int baseDirectionIndex = Mathf.RoundToInt(angle / 45f) % 8;

        // [Step 2] 핑퐁 지그재그
        _zigzagTimer -= Time.fixedDeltaTime;
        if (_zigzagTimer <= 0)
        {
            _zigzagShift *= -1;
            _zigzagTimer = _zigzagInterval;
        }
        int finalDesiredIndex = (baseDirectionIndex + _zigzagShift + 8) % 8;

        // [Step 3 & 4] 지터링 방지 및 장애물 회피 (우선순위 수정)
        _directionLockTimer -= Time.fixedDeltaTime;

        if (_lastCommittedIndex == -1 || _directionLockTimer <= 0)
        {
            int safeIndex = -1;
            // 지그재그 방향(0)을 최우선으로, 그 다음 직진(-shift), 그 다음 반대쪽(shift) 순서로 검색
            int[] searchOffsets = { 0, -_zigzagShift, _zigzagShift };
            
            foreach (int offset in searchOffsets)
            {
                int checkIndex = (finalDesiredIndex + offset + 8) % 8;
                
                // Hemisphere 제한 (±1)
                int diff = Mathf.Abs((checkIndex - baseDirectionIndex + 12) % 8 - 4);
                if (diff > 1) continue;

                Vector2 checkDir = DIRECTIONS[checkIndex];
                if (!Physics2D.CircleCast(transform.position, 0.4f, checkDir, _rayDistance, _obstacleMask))
                {
                    safeIndex = checkIndex;
                    break;
                }
            }

            if (safeIndex == -1) safeIndex = baseDirectionIndex;

            if (safeIndex != _lastCommittedIndex)
            {
                float dot = Vector2.Dot(_currentMoveDirection, DIRECTIONS[safeIndex]);
                if (_directionLockTimer <= 0 || dot <= 0 || _lastCommittedIndex == -1)
                {
                    _lastCommittedIndex = safeIndex;
                    _currentMoveDirection = DIRECTIONS[safeIndex];
                    _directionLockTimer = _directionLockDuration;
                }
            }
        }

        _rb.linearVelocity = _currentMoveDirection * _moveSpeed;
    }
}
