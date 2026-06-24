using UnityEngine;

/// <summary>
/// Hit 상태: 피격 직후 스태거 동안 이동 입력 잠금·공격 잠금 + VFX.
/// - 입력만 잠그고 Movement는 끄지 않음 → FixedUpdate가 계속 돌아 ApplyRecoil 넉백이 적용됨
/// - 카메라 쉐이킹 (강도 높고 짧게)
/// - 애니메이션 프리즈 (Animator.speed = 0)
/// - 타이머 종료 후 Idle / Walk로 복귀
/// </summary>
public class HitState : PlayerState
{
    private const float StaggerDuration      = 0.2f;
    private const float CameraShakeIntensity = 0.35f;
    private const float CameraShakeDuration  = 0.18f;

    private float _timer;

    public HitState(PlayerStateMachine machine) : base(machine) { }

    public override void Enter()
    {
        _timer = StaggerDuration;

        // Movement는 끄지 않고 입력만 잠근다 → FixedUpdate가 계속 돌아 ApplyRecoil 넉백이 적용됨
        Machine.Movement.InputLocked = true;
        Machine.WeaponCtrl.enabled   = false;

        // 애니메이션 피격 자세로 고정
        if (Machine.Animator != null)
            Machine.Animator.speed = 0f;

        // 강도 높고 짧은 카메라 쉐이킹
        CameraShakeController.Instance?.Shake(CameraShakeIntensity, CameraShakeDuration);
    }

    public override void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer > 0f) return;

        RestoreState();
        bool moving = Machine.Movement.MoveInput.sqrMagnitude > 0.01f;
        Machine.TransitionTo(moving ? Machine.Walk : (PlayerState)Machine.Idle);
    }

    public override void Exit()
    {
        RestoreState();
    }

    private void RestoreState()
    {
        Machine.Movement.InputLocked = false;
        Machine.WeaponCtrl.enabled   = true;

        if (Machine.Animator != null)
            Machine.Animator.speed = 1f;
    }
}
