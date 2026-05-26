using UnityEngine;

public class ActionNode_Idle : Node
{
    public ActionNode_Idle(BossBlackboard bb) : base(bb) { }
    public override NodeState Evaluate()
    {
        if (blackboard.isAttacking) return NodeState.FAILURE;
        blackboard.rb.linearVelocity = Vector2.zero;
        blackboard.anim.SetBool("isMove", false);
        return NodeState.SUCCESS;
    }
}

public class CheckPlayerDistance : Node
{
    private float range;
    public CheckPlayerDistance(BossBlackboard bb, float range) : base(bb) => this.range = range;

    public override NodeState Evaluate()
    {
        if (blackboard.playerTarget == null) return NodeState.FAILURE;

        float dist = Vector2.Distance(blackboard.bossTransform.position, blackboard.playerTarget.position);
        return dist <= range ? NodeState.SUCCESS : NodeState.FAILURE;
    }
}

public class CheckIsAttacking : Node
{
    public CheckIsAttacking(BossBlackboard bb) : base(bb) { }
    public override NodeState Evaluate()
    {
        return blackboard.isAttacking ? NodeState.SUCCESS : NodeState.FAILURE;
    }
}

public class CooldownGateNode : Node
{
    public CooldownGateNode(BossBlackboard bb) : base(bb) { }

    public override NodeState Evaluate()
    {
        return blackboard.cooldownTimer <= 0f ? NodeState.SUCCESS : NodeState.FAILURE;
    }
}

public class FollowPlayerNode : Node
{
    public FollowPlayerNode(BossBlackboard bb) : base(bb) { }

    public override NodeState Evaluate()
    {
        if (blackboard.playerTarget == null) return NodeState.FAILURE;
        if (blackboard.isAttacking) return NodeState.FAILURE;

        Vector2 direction = (blackboard.playerTarget.position - blackboard.bossTransform.position).normalized;
        blackboard.rb.linearVelocity = direction * blackboard.moveSpeed;
        blackboard.anim.SetBool("isMove", true);

        return NodeState.RUNNING;
    }
}
