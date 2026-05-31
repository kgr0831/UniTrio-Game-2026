using UnityEngine;

/// <summary>
/// Dash 상태: 대시 실행 중.
///
/// [공통 — 업그레이드 전/후]
/// - Animator.speed = 0  → Walk/Idle 애니메이션 프리즈
/// - WeaponCtrl / Movement 비활성화
/// - 파란 색조 (DashTint)
///
/// [업그레이드 전 (IsUpgraded = false)]
/// - 단순 이동. 무적·충돌 무시 없음.
///
/// [업그레이드 후 (IsUpgraded = true)]
/// - IsInvincible = true
/// - Enemy 레이어 충돌 제외 → 통과 가능
/// - DashAfterimagePool 잔상 VFX 활성화
/// </summary>
public class DashState : PlayerState
{
    private static readonly Color DashTint       = new Color(0.4f, 0.75f, 1f, 0.8f);
    private static readonly int   EnemyLayerMask = 1 << LayerMask.NameToLayer("Enemy");

    private Rigidbody2D _rb;
    // Enter 시점 상태 캐시 → 런타임 토글 시 Enter/Exit 쌍 보장
    private bool        _wasUpgraded;

    public DashState(PlayerStateMachine machine) : base(machine)
    {
        _rb = machine.GetComponent<Rigidbody2D>();
    }

    public override void Enter()
    {
        _wasUpgraded = Machine.Dash.IsUpgraded;

        Machine.WeaponCtrl.enabled  = false;
        Machine.Movement.enabled    = false;

        // 대시 중 항상 무적 (업그레이드 여부 무관)
        Machine.Entity.IsInvincible = true;

        if (Machine.Animator != null)
            Machine.Animator.SetBool("IsDashing", true);

        if (Machine.Sprite != null)
            Machine.Sprite.color = DashTint;

        if (_wasUpgraded)
        {
            if (_rb != null)
                _rb.excludeLayers = _rb.excludeLayers | EnemyLayerMask;

            Machine.AfterimagePool?.StartSpawning();
        }
    }

    public override void Update()
    {
        if (Machine.Dash.IsDashing) return;

        bool moving = Machine.Movement.MoveInput.sqrMagnitude > 0.01f;
        Machine.TransitionTo(moving ? Machine.Walk : (PlayerState)Machine.Idle);
    }

    public override void Exit()
    {
        Machine.WeaponCtrl.enabled  = true;
        Machine.Movement.enabled    = true;

        // 무적 해제
        Machine.Entity.IsInvincible = false;

        if (Machine.Animator != null)
            Machine.Animator.SetBool("IsDashing", false);

        if (Machine.Sprite != null)
            Machine.Sprite.color = Color.white;

        if (_wasUpgraded)
        {
            if (_rb != null)
                _rb.excludeLayers = _rb.excludeLayers & ~EnemyLayerMask;

            Machine.AfterimagePool?.StopSpawning();
        }
    }
}
