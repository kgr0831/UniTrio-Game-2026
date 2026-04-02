using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 커서 방향에 따른 무기 피봇 회전, 공격 입력, 스프라이트 방향 전환 담당.
/// </summary>
public class PlayerWeaponController : MonoBehaviour
{
    [SerializeField] private Transform _weaponPivot;
    [SerializeField] private Animator  _swordAnimator;
    [SerializeField] private Animator  _vfxAnimator;
    [SerializeField] private Animator  _playerAnimator;

    private static readonly int _hashAttack = Animator.StringToHash("Attack");
    private static readonly int _hashDirX   = Animator.StringToHash("DirX");
    private static readonly int _hashDirY   = Animator.StringToHash("DirY");

    private Camera         _mainCamera;
    private Transform      _swordTransform;
    private Transform      _vfxTransform;

    private Vector3 _swordRestLocalPos;
    private Vector3 _vfxRestLocalPos;
    private Vector3 _pivotRestLocalPos;

    [Header("Hitbox")]
    [SerializeField] private Collider2D _hitboxCollider; // 인스펙터에서 할당
    private bool  _hitboxFired;

    [Header("Combo Engine")]
    [SerializeField] private float _comboWindow = 0.5f; // 1타 후 어느 시간 내에 클릭하면 2타로 연계될 지
    private int   _comboStep = 1; // 현재 진행할 콤보 타수 (1 or 2)
    private float _lastAttackEndTime;

    // 입력계 UX: 짧은 찰나에 들어온 광클을 기억해두었다가 공백 없이 시원하게 연결합니다.
    private bool  _attackQueued;
    private float _attackQueueTime;

    private bool  _isAttacking;
    private float _attackStartTime;
    private float _camToWorldZ;

    private SpriteRenderer _vfxRenderer;

    private void Awake()
    {
        _mainCamera  = Camera.main;
        _camToWorldZ = Mathf.Abs(_mainCamera.transform.position.z - transform.position.z);

        _swordTransform    = _swordAnimator.transform;
        _vfxTransform      = _vfxAnimator.transform;

        // 이전 버전에서 꼬인 플립 상태 초기화
        var swordRenderer = _swordAnimator.GetComponent<SpriteRenderer>();
        if (swordRenderer != null) { swordRenderer.flipX = false; swordRenderer.flipY = false; }
        
        _vfxRenderer = _vfxAnimator.GetComponent<SpriteRenderer>();
        if (_vfxRenderer != null) { _vfxRenderer.flipX = false; _vfxRenderer.flipY = false; }

        // Position 고정을 위한 각 객체의 최초 위치 캐싱
        _swordRestLocalPos = _swordTransform.localPosition;
        _vfxRestLocalPos   = _vfxTransform.localPosition;
        _pivotRestLocalPos = _weaponPivot.localPosition;

        if (_hitboxCollider != null)
        {
            _hitboxCollider.enabled = false; // 기본적으로 꺼둠
        }
    }

    private void Update()
    {
        UpdateCursorDirection();
        HandleAttackInput();
        CheckAttackFinished();
    }

    private void LateUpdate()
    {
        // 1. Position 고정: "position 이동 없이 rotation 이동만 유지"
        // 애니메이터가 설정하는 곡선 값(움직임)을 제거하고 항상 무기를 어깨/손 위치로 고정합니다.
        // 좌/우 반전은 Update()에서 pivot의 localScale 반전으로 자동계산되므로 좌표값만 대입하면 됩니다.
        _swordTransform.localPosition = _swordRestLocalPos;
        _vfxTransform.localPosition   = _vfxRestLocalPos;
    }

    private void UpdateCursorDirection()
    {
        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z       = _camToWorldZ;
        Vector3 mouseWorld     = _mainCamera.ScreenToWorldPoint(mouseScreenPos);

        float dx = mouseWorld.x - transform.position.x;
        float dy = mouseWorld.y - transform.position.y;

        float sqrMag = dx * dx + dy * dy;
        if (sqrMag > 0.0001f)
        {
            float inv = 1f / Mathf.Sqrt(sqrMag);
            dx *= inv;
            dy *= inv;
        }

        // 플레이어 몸통(스프라이트) 방향 반영
        _playerAnimator.SetFloat(_hashDirX, dx);
        _playerAnimator.SetFloat(_hashDirY, dy);

        // 요청 반영: 공격 중일 때는 무방비하게 움직이지 않게 각도를 완전히 고정합니다.
        if (!_isAttacking)
        {
            float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            _weaponPivot.localEulerAngles = new Vector3(0f, 0f, angle);

            // 🌟 거울 모드 대칭 반전 조작 🌟
            float baseScaleY = (dx < 0f) ? -1f : 1f;

            // 2타일 경우 상하 공격 궤적을 완전히 뒤집습니다 (올려치기 등)
            if (_comboStep == 2)
            {
                baseScaleY *= -1f;
            }

            _weaponPivot.localScale = new Vector3(1f, baseScaleY, 1f);

            // 스프라이트를 건드리지 않고 Z 좌표만 살짝 조절하여 등 뒤(뒤쪽)로 렌더링 처리
            bool goBehind = false;
            if (Mathf.Abs(dx) >= Mathf.Abs(dy))
                goBehind = (dx < 0f); // 좌우 지배적일 땐 x가 음수면 왼쪽 (뒤)
            else
                goBehind = (dy > 0f); // 상하 지배적일 땐 y가 양수면 위쪽 (뒤)

            Vector3 pivotPos = _pivotRestLocalPos;
            pivotPos.z = _pivotRestLocalPos.z + (goBehind ? 1f : -1f);
            pivotPos.y = _pivotRestLocalPos.y + (goBehind ? 0.001f : -0.001f);

            _weaponPivot.localPosition = pivotPos;
        }
    }

