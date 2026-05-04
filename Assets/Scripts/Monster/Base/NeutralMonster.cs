using UnityEngine;

/// <summary>
/// 중립 몹 (기본 배회, 공격받거나 플레이어 감지 시 반대 방향으로 도망)
/// </summary>
[RequireComponent(typeof(DetectionSystem))]
[RequireComponent(typeof(MonsterNavigator))]
[RequireComponent(typeof(WanderSystem))]
public sealed class NeutralMonster : MonsterBase
{
    private DetectionSystem  _detection;
    private MonsterNavigator _navigator;
    private WanderSystem     _wander;

    protected override void Awake()
    {
        base.Awake();
        _detection = GetComponent<DetectionSystem>();
        _navigator = GetComponent<MonsterNavigator>();
        _wander    = GetComponent<WanderSystem>();
    }

    // --- [Mob Flee AI State] ---
    private int _lastCommittedIndex = -1;
    private int _zigzagShift = 0;
    private float _zigzagTimer;
    private float _directionLockTimer;
    private Vector2 _currentMoveDirection;

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

    protected override BTNode BuildBT()
    {
        // 1. 도망 노드 (MobFleeAI 기반 수정)
        var checkFleeCondition = new BTCondition(() => _detection.HasTarget);
        
        var fleeAction = new BTAction(() =>
        {
            _runtime.CurrentState = MonsterState.Flee;
            if (_runtime.Data != null)
                _runtime.CurrentSpeed = _runtime.Data.Speed * 0.01f;

            Transform target = _detection.DetectedTarget;
            float dist = Vector2.Distance(transform.position, target.position);
            
            // 해제 조건 (1.5배 거리)
            if (dist > _runtime.Data.DetectionRadius * 1.5f)
            {
                _detection.ForceRelease();
                _navigator.Stop();
                _lastCommittedIndex = -1;
                _currentMoveDirection = Vector2.zero;
                _wander.SetBasePosition(transform.position);
                _wander.ForceRecalculate();
                return BTStatus.Success;
            }

            // [Step 1] 순수 도주 벡터 및 8방향 양자화
            Vector2 pureFleeVector = ((Vector2)transform.position - (Vector2)target.position).normalized;
            float angle = Mathf.Atan2(pureFleeVector.y, pureFleeVector.x) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360f;
            int baseDirectionIndex = Mathf.RoundToInt(angle / 45f) % 8;

            // [Step 2] 핑퐁(Ping-Pong) 지그재그 기동 (1.0초 고정 주기)
            if (_zigzagShift == 0) _zigzagShift = 1; // 초기화

            _zigzagTimer -= Time.deltaTime;
            if (_zigzagTimer <= 0)
            {
                // 기존 오프셋에 -1을 곱해 강제로 반전 (1 -> -1 -> 1)
                _zigzagShift *= -1;
                _zigzagTimer = 1.0f; // 1초 고정
            }
            int finalDesiredIndex = (baseDirectionIndex + _zigzagShift + 8) % 8;
            Vector2 desiredDirection = DIRECTIONS[finalDesiredIndex];

            // [Step 3] 지터링(지지직) 방지 및 예외 방향 전환 로직
            _directionLockTimer -= Time.deltaTime;

            if (_lastCommittedIndex == -1 || _directionLockTimer <= 0)
            {
                // [Step 4] 장애물 회피 및 최적 방향 검색 (Ping-Pong 우선순위 반영)
                // 단순히 baseDirectionIndex를 먼저 체크하면 지그재그를 무시하고 직진하려 함.
                // 따라서 finalDesiredIndex(지그재그가 적용된 방향)를 최우선으로 검사해야 함.
                
                int safeIndex = -1;
                // 검색 순서: 1. 지그재그 방향, 2. 직진 방향, 3. 반대쪽 지그재그 방향
                int[] searchOffsets = { 0, -_zigzagShift, _zigzagShift }; 

                LayerMask obstacleMask = LayerMask.GetMask("Wall", "Obstacle");

                foreach (int offset in searchOffsets)
                {
                    // finalDesiredIndex 기준으로 오프셋을 더해 우선순위 검색
                    int checkIndex = (finalDesiredIndex + offset + 8) % 8;
                    
                    // Hemisphere 제한: 베이스 방향(baseDirectionIndex) 기준 ±1을 벗어나면 안 됨
                    int diff = Mathf.Abs((checkIndex - baseDirectionIndex + 12) % 8 - 4);
                    if (diff > 1) continue;

                    Vector2 checkDir = DIRECTIONS[checkIndex];
                    if (!Physics2D.CircleCast(transform.position, 0.4f, checkDir, 1.2f, obstacleMask))
                    {
                        safeIndex = checkIndex;
                        break;
                    }
                }

                // 만약 모든 유효 방향(±1)이 막혔다면 어쩔 수 없이 baseDirectionIndex 강제 유지
                if (safeIndex == -1) safeIndex = baseDirectionIndex;

                // 방향 전환 결정 (현재 방향과 다르거나 처음인 경우)
                if (safeIndex != _lastCommittedIndex)
                {
                    float dotProduct = Vector2.Dot(_currentMoveDirection, DIRECTIONS[safeIndex]);
                    
                    // 90도 이상 꺾이거나 타이머가 끝났을 때만 실제 방향 갱신
                    if (_directionLockTimer <= 0 || dotProduct <= 0 || _lastCommittedIndex == -1)
                    {
                        _lastCommittedIndex = safeIndex;
                        _currentMoveDirection = DIRECTIONS[safeIndex];
                        _directionLockTimer = 0.25f;
                    }
                }
            }

            // [Step 5] 실제 이동 적용
            _navigator.MoveInDirection(_currentMoveDirection);
            return BTStatus.Running;
        });

        // 반응형 시퀀스: 조건 실패 시 즉시 Selector의 다음 자식(Wander)으로 제어권이 넘어감
        BTSequence fleeSequence = new BTSequence(new BTNode[] { checkFleeCondition, fleeAction });

        // 2. 배회 노드 (평상시)
        var wanderAction = new BTAction(() =>
        {
            _runtime.CurrentState = MonsterState.Wander;
            
            if (_runtime.Data != null)
                _runtime.CurrentSpeed = _runtime.Data.Speed * 0.01f * 0.5f;

            Vector2 dir = _wander.GetWanderDirection();
            _navigator.MoveInDirection(dir);
            return BTStatus.Running;
        });

        return new BTSelector(new BTNode[] { fleeSequence, wanderAction });
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (_health != null)
            _health.OnHit += HandleHitFlee;
    }

    private void OnDisable()
    {
        if (_health != null)
            _health.OnHit -= HandleHitFlee;
    }

    private void HandleHitFlee()
    {
        // 플레이어에 의해 타격되었을 때, 시야 밖이라도 즉시 감지 대상으로 등록하여 도망 유도
        if (!_detection.HasTarget)
        {
            // 성능 가이드라인에 따라 가끔 호출되는 이벤트 내에서만 사용
            var player = GameObject.FindWithTag("Player");
            if (player != null)
                _runtime.DetectedPlayer = player.transform;
        }
    }
}
