using UnityEngine;

/// <summary>
/// 적대적 몹 (플레이어 발견 시 추격 후 공격)
/// </summary>
[RequireComponent(typeof(DetectionSystem))]
[RequireComponent(typeof(MonsterNavigator))]
[RequireComponent(typeof(WanderSystem))]
[RequireComponent(typeof(MonsterAttackHandler))]
public sealed class HostileMonster : MonsterBase
{
    private DetectionSystem      _detection;
    private MonsterNavigator     _navigator;
    private WanderSystem         _wander;
    private MonsterAttackHandler _attacker;

    protected override void Awake()
    {
        base.Awake();
        _detection = GetComponent<DetectionSystem>();
        _navigator = GetComponent<MonsterNavigator>();
        _wander    = GetComponent<WanderSystem>();
        _attacker  = GetComponent<MonsterAttackHandler>();
    }

    protected override BTNode BuildBT()
    {
        // 1. 공격 조건 노드 (사거리 내에 플레이어가 있고 쿨다운 0)
        var checkAttackCondition = new BTCondition(() => 
        {
            if (!_detection.HasTarget) return false;
            float dist = Vector2.Distance(transform.position, _detection.DetectedTarget.position);
            
            // 이 몬스터의 공격 사거리를 SO 데이터에서 확인
            // 간단하게 DetectionRadius보다 작은 임의의 사거리 또는 공격 모양 기준값 (예: 원형이면 CircleRadius)
            float attackRange = 1.5f; 
            if (_runtime.Data.AttackShape.ShapeType == AttackShapeType.Circle)
                attackRange = _runtime.Data.AttackShape.CircleRadius;

            return dist <= attackRange && _runtime.AttackCooldownTimer <= 0f;
        });

        var attackAction = new BTAction(() =>
        {
            _navigator.Stop();
            _runtime.CurrentState = MonsterState.Attack;
            _attacker.ExecuteAttack(_detection.DetectedTarget.position);
            // 한 번 공격하면 Success(또는 Running 후 대기) 처리
            return BTStatus.Success;
        });

        BTSequence attackSequence = new BTSequence(new BTNode[] { checkAttackCondition, attackAction });

        // 2. 추격 노드
        var checkChaseCondition = new BTCondition(() => _detection.HasTarget);
        var chaseAction = new BTAction(() =>
        {
            _runtime.CurrentState = MonsterState.Chase;
            _navigator.MoveToward(_detection.DetectedTarget.position);
            return BTStatus.Running; // 쫓는 중
        });

        BTSequence chaseSequence = new BTSequence(new BTNode[] { checkChaseCondition, chaseAction });

        // 3. 배회 노드 (아무것도 안 함)
        var wanderAction = new BTAction(() =>
        {
            _runtime.CurrentState = MonsterState.Wander;
            Vector2 dir = _wander.GetWanderDirection();
            _navigator.MoveInDirection(dir);
            return BTStatus.Running;
        });

        // Selector: 공격 시도 -> 안 되면 추격 시도 -> 안 되면 배회
        return new BTSelector(new BTNode[] { attackSequence, chaseSequence, wanderAction });
    }
}
