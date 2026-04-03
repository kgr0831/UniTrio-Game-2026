using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 커서 방향에 따른 무기 피봇 회전, 공격 입력, 무기 교체(1/2번 키)를 담당하는 범용 컨트롤러.
/// 무기별 공격 로직/애니메이션은 WeaponBehaviourBase 파생 클래스에 위임합니다.
/// </summary>
public class PlayerWeaponController : MonoBehaviour
{
    [SerializeField] private Transform _weaponPivot;
    [SerializeField] private Animator  _playerAnimator;

    [Header("Weapon Slots  (0 = 검 / 1 = 창)")]
    [Tooltip("슬롯 수 만큼 크기를 맞추고 각 무기의 WeaponBehaviourBase 컴포넌트를 할당하세요.")]
    [SerializeField] private WeaponBehaviourBase[] _weaponBehaviours = new WeaponBehaviourBase[2];

    [Tooltip("슬롯 수 만큼 크기를 맞추고 각 무기의 루트 GameObject를 할당하세요.")]
    [SerializeField] private GameObject[] _weaponObjects = new GameObject[2];

    private static readonly int _hashDirX = Animator.StringToHash("DirX");
    private static readonly int _hashDirY = Animator.StringToHash("DirY");

    private Camera _mainCamera;
    private float  _camToWorldZ;
    private Vector3 _pivotRestLocalPos;

    // ── 콤보 엔진 ─────────────────────────────────────────
    // _comboStep : 다음 번에 실행될 공격의 타수(step).
    // BeginAttack 호출 후 즉시 증가하므로, UpdateCursorDirection이
    // 이 값을 읽을 때는 항상 "다음 공격"을 위한 방향 프리뷰가 됩니다.
    private int   _comboStep = 1;
    private float _lastAttackEndTime;

    // 입력 버퍼링: 짧은 찰나의 광클도 씹히지 않게 0.3초간 기억
    private bool  _attackQueued;
    private float _attackQueueTime;
    private float _attackStartTime;

    // 현재 활성 무기
    private int                _currentSlotIndex = 0;
    private WeaponBehaviourBase _activeBehaviour;

    private void Awake()
    {
        _mainCamera      = Camera.main;
        _camToWorldZ     = Mathf.Abs(_mainCamera.transform.position.z - transform.position.z);
        _pivotRestLocalPos = _weaponPivot.localPosition;

        // 시작 시 1번 슬롯(검) 자동 장착
        EquipWeapon(0);
    }

    private void Update()
    {
        HandleWeaponSwitch();
        UpdateCursorDirection();
        HandleAttackInput();
        CheckAttackFinished();
    }

    // ── 무기 교체 ─────────────────────────────────────────

    private void HandleWeaponSwitch()
    {
        // 숫자키 1~9번을 눌렀을 때 배열 길이에 맞게 무기를 교체합니다.
        for (int i = 0; i < _weaponBehaviours.Length; i++)
        {
            if (i < 9 && Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                EquipWeapon(i);
                break;
            }
        }
    }

    private void EquipWeapon(int index)
    {
        if (index < 0 || index >= _weaponBehaviours.Length) return;
        if (_weaponBehaviours[index] == null) return;

        // 이미 들고 있는 무기면 무시
        if (index == _currentSlotIndex
            && index < _weaponObjects.Length
            && _weaponObjects[index] != null
            && _weaponObjects[index].activeSelf) return;

        // 이전 무기 정리
        _activeBehaviour?.OnDeactivated();

        // 무기 오브젝트 표시 전환
        for (int i = 0; i < _weaponObjects.Length; i++)
        {
            if (_weaponObjects[i] != null)
                _weaponObjects[i].SetActive(i == index);
        }

        _currentSlotIndex = index;
        _activeBehaviour  = _weaponBehaviours[index];

        // 무기 교체 시 콤보 완전 초기화
        _comboStep         = 1;
        _attackQueued      = false;
        _lastAttackEndTime = 0f;
    }

