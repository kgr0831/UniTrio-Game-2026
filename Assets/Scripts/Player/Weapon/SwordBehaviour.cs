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

    public override float ComboWindow    => 0.5f;
    public override int   MaxComboSteps  => 2;
    public override bool  FlipComboDirection => true; // 2타에 Y-scale 반전(올려치기)

    private bool    _hitboxFired;
    private Vector3 _weaponRestLocalPos;
    private Vector3 _vfxRestLocalPos;

    private void Awake()
    {
        CurrentComboStep = 1;

        // 이전 버전에서 꼬인 플립 초기화
        var swordRenderer = _weaponAnimator != null ? _weaponAnimator.GetComponent<SpriteRenderer>() : null;
        if (swordRenderer != null) { swordRenderer.flipX = false; swordRenderer.flipY = false; }

        var vfxRenderer = _vfxAnimator != null ? _vfxAnimator.GetComponent<SpriteRenderer>() : null;
        if (vfxRenderer != null) { vfxRenderer.flipX = false; vfxRenderer.flipY = false; }

        // 애니메이터가 위치값을 덮어쓰지 못하도록 최초 로컬 위치를 캐싱
        if (_weaponAnimator != null) _weaponRestLocalPos = _weaponAnimator.transform.localPosition;
        if (_vfxAnimator    != null) _vfxRestLocalPos    = _vfxAnimator.transform.localPosition;

        if (_hitboxCollider != null) _hitboxCollider.enabled = false;
    }

    private void LateUpdate()
    {
        if (_weaponAnimator != null)
        {
            _weaponAnimator.transform.localPosition = _weaponRestLocalPos;
            // 공격 중이 아닐 때: LateUpdate에서 매 프레임 rotation을 0으로 고정.
            // Animator는 Play("Idle") 이후에도 LateUpdate에서 Attack의 마지막 rotation 값을
            // 계속 적용할 수 있으므로, !IsAttacking 구간에서 강제로 눌러둡니다.
            if (!IsAttacking) _weaponAnimator.transform.localEulerAngles = Vector3.zero;
        }
        if (_vfxAnimator != null)
        {
            _vfxAnimator.transform.localPosition = _vfxRestLocalPos;
            if (!IsAttacking) _vfxAnimator.transform.localEulerAngles = Vector3.zero;
        }
    }

    /// <summary>Idle 전환 직후 애니메이터가 갱신되기 전에 rotation을 리셋합니다.</summary>
    private void ResetChildRotations()
    {
        if (_weaponAnimator != null)
        {
             _weaponAnimator.transform.localEulerAngles = Vector3.zero;
             // 애니메이터가 비활성화된 후에도 마지막 프레임이 남지 않도록 강제 업데이트
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
            // Idle 전환 직후 animation 커브 갱신 전에 rotation을 리셋.
            // CheckAttackFinished가 UpdateCursorDirection보다 먼저 실행되므로
            // 같은 프레임에 피봇 회전도 즉시 갱신되어 이상한 각도가 보이지 않음.
            ResetChildRotations();
            return true;
        }
        return false;
    }

    public override void OnDeactivated()
    {
        IsAttacking  = false;
        _hitboxFired = false;
        StopAllCoroutines();

        if (_hitboxCollider != null) _hitboxCollider.enabled = false;
        if (_weaponAnimator  != null) _weaponAnimator.Play("Idle", 0, 0f);
        if (_vfxAnimator     != null) _vfxAnimator.Play("Idle", 0, 0f);
        ResetChildRotations();
    }

    // 물리 사이클 2회 후 히트박스 비활성화 (1 physics frame 온전히 보장)
    private IEnumerator DisableHitboxNextPhysicsUpdate()
    {
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        if (_hitboxCollider != null) _hitboxCollider.enabled = false;
    }
}
