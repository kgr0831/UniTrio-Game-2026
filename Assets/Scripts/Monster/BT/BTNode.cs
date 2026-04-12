using System.Collections.Generic;

/// <summary>BT 노드 실행 결과</summary>
public enum BTStatus { Success, Failure, Running }

/// <summary>BT 노드 추상 기반</summary>
public abstract class BTNode
{
    public abstract BTStatus Execute();
}

/// <summary>자식을 왼쪽부터 순서대로 실행. 실패 시 즉시 중단.</summary>
public class BTSequence : BTNode
{
    private readonly BTNode[] _children;
    private int _currentIndex = 0;

    public BTSequence(BTNode[] children) => _children = children;

    public override BTStatus Execute()
    {
        while (_currentIndex < _children.Length)
        {
            BTStatus status = _children[_currentIndex].Execute();
            if (status == BTStatus.Running) return BTStatus.Running;
            if (status == BTStatus.Failure)
            {
                _currentIndex = 0;
                return BTStatus.Failure;
            }
            _currentIndex++;
        }
        _currentIndex = 0;
        return BTStatus.Success;
    }
}

/// <summary>자식 중 하나라도 성공하면 즉시 반환.</summary>
public class BTSelector : BTNode
{
    private readonly BTNode[] _children;
    private int _currentIndex = 0;

    public BTSelector(BTNode[] children) => _children = children;

    public override BTStatus Execute()
    {
        while (_currentIndex < _children.Length)
        {
            BTStatus status = _children[_currentIndex].Execute();
            if (status == BTStatus.Running) return BTStatus.Running;
            if (status == BTStatus.Success)
            {
                _currentIndex = 0;
                return BTStatus.Success;
            }
            _currentIndex++;
        }
        _currentIndex = 0;
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
