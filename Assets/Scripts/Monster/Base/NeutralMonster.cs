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
        // [반응형 BT 구조] BTSelector와 BTSequence가 무상태로 변경됨에 따라,
        // 매 프레임 높은 우선순위(Flee)부터 다시 평가하여 상한선(1.5배 거리)을 벗어날 때까지 도망을 유지합니다.

        // 1. 도망 노드 (플레이어가 감지범위 내에 있음)
        var checkFleeCondition = new BTCondition(() => _detection.HasTarget);
        
        var fleeAction = new BTAction(() =>
        {
            _runtime.CurrentState = MonsterState.Flee;

            if (_runtime.Data != null)
                _runtime.CurrentSpeed = _runtime.Data.Speed * 0.01f;

            // 도망 로직: 거리가 충분히(1.5배) 멀어지면 감지 해제
            float dist = Vector2.Distance(transform.position, _detection.DetectedTarget.position);
            if (dist > _runtime.Data.DetectionRadius * 1.5f)
            {
                _detection.ForceRelease();
                _navigator.Stop();
                
                // 새로운 배회 거점 설정 및 배회 방향 즉시 재계산 유도
                _wander.SetBasePosition(transform.position);
                _wander.ForceRecalculate();

                return BTStatus.Success; // 도망 완료 -> 다음 프레임부터 Wander로 전환
            }

            // 플레이어 반대 방향 계산
            Vector2 fleeDir = ((Vector2)transform.position - (Vector2)_detection.DetectedTarget.position).normalized;
            _navigator.MoveInDirection(fleeDir);

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
