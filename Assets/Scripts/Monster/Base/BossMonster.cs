using UnityEngine;

/// <summary>
/// 보스 몹 (사거리 제한 없이 무한 추격 + 지속 공격 패턴)
/// </summary>
[RequireComponent(typeof(MonsterNavigator))]
[RequireComponent(typeof(MonsterAttackHandler))]
public sealed class BossMonster : MonsterBase
{
    private MonsterNavigator     _navigator;
    private MonsterAttackHandler _attacker;
    private Transform            _playerTransform;

    protected override void Awake()
    {
        base.Awake();
        _navigator = GetComponent<MonsterNavigator>();
        _attacker  = GetComponent<MonsterAttackHandler>();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        // 보스는 스폰 시 플레이어를 한 번 찾은 뒤 영구 추적함. Find 계열이지만 보스는 한 번만 생기니 용인.
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) _playerTransform = p.transform;
    }

    protected override BTNode BuildBT()
    {
        var hasPlayerCondition = new BTCondition(() => _playerTransform != null);

        var attackOrChaseAction = new BTAction(() =>
        {
            float dist = Vector2.Distance(transform.position, _playerTransform.position);
            
            float attackRange = 2f; 
            if (_runtime.Data.AttackShape.ShapeType == AttackShapeType.Circle)
                attackRange = _runtime.Data.AttackShape.CircleRadius;

            if (dist <= attackRange && _runtime.AttackCooldownTimer <= 0f)
            {
                // 공격
                _navigator.Stop();
                _runtime.CurrentState = MonsterState.Attack;
                _attacker.ExecuteAttack(_playerTransform.position);
                return BTStatus.Success;
            }
            else
            {
                // 추격
                _runtime.CurrentState = MonsterState.Chase;
                _navigator.MoveToward(_playerTransform.position);
                return BTStatus.Running;
            }
        });

        // 플레이어가 있으면 무조건 추격 OR 공격
        return new BTSequence(new BTNode[] { hasPlayerCondition, attackOrChaseAction });
    }
}
