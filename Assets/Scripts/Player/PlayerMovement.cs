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

    /// <summary>외부(활 차징 등)에서 임시로 이동속도를 조절합니다. 정상 = 1.0f</summary>
    [HideInInspector] public float SpeedMultiplier = 1f;

    /// <summary>
    /// true이면 이동 입력을 무시합니다(MoveInput = zero). 단, FixedUpdate는 계속 돌아
    /// ApplyRecoil로 가해진 넉백은 정상 적용됩니다. 피격 스태거(HitState) 중 사용합니다.
    /// </summary>
    public bool InputLocked { get; set; }

    /// <summary>현재 프레임의 정규화된 이동 입력. 입력 없으면 Vector2.zero.</summary>
    public Vector2 MoveInput       { get; private set; }

    /// <summary>커서를 향하는 방향. 애니메이션과 대시 방향(idle 시) 결정에 사용됩니다.</summary>
    public Vector2 FacingDirection { get; private set; }

    private Rigidbody2D _rb;
    private Animator    _anim;
    private Camera      _mainCamera;
    private float       _camToWorldZ;
    private StatSystem  _statSystem; // optional – 없으면 moveSpeed 사용
    private Vector2     _recoilVelocity; // 반동/넉백용 내부 속도
    private PlayerWeaponController _weaponController;

    private static PhysicsMaterial2D _sharedNoPushMat;

    private static readonly int HashIsMoving = Animator.StringToHash("IsMoving");
    private static readonly int HashDirX     = Animator.StringToHash("DirX");
    private static readonly int HashDirY     = Animator.StringToHash("DirY");

    void Start()
    {
        _rb         = GetComponent<Rigidbody2D>();
        _anim       = GetComponent<Animator>();
        _mainCamera = Camera.main;
        if (_mainCamera != null)
            _camToWorldZ = Mathf.Abs(_mainCamera.transform.position.z - transform.position.z);
        _statSystem = GetComponent<StatSystem>(); // nullable
        _weaponController = GetComponent<PlayerWeaponController>();

        // ── 충돌 밀림 방지 설정 ──
        _rb.mass         = 100f;
        _rb.linearDamping = 0f;
        _rb.gravityScale  = 0f;
        _rb.constraints   = RigidbodyConstraints2D.FreezeRotation;

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
        MoveInput = (!attacking && !InputLocked && raw.sqrMagnitude > 0.001f) ? raw.normalized : Vector2.zero;

        if (_anim != null)
        {
            _anim.SetBool(HashIsMoving, MoveInput.sqrMagnitude > 0.001f);

            if (_mainCamera == null) return;

            // 커서 방향 계산 (애니메이션 + FacingDirection 동기화)
            Vector3 mouseScreenPos = Input.mousePosition;
            mouseScreenPos.z       = _camToWorldZ;
            Vector3 mouseWorld     = _mainCamera.ScreenToWorldPoint(mouseScreenPos);

            FacingDirection = ((Vector2)mouseWorld - (Vector2)transform.position).normalized;
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
        // 충돌에 의한 잔여 물리 속도 제거 (밀림 방지)
        _rb.linearVelocity = Vector2.zero;

        // StatSystem이 있으면 TotalMoveSpeed 사용, 없으면 moveSpeed 폴백
        float speed = _statSystem != null ? _statSystem.TotalMoveSpeed : moveSpeed;
        
        // 이동 입력 + 반동 속도
        _rb.MovePosition(_rb.position + (MoveInput * speed * SpeedMultiplier + _recoilVelocity) * Time.fixedDeltaTime);
        
        // 반동 속도는 프레임마다 0으로 수렴 (점진적 감쇠 - 수치를 낮춰 더 멀리 밀려나가게 함)
        if (_recoilVelocity.sqrMagnitude > 0.01f)
            _recoilVelocity = Vector2.Lerp(_recoilVelocity, Vector2.zero, Time.fixedDeltaTime * 6f);
        else
            _recoilVelocity = Vector2.zero;
    }

    private void OnTriggerEnter2D(Collider2D other) // 테스트 코드
    {
        if (other.gameObject.layer == 8) Debug.Log("정상 충돌");
        //throw new NotImplementedException();
    }
}
