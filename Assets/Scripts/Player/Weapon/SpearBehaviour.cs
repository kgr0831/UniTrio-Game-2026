using System.Collections;
using UnityEngine;

/// <summary>
/// 창(Spear) 무기의 공격 로직, 애니메이션, 히트박스를 전담합니다.
/// - 찌르기(Thrust) 단타: 콤보 없이 1타만 존재.
/// - 스프라이트가 45° 기울어진 형태이므로 PivotRotationOffset = 45f.
/// - 검의 VFX와 동일하게 SpearVFX 애니메이터를 함께 재생합니다.
/// </summary>
public class SpearBehaviour : WeaponBehaviourBase
{
    [Header("Animators")]
    [SerializeField] private Animator _weaponAnimator; // 창 스프라이트 애니메이터
    [SerializeField] private Animator _vfxAnimator;    // 찌르기 VFX 애니메이터

    [Header("Hitbox")]
    [SerializeField] private Collider2D _hitboxCollider;

    public override WeaponType WeaponType => WeaponType.Spear;

    [Header("Orbit Settings (공전)")]
    [Tooltip("캐릭터를 중심으로 얼마나 띄울지 결정합니다.")]
    [SerializeField] private float _orbitRadius = 0.5f;
    [Tooltip("무기가 뒤로 숨는 로직을 끌지 결정합니다 (공전할 때는 끄는게 보통 자연스럽습니다).")]
    [SerializeField] private bool _useGoBehind = false;

    // 창 스프라이트가 기본적으로 대각선이나 가로로 세팅되어 있음. 
    // 커서 방향과 완벽히 일치시키기 위해 오프셋을 0으로 둡니다. (로컬 Z값이 이 역할을 함)
    public override float PivotRotationOffset => 0f;

    // 창은 360도 회전으로만 방향 표현 → Y-scale 반전 없음
    public override bool  UseYScaleFlip => false;

    // 새로 추가된 공전 보정 매커니즘
    public override float OrbitRadius => _orbitRadius;
    public override bool UseGoBehind => _useGoBehind;

    public override float ComboWindow   => 0f;  // 콤보 없으므로 의미 없음
    public override int   MaxComboSteps => 1;   // 단타

    private bool             _hitboxFired;
    private SpriteRenderer[] _vfxRenderers; // 자식 포함 전체 렌더러 (GetComponent는 자식 미포함으로 null 위험)

    private void Awake()
    {
        CurrentComboStep = 1;

        if (_vfxAnimator != null)
        {
            // GetComponent 대신 GetComponentsInChildren으로 자식까지 탐색
            _vfxRenderers = _vfxAnimator.GetComponentsInChildren<SpriteRenderer>(true);
            SetVfxVisible(false);
        }

        if (_hitboxCollider != null) _hitboxCollider.enabled = false;
    }

    private void SetVfxVisible(bool visible)
    {
        if (_vfxRenderers == null) return;
        for (int i = 0; i < _vfxRenderers.Length; i++)
            _vfxRenderers[i].enabled = visible;
    }

    public override void BeginAttack(int comboStep)
    {
        IsAttacking      = true;
        _hitboxFired     = false;
        CurrentComboStep = 1; // 단타이므로 항상 1

        SetVfxVisible(true);

        _weaponAnimator.SetTrigger("Attack");
        if (_vfxAnimator != null) _vfxAnimator.SetTrigger("Attack");
    }

    public override bool PollFinished(float attackStartTime)
    {
        if (Time.time - attackStartTime < 0.05f) return false;

        AnimatorStateInfo info = _weaponAnimator.GetCurrentAnimatorStateInfo(0);

        // 찌르기 무기: 0.2f 지점에서 히트박스 활성화 (검 0.25f보다 살짝 빠름)
        if (info.IsName("Attack") && info.normalizedTime >= 0.2f && !_hitboxFired)
        {
            _hitboxFired = true;
            if (_hitboxCollider != null)
            {
                _hitboxCollider.enabled = true;
                // 찌르기 히트박스는 3 physics update 유지 (관통감)
                StartCoroutine(DisableHitboxAfterThrust());
            }
        }

        // 95% 이상 재생 시 즉시 Idle 강제 전환
        if (!info.IsName("Attack") || info.normalizedTime >= 0.95f)
        {
            IsAttacking = false;
            SetVfxVisible(false);

            // 무기가 Attack 상태에 있을 때만 Idle로 강제 전환 (이미 전환된 경우 중복 호출 방지)
            if (info.IsName("Attack"))
                _weaponAnimator.Play("Idle", 0, 0f);

            // VFX는 무기 전환 여부와 무관하게 항상 Idle로 복귀
            if (_vfxAnimator != null)
                _vfxAnimator.Play("Idle", 0, 0f);

            return true;
        }
        return false;
    }

    public override void OnDeactivated()
    {
        IsAttacking  = false;
        _hitboxFired = false;
        StopAllCoroutines();

        SetVfxVisible(false);
        if (_hitboxCollider != null) _hitboxCollider.enabled = false;
        if (_weaponAnimator  != null) 
        {
            _weaponAnimator.Play("Idle", 0, 0f);
            _weaponAnimator.Update(0f);
        }
        if (_vfxAnimator != null) 
        {
            _vfxAnimator.Play("Idle", 0, 0f);
            _vfxAnimator.Update(0f);
        }
    }

    private IEnumerator DisableHitboxAfterThrust()
    {
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        if (_hitboxCollider != null) _hitboxCollider.enabled = false;
    }
}
