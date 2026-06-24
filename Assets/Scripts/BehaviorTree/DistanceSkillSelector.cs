using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어와의 거리에 따라 사용할 스킬 풀을 골라 무작위 실행한다.
/// - 근거리(nearRange 이내): nearSkills (예: 내려찍기 포함 전 스킬)
/// - 원거리(nearRange 밖)  : farSkills  (예: 돌진·투척처럼 원거리에서도 쓸 수 있는 스킬)
/// 한 번 고른 스킬은 끝날 때까지 유지(거리 변화와 무관) → 진행 중 공격이 끊기지 않는다.
/// </summary>
public class DistanceSkillSelector : Node
{
    private readonly List<Node> nearSkills;
    private readonly List<Node> farSkills;
    private readonly float nearRange;
    private Node selectedNode;

    public DistanceSkillSelector(BossBlackboard bb, List<Node> nearSkills, List<Node> farSkills, float nearRange) : base(bb)
    {
        this.nearSkills = nearSkills;
        this.farSkills = farSkills;
        this.nearRange = nearRange;
    }

    public override NodeState Evaluate()
    {
        if (selectedNode == null)
        {
            List<Node> pool = ChoosePool();
            if (pool == null || pool.Count == 0) return NodeState.FAILURE;
            selectedNode = pool[Random.Range(0, pool.Count)];
        }

        NodeState state = selectedNode.Evaluate();
        if (state != NodeState.RUNNING)
            selectedNode = null;
        return state;
    }

    private List<Node> ChoosePool()
    {
        float dist = float.MaxValue;
        if (blackboard.playerTarget != null)
            dist = Vector2.Distance(blackboard.bossTransform.position, blackboard.playerTarget.position);

        return dist <= nearRange ? nearSkills : farSkills;
    }
}