    // ── 커서 방향 / 피봇 회전 ─────────────────────────────

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

        _playerAnimator.SetFloat(_hashDirX, dx);
        _playerAnimator.SetFloat(_hashDirY, dy);

        // 공격 중에는 피봇 각도를 고정 (주로 근접 무기). 설정에 따라 활처럼 조준을 유지할 수도 있습니다.
        if (_activeBehaviour != null && _activeBehaviour.IsAttacking && _activeBehaviour.LockRotationDuringAttack) return;

        float angle     = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
        float rotOffset = _activeBehaviour != null ? _activeBehaviour.PivotRotationOffset : 0f;
        // goBehind / Y-scale 계산은 원본 커서 각도 기준, 피봇 회전에만 오프셋 적용
        _weaponPivot.localEulerAngles = new Vector3(0f, 0f, angle + rotOffset);

        // UseYScaleFlip=false 인 무기(창 등)는 항상 scale (1,1,1) — 반전 없음
        float baseScaleY = 1f;
        if (_activeBehaviour == null || _activeBehaviour.UseYScaleFlip)
        {
            // _comboStep 은 "다음 공격의 타수"를 가리키므로
            // 현재 비어있는 프레임에서 이미 다음 공격의 방향 프리뷰가 맞게 적용됩니다.
            bool flipY = _activeBehaviour != null
                         && _activeBehaviour.FlipComboDirection
                         && _comboStep == 2;
            baseScaleY = (dx < 0f) ? -1f : 1f;
            if (flipY) baseScaleY *= -1f;
        }

        _weaponPivot.localScale = new Vector3(1f, baseScaleY, 1f);

        // 무기가 등 뒤(위/좌 방향)일 때 Z를 +1 해 플레이어 뒤로 렌더링할지 결정
        bool goBehind = false;
        if (_activeBehaviour == null || _activeBehaviour.UseGoBehind)
        {
            if (Mathf.Abs(dx) >= Mathf.Abs(dy))
                goBehind = (dx < 0f);
            else
                goBehind = (dy > 0f);
        }

        // 모든 무기는 에디터에서 배치한 피봇 위치를 그대로 유지하고 Z축 회전만 합니다.
        Vector3 pivotPos = _pivotRestLocalPos;
        pivotPos.z = _pivotRestLocalPos.z + (goBehind ? 1f : -1f);
        pivotPos.y = _pivotRestLocalPos.y + (goBehind ? 0.001f : -0.001f);
        _weaponPivot.localPosition = pivotPos;
    }

    // ── 공격 입력 처리 ────────────────────────────────────

    private void HandleAttackInput()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (Input.GetMouseButtonDown(0))
        {
            _attackQueued    = true;
            _attackQueueTime = Time.time;
        }

        // 버퍼 유효 시간(0.3초) 초과 시 파기
        if (_attackQueued && Time.time - _attackQueueTime > 0.3f)
            _attackQueued = false;

        // 공격 중이면 대기
        if (_activeBehaviour == null || _activeBehaviour.IsAttacking) return;

        // 콤보 유효 시간이 지났으면 1타로 리셋
        if (Time.time - _lastAttackEndTime > _activeBehaviour.ComboWindow)
            _comboStep = 1;

        if (_attackQueued)
        {
            _attackQueued    = false;
            _attackStartTime = Time.time;

            _activeBehaviour.BeginAttack(_comboStep);

            // 다음 클릭을 위해 스텝 순환 (1→2→1→ … MaxComboSteps까지)
            _comboStep = (_comboStep % _activeBehaviour.MaxComboSteps) + 1;
        }
    }

    // ── 공격 종료 감시 ────────────────────────────────────

    private void CheckAttackFinished()
    {
        if (_activeBehaviour == null || !_activeBehaviour.IsAttacking) return;

        if (_activeBehaviour.PollFinished(_attackStartTime))
        {
            _lastAttackEndTime = Time.time;
        }
    }
}
