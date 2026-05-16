using System.Collections.Generic;

/// <summary>BT 노드 실행 결과</summary>
public enum BTStatus { Success, Failure, Running }

/// <summary>BT 노드 추상 기반</summary>
public abstract class BTNode
{
    public abstract BTStatus Execute();
}

/// <summary>자식을 왼쪽부터 순서대로 실행. 하나라도 실패/진행 중이면 중단. (반응형)</summary>
public class BTSequence : BTNode
{
    private readonly BTNode[] _children;

    public BTSequence(BTNode[] children) => _children = children;

    public override BTStatus Execute()
    {
        foreach (var child in _children)
        {
            BTStatus status = child.Execute();
            if (status != BTStatus.Success) return status;
        }
        return BTStatus.Success;
    }
}

/// <summary>자식 중 우선순위가 높은 것(왼쪽)부터 실행. 하나라도 성공/진행 중이면 즉시 반환. (반응형 우선순위 선택기)</summary>
public class BTSelector : BTNode
{
    private readonly BTNode[] _children;

    public BTSelector(BTNode[] children) => _children = children;

    public override BTStatus Execute()
    {
        foreach (var child in _children)
        {
            BTStatus status = child.Execute();
            if (status != BTStatus.Failure) return status;
        }
        return BTStatus.Failure;
    }
}

/// <summary>조건 판별 노드 (람다 기반).</summary>
public class BTCondition : BTNode
{
    private readonly System.Func<bool> _condition;
    public BTCondition(System.Func<bool> condition) => _condition = condition;
    public override BTStatus Execute() => _condition() ? BTStatus.Success : BTStatus.Failure;
}

/// <summary>실행 노드 (람다 기반).</summary>
public class BTAction : BTNode
{
    private readonly System.Func<BTStatus> _action;
    public BTAction(System.Func<BTStatus> action) => _action = action;
    public override BTStatus Execute() => _action();
}
