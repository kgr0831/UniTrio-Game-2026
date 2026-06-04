using System;
using UnityEngine;

/// <summary>
/// 플레이어 이동 입력 처리 및 Rigidbody2D 이동 전담 컴포넌트.
/// StatSystem이 있으면 TotalMoveSpeed를 사용하고, 없으면 moveSpeed 필드를 사용합니다.
/// MoveInput / FacingDirection 프로퍼티를 공개해 FSM 등 외부에서 참조할 수 있게 합니다.
/// </summary>
public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;

    [Header("Footstep Audio")]
    [Tooltip("이 거리(units)만큼 이동할 때마다 발걸음 사운드(Walk 1~5 순환)를 재생합니다.")]
    [SerializeField] private float _footstepStride = 50f;

    /// <summary>외부(활 차징 등)에서 임시로 이동속도를 조절합니다. 정상 = 1.0f</summary>
    [HideInInspector] public float SpeedMultiplier = 1f;

    /// <summary>
    /// 외부에서 플레이어를 강제 이동시킬 속도 (units/sec).
    /// Vector2.zero가 아니면 일반 이동 입력을 완전히 무시하고 이 속도로 이동합니다.
    /// 사용 후 반드시 Vector2.zero로 초기화하세요.
    /// </summary>
    [HideInInspector] public Vector2 DashVelocity = Vector2.zero;

    /// <summary>현재 프레임의 정규화된 이동 입력. 입력 없으면 Vector2.zero.</summary>
    public Vector2 MoveInput       { get; private set; }

    /// <summary>커서를 향하는 방향. 애니메이션과 대시 방향(idle 시) 결정에 사용됩니다.</summary>
    public Vector2 FacingDirection { get; private set; }

    /// <summary>
    /// 공격 시작 시 커서 방향으로 FacingDirection을 고정합니다.
    /// 이동 입력이 들어오기 전까지 해당 방향으로 유지됩니다.
    /// </summary>
    public void SetFacingDirection(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.001f) return;
        FacingDirection = dir.normalized;
        if (_spriteRenderer != null)
        {
            if (FacingDirection.x < -0.01f)      _spriteRenderer.flipX = true;
            else if (FacingDirection.x > 0.01f)  _spriteRenderer.flipX = false;
        }
        if (_anim != null)
        {
            _anim.SetFloat(HashDirX, FacingDirection.x);
            _anim.SetFloat(HashDirY, FacingDirection.y);
        }
    }

    private Rigidbody2D _rb;
    private Animator    _anim;
    private SpriteRenderer _spriteRenderer;
    private Camera      _mainCamera;
    private float       _camToWorldZ;
    private StatSystem  _statSystem; // optional – 없으면 moveSpeed 사용
    private Vector2     _recoilVelocity; // 반동/넉백용 내부 속도
    private PlayerWeaponController _weaponController;

    private float       _distanceSinceStep; // 발걸음 사운드용 누적 이동 거리
    private bool        _wasMoving;          // 직전 프레임 이동 여부 (정지→이동 시 첫 발걸음 즉시 재생)

    private static PhysicsMaterial2D _sharedNoPushMat;

    private static readonly int HashIsMoving = Animator.StringToHash("IsMoving");
    private static readonly int HashDirX     = Animator.StringToHash("DirX");
    private static readonly int HashDirY     = Animator.StringToHash("DirY");

    void Start()
    {
        _rb         = GetComponent<Rigidbody2D>();
        _anim       = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _mainCamera = Camera.main;
        if (_mainCamera != null)
            _camToWorldZ = Mathf.Abs(_mainCamera.transform.position.z - transform.position.z);
        _statSystem = GetComponent<StatSystem>(); // nullable
        _weaponController = GetComponent<PlayerWeaponController>();
        
        FacingDirection = Vector2.down; // 기본 바라보는 방향

        // ── 충돌 밀림 방지 설정 ──
        _rb.mass         = 100f;
        _rb.linearDamping = 0f;
        _rb.gravityScale  = 0f;
        _rb.constraints   = RigidbodyConstraints2D.FreezeRotation;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate; // 카메라 트래킹/이동 덜덜 떨림(Jitter) 현상 방지

        if (_sharedNoPushMat == null)
            _sharedNoPushMat = new PhysicsMaterial2D("NoPush")
                { friction = 0f, bounciness = 0f };

        foreach (var col in GetComponentsInChildren<Collider2D>())
            col.sharedMaterial = _sharedNoPushMat;
    }

    void Update()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");
        Vector2 raw = new Vector2(x, y);
        bool attacking = _weaponController != null && _weaponController.IsAttacking;
        MoveInput = (!attacking && raw.sqrMagnitude > 0.001f) ? raw.normalized : Vector2.zero;

        if (_anim != null)
        {
            _anim.SetBool(HashIsMoving, MoveInput.sqrMagnitude > 0.001f);

            // 이동 입력이 있고 공격 중이 아닐 때만 FacingDirection 갱신
            // (공격 중에는 SetFacingDirection으로 고정된 커서 방향 유지)
            if (MoveInput.sqrMagnitude > 0.001f && !attacking)
            {
                FacingDirection = MoveInput;

                if (_spriteRenderer != null)
                {
                    if (FacingDirection.x < -0.01f)
                        _spriteRenderer.flipX = true;
                    else if (FacingDirection.x > 0.01f)
                        _spriteRenderer.flipX = false;
                }
            }

            _anim.SetFloat(HashDirX, FacingDirection.x);
            _anim.SetFloat(HashDirY, FacingDirection.y);
        }
    }

    /// <summary>
    /// 플레이어에게 일시적인 반동(넉백) 힘을 가합니다.
    /// </summary>
    public void ApplyRecoil(Vector2 force)
    {
        _recoilVelocity += force;
    }

    void FixedUpdate()
    {
        _rb.linearVelocity = Vector2.zero;

        Vector2 movement;
        if (DashVelocity.sqrMagnitude > 0.001f)
        {
            // 외부 강제 이동 (돌진 등): 일반 입력/SpeedMultiplier 무시
            movement = DashVelocity;
        }
        else
        {
            float speed = _statSystem != null ? _statSystem.TotalMoveSpeed : moveSpeed;
            movement = MoveInput * speed * SpeedMultiplier + _recoilVelocity;

            if (_recoilVelocity.sqrMagnitude > 0.01f)
                _recoilVelocity = Vector2.Lerp(_recoilVelocity, Vector2.zero, Time.fixedDeltaTime * 6f);
            else
                _recoilVelocity = Vector2.zero;
        }

        _rb.MovePosition(_rb.position + movement * Time.fixedDeltaTime);

        UpdateFootsteps(movement);
    }

    /// <summary>
    /// 이동 거리 기반 발걸음 사운드. 일반 이동(대시 제외)으로 일정 거리(_footstepStride)를
    /// 누적할 때마다 AudioManager가 Walk 1~5를 순환 재생합니다.
    /// </summary>
    private void UpdateFootsteps(Vector2 movement)
    {
        // 대시 중이거나 이동 입력이 없으면 발걸음 누적 중단
        bool moving = DashVelocity.sqrMagnitude <= 0.001f && MoveInput.sqrMagnitude > 0.001f;

        if (!moving)
        {
            _distanceSinceStep = 0f;
            _wasMoving = false;
            return;
        }

        // 정지 → 이동 전환 시 첫 발걸음을 즉시 재생
        if (!_wasMoving)
            _distanceSinceStep = _footstepStride;

        _wasMoving = true;
        _distanceSinceStep += movement.magnitude * Time.fixedDeltaTime;

        if (_distanceSinceStep >= _footstepStride)
        {
            _distanceSinceStep -= _footstepStride;
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayFootstep();
        }
    }

    private void OnTriggerEnter2D(Collider2D other) // 테스트 코드
    {
        if (other.gameObject.layer == 8) Debug.Log("정상 충돌");
        //throw new NotImplementedException();
    }
}
