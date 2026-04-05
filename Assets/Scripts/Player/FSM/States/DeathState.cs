using UnityEngine;

/// <summary>
/// Death 상태: 사망 처리.
/// - 이동·무기 비활성화
/// - Death 애니메이션 트리거 (Animator에 "Death" 파라미터가 있는 경우)
/// Milestone 3+에서 부활/게임오버 흐름 연동 예정.
/// </summary>
public class DeathState : PlayerState
{
    private static readonly int HashDeath = Animator.StringToHash("Death");

    public DeathState(PlayerStateMachine machine) : base(machine) { }

    public override void Enter()
    {
        Machine.Movement.enabled   = false;
        Machine.WeaponCtrl.enabled = false;

        // Death 파라미터가 Animator에 등록된 경우에만 트리거
        if (Machine.Animator != null && HasDeathParameter())
            Machine.Animator.SetTrigger(HashDeath);

        Debug.Log("[Player] Death 상태 진입");
    }

    private bool HasDeathParameter()
    {
        AnimatorControllerParameter[] ps = Machine.Animator.parameters;
        for (int i = 0; i < ps.Length; i++)
        {
            if (ps[i].nameHash == HashDeath) return true;
        }
        return false;
    }
}
