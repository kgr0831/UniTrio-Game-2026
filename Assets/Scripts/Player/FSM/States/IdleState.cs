/// <summary>
/// Idle 상태: 이동 입력 없음. 대시 입력 감지 → DashState, 이동 입력 감지 → WalkState.
/// </summary>
public class IdleState : PlayerState
{
    public IdleState(PlayerStateMachine machine) : base(machine) { }

    public override void Enter()
    {
        Machine.Movement.enabled   = true;
        Machine.WeaponCtrl.enabled = true;
    }

    public override void Update()
    {
        // 이동 입력 감지 → Walk
        if (Machine.Movement.MoveInput.sqrMagnitude > 0.01f)
        {
            Machine.TransitionTo(Machine.Walk);
            return;
        }

        // 공격 중에는 대시 불가 (양방향 잠금: 대시 중 공격 불가는 DashState에서 처리)
        if (!Machine.WeaponCtrl.IsAttacking && Machine.Dash.TryConsumeDashInput())
        {
            // Idle 시 커서 방향(FacingDirection)으로 대시
            Machine.Dash.StartDash(Machine.Movement.FacingDirection);
            Machine.TransitionTo(Machine.DashSt);
        }
    }
}
