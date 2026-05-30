using UnityEngine;

/// <summary>
/// Hit 상태: 피격 직후 0.2초 동안 이동·공격 잠금 (스태거).
/// 타이머 종료 후 입력 여부에 따라 Idle / Walk로 복귀합니다.
/// </summary>
public class HitState : PlayerState
{
    private const float StaggerDuration = 0.2f;
    private float _timer;

    public HitState(PlayerStateMachine machine) : base(machine) { }

    public override void Enter()
    {
        _timer                       = StaggerDuration;
        // Movement는 끄지 않고 입력만 잠근다 → FixedUpdate가 계속 돌아 ApplyRecoil 넉백이 적용됨
        Machine.Movement.InputLocked = true;
        Machine.WeaponCtrl.enabled   = false;
    }

    public override void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer > 0f) return;

        // 스태거 종료 – 복귀
        Machine.Movement.InputLocked = false;
        Machine.WeaponCtrl.enabled   = true;

        bool moving = Machine.Movement.MoveInput.sqrMagnitude > 0.01f;
        Machine.TransitionTo(moving ? Machine.Walk : (PlayerState)Machine.Idle);
    }

    public override void Exit()
    {
        // Exit에서도 보장 (외부에서 강제 전환 시 잠김 방지)
        Machine.Movement.InputLocked = false;
        Machine.WeaponCtrl.enabled   = true;
    }
}
