/// <summary>
/// Cutscene 상태: 연출 중 플레이어 입력 전면 잠금.
/// 외부에서 Machine.TransitionTo(Machine.Idle) 호출로 해제합니다.
/// </summary>
public class CutsceneState : PlayerState
{
    public CutsceneState(PlayerStateMachine machine) : base(machine) { }

    public override void Enter()
    {
        Machine.Movement.enabled   = false;
        Machine.WeaponCtrl.enabled = false;
    }

    public override void Exit()
    {
        Machine.Movement.enabled   = true;
        Machine.WeaponCtrl.enabled = true;
    }
}
