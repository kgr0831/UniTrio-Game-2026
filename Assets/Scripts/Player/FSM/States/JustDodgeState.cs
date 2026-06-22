using UnityEngine;

/// <summary>
/// JustDodge 상태: 회피 저스트 시퀀스 동안 FSM을 '주차(park)'시켜 일반 이동/공격을 잠근다.
/// (인벤토리 토글 등은 별도 컴포넌트라 영향받지 않는다.)
///
/// 실제 연출·돌진·카운터는 <see cref="JustDodgeController"/>(코루틴)가 구동하며,
/// 시퀀스가 끝나면 컨트롤러가 직접 Idle/Walk로 전환한다.
/// </summary>
public class JustDodgeState : PlayerState
{
    public JustDodgeState(PlayerStateMachine machine) : base(machine) { }

    public override void Enter()
    {
        // 일반 이동/공격 입력만 잠근다 (대시 이동은 그대로 이어지게 둔다)
        Machine.Movement.enabled   = false;
        Machine.WeaponCtrl.enabled = false;

        // 시퀀스 내내 완전 무적
        Machine.Entity.IsInvincible = true;

        // ★ 회피 저스트 성공 시에도 기존 대시 이동을 그대로 유지한다.
        //   대시 속도/타이머는 DashHandler가 계속 관리하므로 여기서 속도를 죽이지 않는다
        //   → 일반 회피와 동일한 거리만큼 이동한 뒤 자연 종료. 대시 자세도 유지한다.
        if (Machine.Animator != null)
            Machine.Animator.SetBool("IsDashing", true);
    }

    public override void Exit()
    {
        Machine.Movement.enabled   = true;
        Machine.WeaponCtrl.enabled = true;
        Machine.Entity.IsInvincible = false;

        if (Machine.Animator != null)
        {
            Machine.Animator.SetBool("IsDashing", false);
            Machine.Animator.speed = 1f;
        }
        if (Machine.Sprite != null)
            Machine.Sprite.color = Color.white;
    }
}
