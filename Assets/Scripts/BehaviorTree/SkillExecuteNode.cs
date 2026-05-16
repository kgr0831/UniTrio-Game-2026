using UnityEngine;

public class SkillExecuteNode : Node
{
    private BaseSkillAction skill;
    private bool isStarted = false;

    public SkillExecuteNode(BossBlackboard bb, BaseSkillAction skill) : base(bb) {
        this.skill = skill;
        this.skill.Initialize(bb.bossTransform.gameObject, bb);
    }

    public override NodeState Evaluate() {
        if (!isStarted) {
            skill.OnStart();
            isStarted = true;
        }
        
        NodeState state = skill.OnUpdate();
        
        if (state != NodeState.RUNNING) {
            skill.OnEnd();
            isStarted = false;
        }
        return state;
    }
}
