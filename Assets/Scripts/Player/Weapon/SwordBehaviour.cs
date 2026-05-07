using System.Collections;
using UnityEngine;

/// <summary>
/// 검(Sword) 무기의 공격 로직, 애니메이션, 히트박스를 전담합니다.
/// 2-콤보 좌우 교번 스윙, 슬래시 VFX, 히트박스 타이밍을 포함합니다.
/// 강타(Bash) 스킬 활성화 시 검 스프라이트에 붉은 HDR 블룸 색조를 입히고 VFX 스프라이트를 교체합니다.
/// </summary>
public class SwordBehaviour : WeaponBehaviourBase
{
    [Header("Animators")]
    [SerializeField] private Animator _weaponAnimator; // 검 스프라이트 애니메이터
    [SerializeField] private Animator _vfxAnimator;    // 슬래시 VFX 애니메이터

    [Header("Hitbox")]
    [SerializeField] private Collider2D _hitboxCollider;

    [Header("Bash Effect")]
    [Tooltip("검 스프라이트의 SpriteRenderer (강타 발동 시 붉은 블룸 색조 적용)")]
    [SerializeField] private SpriteRenderer _swordRenderer;
    [Tooltip("강타 발동 시 붉은 HDR 블룸 색조. 값이 1을 초과하면 URP 블룸이 발생합니다.")]
    [ColorUsage(true, true)]
    [SerializeField] private Color _bashSwordColor = new Color(1.5f, 0.1f, 0.1f, 1f);
    [Tooltip("VFX 자식 오브젝트의 SpriteRenderer (강타 활성화 시 검과 동일한 블룸 색조 적용)")]
    [SerializeField] private SpriteRenderer _vfxRenderer;
    [Tooltip("강타 활성 중 VFX Animator에서 재생할 스테이트 이름 (Animator Controller에 해당 스테이트가 있어야 합니다)")]
    [SerializeField] private string _bashVfxStateName = "BashIdle";
    [Tooltip("강타 활성 중 VFX에 사용할 스프라이트 (빈 칸으로 두면 스프라이트를 교체하지 않습니다)")]
    [SerializeField] private Sprite _bashVfxSprite;


    public override WeaponType WeaponType => WeaponType.Sword;

    public override float ComboWindow        => 0.5f;
    public override int   MaxComboSteps      => 3;
    // 콤보 flip은 이 클래스 내부(this.transform Y-scale)에서 처리하므로 컨트롤러 레벨 flip 불필요
    public override bool  FlipComboDirection => false;

    private bool    _hitboxFired;

    // this.transform(SwordWeapon 루트) 기준 캐싱
    private Vector3 _restLocalScale;

    // 자식 애니메이터 localPosition 캐싱 (애니메이터가 위치를 덮어쓰는 것 방지)
    private Vector3 _weaponRestLocalPos;
    private Vector3 _vfxRestLocalPos;

    // ── 강타 효과 상태 ────────────────────────────────────────────────
    private StatSystem _statSystem;
    private Color      _originalSwordColor;
    private Color      _originalVfxColor;
    private Sprite     _originalVfxSprite;
    private bool       _bashEffectActive;
    private TrailRenderer _bashTrail;




    private void Awake()
    {
        CurrentComboStep = 1;

        // SwordWeapon 루트의 초기 스케일 저장
        _restLocalScale = transform.localScale;

        // 사용자의 요청으로 애니메이터 VFX는 비활성화합니다. (대신 스크립트로 잔상 생성)
        if (_vfxAnimator != null)
        {
            _vfxAnimator.transform.localScale = Vector3.one;
            _vfxAnimator.gameObject.SetActive(false); // 다시 끕니다
        }

        if (_weaponAnimator != null)
            _weaponRestLocalPos = _weaponAnimator.transform.localPosition;
        if (_vfxAnimator != null)
            _vfxRestLocalPos = _vfxAnimator.transform.localPosition;

        if (_hitboxCollider != null) _hitboxCollider.enabled = false;

        // 강타 효과용 원본값 캐싱
        _statSystem         = GetComponentInParent<StatSystem>();
        _originalSwordColor = _swordRenderer != null ? _swordRenderer.color : Color.white;
        _originalVfxColor   = _vfxRenderer   != null ? _vfxRenderer.color   : Color.white;
        _originalVfxSprite  = _vfxRenderer   != null ? _vfxRenderer.sprite  : null;

        CreateBashTrail();
    }

