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

    // 접근 방향(좌/우) 히스테리시스: -1=플레이어 왼쪽, +1=오른쪽. 정바로 아래/위에선 기존 쪽 유지.
    private int _approachSide = 0;

    /// <summary>이 몬스터의 공격 사거리 (SO 데이터 기준).</summary>
    private float GetAttackRange()
    {
        if (_runtime != null && _runtime.Data != null
            && _runtime.Data.AttackShape.ShapeType == AttackShapeType.Circle)
            return _runtime.Data.AttackShape.CircleRadius;
        return 1.5f;
    }

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
            
            return dist <= GetAttackRange() && _runtime.AttackCooldownTimer <= 0f;
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

            Vector2 playerPos = _detection.DetectedTarget.position;
            float dx = transform.position.x - playerPos.x;
            const float sideMargin = 0.4f; // 이 안쪽(정바로 아래/위)에선 기존 접근 쪽 유지 → 떨림 방지
            if (dx < -sideMargin)      _approachSide = -1; // 곰이 플레이어 왼쪽
            else if (dx > sideMargin)  _approachSide =  1; // 오른쪽
            if (_approachSide == 0)    _approachSide = (dx <= 0f) ? -1 : 1;

            // 플레이어의 좌/우 옆(같은 Y)으로 접근 → 아래로 파고들지 않고 좌우 공격 자세를 잡는다.
            float standoff = GetAttackRange() * 0.9f;
            Vector2 approach = new Vector2(playerPos.x + _approachSide * standoff, playerPos.y);
            _navigator.MoveToward(approach);
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
