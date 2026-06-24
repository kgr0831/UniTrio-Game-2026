using System.Collections.Generic;
using UnityEngine;

public class RandomSkillSelector : Node
{
    private List<Node> skills;
    private Node selectedNode;

    public RandomSkillSelector(BossBlackboard bb, List<Node> skills) : base(bb) {
        this.skills = skills;
    }

    public override NodeState Evaluate() {
        if (selectedNode == null) {
            selectedNode = skills[Random.Range(0, skills.Count)];
        }

        NodeState state = selectedNode.Evaluate();

        if (state != NodeState.RUNNING) {
            selectedNode = null;
        }
        return state;
    }
}