using System.Collections;
using UnityEngine;

/// <summary>
/// 검(Sword) 무기의 공격 로직, 애니메이션, 히트박스를 전담합니다.
/// 2-콤보 좌우 교번 스윙, 슬래시 VFX, 히트박스 타이밍을 포함합니다.
/// </summary>
public class SwordBehaviour : WeaponBehaviourBase
{
    [Header("Animators")]
    [SerializeField] private Animator _weaponAnimator; // 검 스프라이트 애니메이터
    [SerializeField] private Animator _vfxAnimator;    // 슬래시 VFX 애니메이터

    [Header("Hitbox")]
    [SerializeField] private Collider2D _hitboxCollider;

    public override float ComboWindow        => 0.5f;
    public override int   MaxComboSteps      => 2;
    // 콤보 flip은 이 클래스 내부(this.transform Y-scale)에서 처리하므로 컨트롤러 레벨 flip 불필요
    public override bool  FlipComboDirection => false;

    private bool    _hitboxFired;

    // this.transform(SwordWeapon 루트) 기준 캐싱
    private Vector3 _restLocalScale;

    // 자식 애니메이터 localPosition 캐싱 (애니메이터가 위치를 덮어쓰는 것 방지)
    private Vector3 _weaponRestLocalPos;
    private Vector3 _vfxRestLocalPos;

    private void Awake()
    {
        CurrentComboStep = 1;

        // SwordWeapon 루트의 초기 스케일 저장
        _restLocalScale = transform.localScale;

        if (_weaponAnimator != null)
            _weaponRestLocalPos = _weaponAnimator.transform.localPosition;
        if (_vfxAnimator != null)
            _vfxRestLocalPos = _vfxAnimator.transform.localPosition;

        if (_hitboxCollider != null) _hitboxCollider.enabled = false;
    }

    private void LateUpdate()
    {
        // 애니메이터가 매 프레임 덮어쓰는 localPosition을 원래 값으로 복원
        if (_weaponAnimator != null)
            _weaponAnimator.transform.localPosition = _weaponRestLocalPos;
        if (_vfxAnimator != null)
            _vfxAnimator.transform.localPosition = _vfxRestLocalPos;

        // 비공격 상태에서는 자식 회전도 원복
        if (!IsAttacking)
        {
            if (_weaponAnimator != null)
                _weaponAnimator.transform.localEulerAngles = Vector3.zero;
            if (_vfxAnimator != null)
                _vfxAnimator.transform.localEulerAngles = Vector3.zero;
        }
    }

    /// <summary>Idle 전환 직후 애니메이터가 갱신되기 전에 rotation과 루트 스케일을 리셋합니다.</summary>
    private void ResetChildTransforms()
    {
        // SwordWeapon 루트 스케일 복원 (콤보 flip 해제)
        transform.localScale = _restLocalScale;

        if (_weaponAnimator != null)
        {
            _weaponAnimator.transform.localEulerAngles = Vector3.zero;
            _weaponAnimator.Update(0f);
        }
        if (_vfxAnimator != null)
        {
            _vfxAnimator.transform.localEulerAngles = Vector3.zero;
            _vfxAnimator.Update(0f);
        }
    }

    public override void BeginAttack(int comboStep)
    {
        IsAttacking      = true;
        _hitboxFired     = false;
        CurrentComboStep = comboStep;

        // 2타는 SwordWeapon 루트(this.transform)의 Y-scale을 반전해 스윙 방향을 뒤집습니다.
        // - this.transform의 worldPosition은 변하지 않으므로 피봇 위치 이동 없음.
        // - 자식(애니메이터, VFX, 히트박스)이 모두 일관되게 함께 뒤집힘.
        // - 피봇의 Y-scale(커서 방향 미러)과는 독립적으로 곱해져 좌우 어느 방향이든 동작.
        float flipY = (comboStep == 2) ? -1f : 1f;
        transform.localScale = new Vector3(
            _restLocalScale.x, _restLocalScale.y * flipY, _restLocalScale.z);

        _weaponAnimator.SetTrigger("Attack");
        if (_vfxAnimator != null) _vfxAnimator.SetTrigger("Attack");
    }

    public override bool PollFinished(float attackStartTime)
    {
        // 첫 0.05초는 Play() 호출 직후 상태가 갱신되지 않아 오탐이 생길 수 있으므로 대기
        if (Time.time - attackStartTime < 0.05f) return false;

        AnimatorStateInfo info = _weaponAnimator.GetCurrentAnimatorStateInfo(0);

        // 애니메이션 1/4 지점에서 히트박스 1프레임 활성화
        if (info.IsName("Attack") && info.normalizedTime >= 0.25f && !_hitboxFired)
        {
            _hitboxFired = true;
            if (_hitboxCollider != null)
            {
                _hitboxCollider.enabled = true;
                StartCoroutine(DisableHitboxNextPhysicsUpdate());
            }
        }

        // 95% 이상 재생 시 즉시 Idle로 강제 전환 (전이 딜레이 없애기)
        if (!info.IsName("Attack") || info.normalizedTime >= 0.95f)
        {
            IsAttacking = false;
            if (info.IsName("Attack"))
            {
                _weaponAnimator.Play("Idle", 0, 0f);
                if (_vfxAnimator != null) _vfxAnimator.Play("Idle", 0, 0f);
            }
            // Idle 전환 직후 animation 커브 갱신 전에 rotation·scale을 리셋.
            ResetChildTransforms();
            return true;
        }
        return false;
    }

    public override void OnDeactivated()
    {
        IsAttacking  = false;
        _hitboxFired = false;
        StopAllCoroutines();

        transform.localScale = _restLocalScale;
        if (_hitboxCollider != null) _hitboxCollider.enabled = false;
        if (_weaponAnimator  != null) _weaponAnimator.Play("Idle", 0, 0f);
        if (_vfxAnimator     != null) _vfxAnimator.Play("Idle", 0, 0f);
        ResetChildTransforms();
    }

    // 물리 사이클 2회 후 히트박스 비활성화 (1 physics frame 온전히 보장)
    private IEnumerator DisableHitboxNextPhysicsUpdate()
    {
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        if (_hitboxCollider != null) _hitboxCollider.enabled = false;
    }
}
