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

    protected override BTNode BuildBT()
    {
        // 1. 도망 노드 (플레이어가 감지범위 내에 있음)
        var checkFleeCondition = new BTCondition(() => _detection.HasTarget);
        
        var fleeAction = new BTAction(() =>
        {
            _runtime.CurrentState = MonsterState.Flee;

            // 도망 시에는 정상 속도로 달림 (SO 데이터 기준 100 = 1 이므로 0.01 곱연산)
            if (_runtime.Data != null)
            {
                _runtime.CurrentSpeed = _runtime.Data.Speed * 0.01f;
            }

            // 추가 로직: 거리가 너무 떨어지면 안전하다고 판단하여 감지 해제 (감지 반경의 1.5배)
            float dist = Vector2.Distance(transform.position, _detection.DetectedTarget.position);
            if (dist > _runtime.Data.DetectionRadius * 1.5f)
            {
                _detection.ForceRelease();
                _navigator.Stop();
                
                // 도망이 끝난 자리를 새로운 배회 거점으로 설정
                _wander.SetBasePosition(transform.position);

                return BTStatus.Success; // 도망 성공 -> 다음 프레임부터 배회
            }

            // 반대 방향으로 도망
            Vector2 fleeDir = ((Vector2)transform.position - (Vector2)_detection.DetectedTarget.position).normalized;
            _navigator.MoveInDirection(fleeDir);

            return BTStatus.Running;
        });

        BTSequence fleeSequence = new BTSequence(new BTNode[] { checkFleeCondition, fleeAction });

        // 2. 배회 노드 (평상시)
        var wanderAction = new BTAction(() =>
        {
            _runtime.CurrentState = MonsterState.Wander;
            
            // 배회 시 스피드는 지정된 최고 속도의 절반! (100 기준 0.5가 됨)
            if (_runtime.Data != null)
            {
                _runtime.CurrentSpeed = _runtime.Data.Speed * 0.01f * 0.5f;
            }

            Vector2 dir = _wander.GetWanderDirection();
            _navigator.MoveInDirection(dir);
            return BTStatus.Running;
        });

        return new BTSelector(new BTNode[] { fleeSequence, wanderAction });
    }

    // 중립몹 로직 보강: 맞았을 때 플레이어 위치를 강제 감시망에 넣기
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
        // 아직 플레이어를 못찾았더라도, 맞으면 즉시 반항(도망) 시작
        if (!_detection.HasTarget)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                 // 멀리서 원거리 공격을 맞아도 타격자(Player)를 즉시 감지 대상으로 지정하여 도망치도록 함
                 _runtime.DetectedPlayer = player.transform;
            }
        }
    }
}
