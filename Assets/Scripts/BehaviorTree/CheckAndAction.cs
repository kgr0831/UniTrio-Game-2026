using System.Collections;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Rendering.UI;

public class ActionNode_Idle : Node
{
    public ActionNode_Idle(BossBlackboard bb) : base(bb) { }
    public override NodeState Evaluate()
    {
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

public class FollowPlayerNode : Node
{
    public FollowPlayerNode(BossBlackboard bb) : base(bb) { }

    public override NodeState Evaluate()
    {
        if (blackboard.playerTarget == null) return NodeState.FAILURE;

        Vector2 direction = (blackboard.playerTarget.position - blackboard.bossTransform.position).normalized;
        blackboard.rb.linearVelocity = direction * blackboard.moveSpeed;
        blackboard.anim.SetBool("isMove", true);
        
        return NodeState.RUNNING;
    }
}

public class RandomSkillNode : Node
{
    public RandomSkillNode(BossBlackboard bb) : base(bb) { }
    
    private bool isStarted = false;
    private int selectedIndex = -1;
    private string targetStateName;

    public override NodeState Evaluate()
    {
        if (!isStarted)
        {
            selectedIndex = Random.Range(0, 3); 
            targetStateName = "Attack0" + (selectedIndex + 1);
            blackboard.anim.SetTrigger(targetStateName);
            
            isStarted = true;
            return NodeState.RUNNING;
        }
        
        var stateInfo = blackboard.anim.GetCurrentAnimatorStateInfo(0);
        
        if (stateInfo.IsName(targetStateName) && stateInfo.normalizedTime < 1.0f)
        {
            return NodeState.RUNNING;
        }
        
        isStarted = false;
        selectedIndex = -1;
        return NodeState.SUCCESS;
    }
}