    private void CreateBashTrail()
    {
        if (_swordRenderer == null) return;

        GameObject trailObj = new GameObject("BashTrail");
        trailObj.transform.SetParent(_swordRenderer.transform);
        trailObj.transform.localPosition = new Vector3(0, 1.2f, 0); // 검 끝부분 대략치

        _bashTrail = trailObj.AddComponent<TrailRenderer>();
        _bashTrail.time = 0.25f;
        _bashTrail.minVertexDistance = 0.05f;
        
        _bashTrail.startWidth = 1.0f;
        _bashTrail.endWidth = 0.0f;
        
        Material glowMat = new Material(Shader.Find("Custom/SpriteGlow"));
        glowMat.EnableKeyword("_USE_MAIN_ALPHA_AS_GLOW");
        glowMat.SetFloat("_GlowIntensity", 4f);
        glowMat.SetColor("_GlowColor", _bashSwordColor);
        _bashTrail.material = glowMat;
        
        _bashTrail.sortingLayerName = "Weapons";
        _bashTrail.sortingOrder = 5;

        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        _bashTrail.colorGradient = g;
        
        _bashTrail.emitting = false;
    }


    private void LateUpdate()
    {
        if (_weaponAnimator != null)
        {
            // 검 스프라이트는 수동 수식(FloatingWeaponMotion)에 완벽하게 종속되도록 고정
            _weaponAnimator.transform.localPosition = _weaponRestLocalPos;
            _weaponAnimator.transform.localEulerAngles = Vector3.zero;
            // 애니메이션 키프레임에 의해 검의 크기가 찌그러지는 현상을 방지 (모든 프레임에서 일정한 크기 유지)
            _weaponAnimator.transform.localScale = Vector3.one;
        }

        if (_vfxAnimator != null)
        {
            // 사용하지 않지만 기본 로컬 위치로 고정
            _vfxAnimator.transform.localPosition = _vfxRestLocalPos;
            _vfxAnimator.transform.localEulerAngles = Vector3.zero;
        }

        // 강타 스택 변화 감지 및 비주얼 효과 동기화
        SyncBashEffect();

        // 궤적(Trail) 방출 동기화
        if (_bashTrail != null)
        {
            _bashTrail.emitting = _bashEffectActive && IsAttacking;
        }
    }

    /// <summary>
    /// 강타(Bash) 스택이 남아있는 동안 검 스프라이트에 붉은 HDR 블룸 색조를 적용합니다.
    /// 공격 로직 분리: 타격하는 순간(충돌) 스택이 차감되더라도 공격 모션이 완전히 끝날 때까지 비주얼을 유지합니다.
    /// </summary>
    private void SyncBashEffect()
    {
        if (_statSystem == null) return;
        
        bool hasStack = _statSystem.BashCount > 0;
        // 스택이 남아있거나, 현재 이펙트가 켜진 채로 공격 중이면 이펙트 유지 (도중 꺼짐 방지)
        bool shouldBeActive = hasStack || (_bashEffectActive && IsAttacking);
        
        if (shouldBeActive == _bashEffectActive) return;

        ApplyBashVisual(shouldBeActive);
    }

