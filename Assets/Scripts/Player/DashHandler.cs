using UnityEngine;

/// <summary>
/// 대시 입력 감지, 쿨다운 관리, Rigidbody2D 속도 부스트를 전담하는 컴포넌트.
///
/// 흐름:
/// 1. Update: Space 키 입력 → _dashInputBuffered = true
/// 2. IdleState/WalkState: TryConsumeDashInput() → true 반환 시 StartDash(dir) 호출
/// 3. DashState: IsDashing이 false가 될 때까지 무적·이동 잠금 유지
/// 4. FixedUpdate: 대시 타이머 감소 → 0이 되면 linearVelocity = 0 초기화
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class DashHandler : MonoBehaviour
{
    [Header("Dash Settings")]
    [Tooltip("대시 키 (기본: Space)")]
    [SerializeField] private KeyCode _dashKey      = KeyCode.Space;
    [Tooltip("대시 이동 속도")]
    [SerializeField] private float   _dashForce    = 28f;
    [Tooltip("대시 지속 시간 (초)")]
    [SerializeField] private float   _dashDuration = 0.18f;
    [Tooltip("대시 쿨다운 (초)")]
    [SerializeField] private float   _cooldown     = 1f;

    [Header("Upgrade (스킬트리 연동 전 임시 제어)")]
    [Tooltip("true = 업그레이드 대시 (무적·충돌무시·잔상 VFX)\nfalse = 기본 대시 (이동만)")]
    [SerializeField] private bool _isUpgraded = false;

    /// <summary>업그레이드 대시 여부. 스킬트리에서 외부 설정 가능.</summary>
    public bool IsUpgraded => _isUpgraded;

    /// <summary>현재 대시 중이면 true.</summary>
    public bool IsDashing => _dashTimer > 0f;

    /// <summary>쿨다운이 끝나 대시 가능한 상태이면 true.</summary>
    public bool CanDash   => _cooldownTimer <= 0f;

    private Rigidbody2D _rb;
    private float       _dashTimer;
    private float       _cooldownTimer;
    private bool        _dashInputBuffered;
    private ChargeSystem _chargeSystem;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _chargeSystem = GetComponent<ChargeSystem>();
    }

    private void Update()
    {
        // 쿨다운/대시 중에는 버퍼링하지 않음 → 입력 예약으로 인한 자동 대시 방지
        if (Input.GetKeyDown(_dashKey) && _cooldownTimer <= 0f && !IsDashing)
            _dashInputBuffered = true;

        if (_cooldownTimer > 0f)
            _cooldownTimer -= Time.deltaTime;
    }

    private void FixedUpdate()
    {
        if (_dashTimer <= 0f) return;

        _dashTimer -= Time.fixedDeltaTime;
        if (_dashTimer <= 0f)
        {
            _dashTimer = 0f;
            // 대시 종료 시 속도 초기화 → PlayerMovement가 즉시 깔끔하게 제어권 회수
            _rb.linearVelocity = UnityEngine.Vector2.zero;
        }
    }

    // ── Public API ──────────────────────────────────────────────────

    /// <summary>
    /// 버퍼링된 대시 입력이 있고 쿨다운이 끝났으면 true를 반환하고 버퍼를 소모합니다.
    /// Idle/WalkState의 Update에서 매 프레임 호출합니다.
    /// </summary>
    public bool TryConsumeDashInput()
    {
        if (_chargeSystem != null && _chargeSystem.IsCharging) return false;
        if (!_dashInputBuffered || _cooldownTimer > 0f) return false;
        _dashInputBuffered = false;
        return true;
    }

    /// <summary>
    /// 대시 실행. DashState.Enter에서 호출합니다.
    /// 지정 방향으로 Rigidbody2D 속도를 설정하고 타이머를 시작합니다.
    /// </summary>
    public void StartDash(UnityEngine.Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.001f)
            direction = UnityEngine.Vector2.right;

        _dashTimer     = _dashDuration;
        _cooldownTimer = _cooldown;

        _rb.linearVelocity = direction.normalized * _dashForce;
    }
}
