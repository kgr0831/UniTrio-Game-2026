using System.Collections.Generic;
using UnityEngine;

public enum NodeState { RUNNING, SUCCESS, FAILURE }

// 모든 노드의 조상
public abstract class Node
{
    protected BossBlackboard blackboard;
    public Node(BossBlackboard blackboard) => this.blackboard = blackboard;
    public abstract NodeState Evaluate();
}

// Selector: 자식 중 하나라도 성공/진행 중이면 즉시 반환 (우선순위 결정)
public class Selector : Node
{
    private List<Node> children;
    public Selector(BossBlackboard blackboard, List<Node> children) : base(blackboard) => this.children = children;

    public override NodeState Evaluate()
    {
        foreach (var node in children)
        {
            switch (node.Evaluate())
            {
                case NodeState.FAILURE: continue;
                case NodeState.SUCCESS: return NodeState.SUCCESS;
                case NodeState.RUNNING: return NodeState.RUNNING;
            }
        }
        return NodeState.FAILURE;
    }
}

// Sequence: 모든 자식이 성공해야 성공 (일련의 행동 수행)
public class Sequence : Node
{
    private List<Node> children;
    public Sequence(BossBlackboard blackboard, List<Node> children) : base(blackboard) => this.children = children;

    public override NodeState Evaluate()
    {
        bool anyChildRunning = false;
        foreach (var node in children)
        {
            switch (node.Evaluate())
            {
                case NodeState.FAILURE: return NodeState.FAILURE;
                case NodeState.SUCCESS: continue;
                case NodeState.RUNNING: anyChildRunning = true; continue;
            }
        }
        return anyChildRunning ? NodeState.RUNNING : NodeState.SUCCESS;
    }
}