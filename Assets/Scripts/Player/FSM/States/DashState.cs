using UnityEngine;

/// <summary>
/// Dash 상태: 대시 실행 중.
/// - 무적(IsInvincible = true)
/// - 무기 잠금(WeaponCtrl.enabled = false)
/// - 이동 컴포넌트 비활성화 (DashHandler가 Rigidbody2D 속도 직접 제어)
/// - Enemy 레이어 물리 충돌 제외 → 대시로 좀비를 통과할 수 있음
/// - 스프라이트 파란 색조로 무적 시각화
/// DashHandler.IsDashing이 false가 되면 Idle / Walk로 복귀합니다.
/// </summary>
public class DashState : PlayerState
{
    // 무적 중 반투명 파란 색조
    private static readonly Color DashTint = new Color(0.4f, 0.75f, 1f, 0.8f);

    private Rigidbody2D _rb;
    private static readonly int EnemyLayerMask = 1 << 8; // Enemy 레이어(8번, TagManager 기준)

    public DashState(PlayerStateMachine machine) : base(machine)
    {
        _rb = machine.GetComponent<Rigidbody2D>();
    }

    public override void Enter()
    {
        Machine.Entity.IsInvincible = true;
        Machine.WeaponCtrl.enabled  = false;
        Machine.Movement.enabled    = false; // DashHandler가 속도 직접 제어

        // 대시 중 Enemy 레이어와 물리 충돌 제외 → 통과 가능
        if (_rb != null)
            _rb.excludeLayers = _rb.excludeLayers | EnemyLayerMask;

        if (Machine.Sprite != null)
            Machine.Sprite.color = DashTint;
    }

    public override void Update()
    {
        // 대시 종료 감지
        if (Machine.Dash.IsDashing) return;

        bool moving = Machine.Movement.MoveInput.sqrMagnitude > 0.01f;
        Machine.TransitionTo(moving ? Machine.Walk : (PlayerState)Machine.Idle);
    }

    public override void Exit()
    {
        Machine.Entity.IsInvincible = false;
        Machine.WeaponCtrl.enabled  = true;
        Machine.Movement.enabled    = true;

        // Enemy 레이어 충돌 복원
        if (_rb != null)
            _rb.excludeLayers = _rb.excludeLayers & ~EnemyLayerMask;

        if (Machine.Sprite != null)
            Machine.Sprite.color = Color.white;
    }
}
