using System.Collections.Generic;
using System.Collections;
using UnityEngine;

public enum NodeState { RUNNING, SUCCESS, FAILURE }

public abstract class Node
{
    protected BossBlackboard blackboard;
    public Node(BossBlackboard blackboard) => this.blackboard = blackboard;
    public abstract NodeState Evaluate();
}

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