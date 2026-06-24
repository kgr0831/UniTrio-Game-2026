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
            blackboard.currentSkill = skill; // 중단 시 정리할 수 있도록 기록
            isStarted = true;
        }

        NodeState state = skill.OnUpdate();

        if (state != NodeState.RUNNING) {
            skill.OnEnd();
            if (blackboard.currentSkill == skill) blackboard.currentSkill = null;
            isStarted = false;
        }
        return state;
    }
}
