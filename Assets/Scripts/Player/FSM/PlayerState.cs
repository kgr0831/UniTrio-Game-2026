/// <summary>
/// 플레이어 FSM 모든 상태의 추상 기반 클래스.
/// 각 상태는 PlayerStateMachine을 통해 컴포넌트 참조 및 상태 전환을 수행합니다.
/// </summary>
public abstract class PlayerState
{
    protected PlayerStateMachine Machine { get; }

    protected PlayerState(PlayerStateMachine machine)
    {
        Machine = machine;
    }

    /// <summary>상태 진입 시 1회 호출.</summary>
    public virtual void Enter()       { }

    /// <summary>매 프레임 호출 (Update). 상태 전환 조건 감지에 사용.</summary>
    public virtual void Update()      { }

    /// <summary>물리 프레임 호출 (FixedUpdate). 물리 기반 처리에 사용.</summary>
    public virtual void FixedUpdate() { }

    /// <summary>상태 이탈 시 1회 호출. 정리 작업 수행.</summary>
    public virtual void Exit()        { }
}
