/// <summary>
/// Walk 상태: 이동 중. 입력 소멸 → IdleState, 대시 입력 → DashState.
/// </summary>
public class WalkState : PlayerState
{
    public WalkState(PlayerStateMachine machine) : base(machine) { }

    public override void Update()
    {
        // 이동 입력 없으면 Idle
        if (Machine.Movement.MoveInput.sqrMagnitude <= 0.01f)
        {
            Machine.TransitionTo(Machine.Idle);
            return;
        }

        // 공격 중에는 대시 불가 (양방향 잠금: 대시 중 공격 불가는 DashState에서 처리)
        if (!Machine.WeaponCtrl.IsAttacking && Machine.Dash.TryConsumeDashInput())
        {
            Machine.Dash.StartDash(Machine.Movement.MoveInput);
            Machine.TransitionTo(Machine.DashSt);
        }
    }
}