    private void HandleAttackInput()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        // 마우스를 클릭하면 "공격하겠다"는 입력을 0.3초간 기억합니다 (입력 씹힘/버벅임 완벽 방지)
        if (Input.GetMouseButtonDown(0))
        {
            _attackQueued = true;
            _attackQueueTime = Time.time;
        }

        // 시간이 초과된 낡은 입력은 파기
        if (_attackQueued && Time.time - _attackQueueTime > 0.3f)
        {
            _attackQueued = false;
        }

        // 공격 중일 때는 때리라는 명령을 대기(버퍼링)만 시키고 실행하지 않음!
        if (_isAttacking)
            return;

        // 콤보 유효 시간이 지났다면 1타로 콤보 완전 초기화
        if (Time.time - _lastAttackEndTime > _comboWindow)
        {
            _comboStep = 1;
        }

        // 공격 중이 아니면서 공격 입력이 들어와있다면 즉시 실행
        if (_attackQueued)
        {
            ExecuteAttack();
        }
    }

    private void ExecuteAttack()
    {
        _attackQueued = false;
        _attackStartTime = Time.time;
        
        // 요청 반영: 다음 공격 사이클 진입 시 즉시 마우스 각도를 수집하여 낡은 각도 사용 방지
        // UpdateCursorDirection이 호출될 때 현재 _comboStep 값을 읽어가서 올려치기/내려치기 폼을 변경합니다.
        _isAttacking = false;
        UpdateCursorDirection();

        _isAttacking = true;
        _hitboxFired = false; // 새 공격 시작 시 히트박스 트리거 초기화

        // 다음 클릭 시 다른 모션이 나가도록 콤보 스텝 무한 순환 (1타 -> 2타 -> 1타 -> ...)
        _comboStep = (_comboStep == 1) ? 2 : 1;

        // 강제로 0프레임부터 즉시 재생(Play)
        _swordAnimator.Play("Attack", 0, 0f);
        _vfxAnimator.Play("Attack", 0, 0f);
    }

    private void CheckAttackFinished()
    {
        if (!_isAttacking) return;
        
        if (Time.time - _attackStartTime < 0.05f) return;

        AnimatorStateInfo info = _swordAnimator.GetCurrentAnimatorStateInfo(0);

        // 1. 히트박스 활성화 로직 (애니메이션 1/4 지점 = 0.25f)
        if (info.IsName("Attack") && info.normalizedTime >= 0.25f && !_hitboxFired)
        {
            _hitboxFired = true;
            if (_hitboxCollider != null)
            {
                _hitboxCollider.enabled = true;
                // 다음 물리연산 주기에 끄기 위한 코루틴 시작
                StartCoroutine(DisableHitboxNextPhysicsUpdate());
            }
        }
        
        // 2. 스윙 애니메이션 종료 로직
        // 공격 끝자락이 되면 즉시 통제권을 돌려주고 이상한 애니메이션 멈춤(프리징) 딜레이를 아예 도려냅니다.
        if (!info.IsName("Attack") || info.normalizedTime >= 0.95f)
        {
            _isAttacking = false;
            _lastAttackEndTime = Time.time; // 콤보 타이머용 현재 시각 저장

            // 애니메이터가 Attack에서 Idle로 넘어가며 쭈뼛거리고 굳어버리는 0.2초의 전이(Transition) 기간을 
            // 허용하지 않고 즉시 Idle 상태로 초기화하여 캐릭터의 역동성을 살립니다.
            if (info.IsName("Attack"))
            {
                _swordAnimator.Play("Idle", 0, 0f);
                _vfxAnimator.Play("Idle", 0, 0f);
            }
        }
    }

    private System.Collections.IEnumerator DisableHitboxNextPhysicsUpdate()
    {
        // 1프레임의 물리 사이클 온전히 보장 (Wait 2 physics updates just to ensure overlap)
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        
        if (_hitboxCollider != null)
        {
            _hitboxCollider.enabled = false;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Monster 태그 판정 (트리거)
        if (other.CompareTag("Monster"))
        {
            // Monster 측의 맞는 판정 로직을 향후 이 부분에 호출
            Debug.Log("[PlayerWeapon] 몬스터 피격 판정 발생!");
        }
    }
}
