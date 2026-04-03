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
        // 애니메이터의 position 커브가 검/VFX를 이동시키지 못하도록 매 프레임 고정
        if (_weaponAnimator != null) _weaponAnimator.transform.localPosition = _weaponRestLocalPos;
        if (_vfxAnimator    != null) _vfxAnimator.transform.localPosition    = _vfxRestLocalPos;
    }

    public override void BeginAttack(int comboStep)
    {
        IsAttacking      = true;
        _hitboxFired     = false;
        CurrentComboStep = comboStep;

        _weaponAnimator.Play("Attack", 0, 0f);
        if (_vfxAnimator != null) _vfxAnimator.Play("Attack", 0, 0f);
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
    }

    // 물리 사이클 2회 후 히트박스 비활성화 (1 physics frame 온전히 보장)
    private IEnumerator DisableHitboxNextPhysicsUpdate()
    {
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        if (_hitboxCollider != null) _hitboxCollider.enabled = false;
    }
}