    private void ApplyBashVisual(bool active)
    {
        _bashEffectActive = active;

        if (_swordRenderer != null)
            _swordRenderer.color = active ? _bashSwordColor : _originalSwordColor;

        if (_vfxRenderer != null)
        {
            // SpriteAnimator가 _vfxRenderer.color를 매 프레임 덮어씌워서 무효화시키는 문제를 회피하기 위해,
            // 인스턴스화된 Material 자체의 컬러 속성을 변경해 강제로 붉은 기운(블룸)을 덧입힙니다.
            if (_vfxRenderer.material.HasProperty("_BaseColor"))
                _vfxRenderer.material.SetColor("_BaseColor", active ? _bashSwordColor : _originalVfxColor);
            else if (_vfxRenderer.material.HasProperty("_Color"))
                _vfxRenderer.material.SetColor("_Color", active ? _bashSwordColor : _originalVfxColor);
                
            // 혹시 몰라 기존 fallback도 유지
            _vfxRenderer.color = active ? _bashSwordColor : _originalVfxColor;

            // 스프라이트 교체 로직
            if (_bashVfxSprite != null)
                _vfxRenderer.sprite = active ? _bashVfxSprite : _originalVfxSprite;
        }


        // 공격 중이 아닐 때만 VFX 애니메이션 스테이트 전환 (공격 중엔 Attack 재생 유지)
        if (!IsAttacking && _vfxAnimator != null)
        {
            string state = active && !string.IsNullOrEmpty(_bashVfxStateName) ? _bashVfxStateName : "Idle";
            if (_vfxAnimator.HasState(0, Animator.StringToHash(state)))
                _vfxAnimator.Play(state, 0, 0f);
        }
    }

    /// <summary>BashSkillData.Execute()에서 호출 — 강타 효과를 즉시 활성화합니다.</summary>
    public override void SetBashEffectActive(bool active)
    {
        ApplyBashVisual(active);
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

        // 스윙 시작 시 강타 스택 소모 (적중 여부 무관하게 1회 차감)
        CurrentSwingBashMultiplier = _statSystem != null ? _statSystem.UseBashStack() : 1f;

        // 2타, 3타는 스윙 궤적과 시계방향 오르빗에 맞춰 칼날이 밖을 향하도록 Y를 뒤집습니다.
        float flipY = (comboStep >= 2) ? -1f : 1f;
        transform.localScale = new Vector3(
            _restLocalScale.x, _restLocalScale.y * flipY, _restLocalScale.z);

        // 공격 속도 연동 (StatSystem 반영)
        float speed = (_statSystem != null) ? _statSystem.TotalAttackSpeed : 1f;
        if (speed <= 0) speed = 1f;

        _weaponAnimator.ResetTrigger("Attack"); // 버퍼 방지 (1번 클릭에 2번 나가는 버그 수정)
        _weaponAnimator.speed = speed;
        _weaponAnimator.SetTrigger("Attack");

        if (_vfxAnimator != null && _vfxAnimator.gameObject.activeInHierarchy)
        {
            _vfxAnimator.speed = speed;
            _vfxAnimator.SetTrigger("Attack");
        }
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
            
            _weaponAnimator.ResetTrigger("Attack"); // 공격 종료 시 버퍼 초기화
            
            if (info.IsName("Attack"))
            {
                _weaponAnimator.Play("Idle", 0, 0f);
                // 강타 활성 중이면 BashIdle로, 아니면 Idle로 복귀
                if (_vfxAnimator != null)
                {
                    string vfxState = _bashEffectActive && !string.IsNullOrEmpty(_bashVfxStateName)
                        ? _bashVfxStateName : "Idle";
                    if (!_vfxAnimator.HasState(0, Animator.StringToHash(vfxState))) vfxState = "Idle";
                    _vfxAnimator.Play(vfxState, 0, 0f);
                }
            }
            // Idle 전환 직후 animation 커브 갱신 전에 rotation·scale을 리셋.
            // Idle 전환 시 즉시 리셋 제거 (FloatingWeaponMotion에서 처리)
            // ResetChildTransforms();
            return true;
        }
        return false;
    }

    public override void OnDeactivated()
    {
        IsAttacking  = false;
        _hitboxFired = false;
        StopAllCoroutines();

        // transform.localScale = _restLocalScale; // 즉시 리셋 제거
        if (_hitboxCollider != null) _hitboxCollider.enabled = false;
        if (_weaponAnimator  != null) _weaponAnimator.Play("Idle", 0, 0f);
        if (_vfxAnimator     != null) _vfxAnimator.Play("Idle", 0, 0f);

        ApplyBashVisual(false);
    }

    // 물리 사이클 2회 후 히트박스 비활성화 (1 physics frame 온전히 보장)
    private IEnumerator DisableHitboxNextPhysicsUpdate()
    {
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        if (_hitboxCollider != null) _hitboxCollider.enabled = false;
    }
}
