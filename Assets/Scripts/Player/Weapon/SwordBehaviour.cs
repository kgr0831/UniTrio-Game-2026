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
    [SerializeField] private SwordHitbox _swordHitbox;

    [Header("Hitbox Size Per Combo")]
    [Tooltip("1,2타용 히트박스 크기 (BoxCollider2D 기준)")]
    [SerializeField] private Vector2 _normalHitboxSize = new Vector2(1.6f, 1.0f);
    [Tooltip("3타용 히트박스 크기 (더 넓은 범위)")]
    [SerializeField] private Vector2 _finisherHitboxSize = new Vector2(2.2f, 1.4f);

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
    public override bool  FlipComboDirection => true;

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
        _bashTrail.minVertexDistance = 0.01f;
        
        // 날렵한 테이퍼링 (끝으로 갈수록 얇아지게)
        AnimationCurve widthCurve = new AnimationCurve();
        widthCurve.AddKey(new Keyframe(0f, 1.2f, 0f, -2f));
        widthCurve.AddKey(new Keyframe(1f, 0.0f));
        _bashTrail.widthCurve = widthCurve;
        
        Material vfxMat = new Material(Shader.Find("Custom/VFXLit2D"));
        vfxMat.SetFloat("_EmissionIntensity", 4f);
        vfxMat.SetColor("_EmissionColor", _bashSwordColor);
        vfxMat.SetFloat("_LightInfluence", 0.3f);
        _bashTrail.material = vfxMat;
        
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

    private bool _isFlippedY;

    public override void SetFlipY(bool flip, bool isAimingLeft)
    {
        base.SetFlipY(flip, isAimingLeft);
        _isFlippedY = flip;
        if (_swordRenderer != null)
        {
            _swordRenderer.flipY = flip;
        }
        if (_bashTrail != null)
        {
            float yOff = flip ? -1.2f : 1.2f;
            _bashTrail.transform.localPosition = new Vector3(0f, yOff, 0f);
        }
    }

    public override void AddTrailPosition(Vector3 rendererWorldPos, Quaternion rendererRot)
    {
        if (_bashTrail != null && _bashTrail.emitting)
        {
            float yOff = _isFlippedY ? -1.2f : 1.2f;
            Vector3 tipWorldPos = rendererWorldPos + rendererRot * new Vector3(0, yOff, 0);
            _bashTrail.AddPosition(tipWorldPos);
        }
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

        // 이동 중 공격 시 히트박스 위치 동기화 (AutoSyncTransforms=0 환경)
        if (IsAttacking && _hitboxCollider != null && _hitboxCollider.enabled)
            Physics2D.SyncTransforms();

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

    public override void SetWeaponSprite(Sprite sprite)
    {
        if (_swordRenderer != null && sprite != null)
            _swordRenderer.sprite = sprite;
    }

    /// <summary>BashSkillData.Execute()에서 호출 — 강타 효과를 즉시 활성화합니다.</summary>
    public override void SetBashEffectActive(bool active)
    {
        ApplyBashVisual(active);
    }

    /// <summary>Idle 전환 직후 애니메이터가 갱신되기 전에 rotation과 루트 스케일을 리셋합니다.</summary>
    private void ResetChildTransforms()
    {
        // SwordWeapon 스케일/회전 리셋
        transform.localScale = _restLocalScale;
        transform.localRotation = Quaternion.identity;

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

    public override float GetCurrentAttackSpeedMultiplier()
    {
        float speed = (_statSystem != null) ? _statSystem.TotalAttackSpeed : 1f;
        if (speed <= 0) speed = 1f;
        return speed + GetAnimationSpeedBonus();
    }

    public override void BeginAttack(int comboStep)
    {
        if (_bashTrail != null)
            _bashTrail.Clear();

        IsAttacking      = true;
        _hitboxFired     = false;
        CurrentComboStep = comboStep;

        // 스윙 시작 시 강타 스택 소모 (적중 여부 무관하게 1회 차감)
        CurrentSwingBashMultiplier = _statSystem != null ? _statSystem.UseBashStack() : 1f;

        // 회전/스케일 강제 영구 고정 (음수 스케일/회전 대칭 방지)
        transform.localRotation = Quaternion.identity;
        transform.localScale = _restLocalScale * ChargeSizeMultiplier;

        // 히트박스 크기 조절 및 활성화
        if (_hitboxCollider is BoxCollider2D box)
            box.size = (comboStep >= 3) ? _finisherHitboxSize : _normalHitboxSize;

        if (_swordHitbox != null) _swordHitbox.ResetSwingHits();
        if (_hitboxCollider != null) _hitboxCollider.enabled = true;
        Physics2D.SyncTransforms();

        // 원래 배율 복구 (StatSystem)
        float speed = (_statSystem != null) ? _statSystem.TotalAttackSpeed : 1f;
        if (speed <= 0) speed = 1f;
        speed += GetAnimationSpeedBonus();

        _weaponAnimator.ResetTrigger("Attack");
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
        float elapsed = Time.time - attackStartTime;
        if (elapsed < 0.05f) return false;

        float orbitDuration;
        switch (CurrentComboStep)
        {
            case 1:  orbitDuration = 0.35f / 1.0f; break;
            case 2:  orbitDuration = 0.45f / 1.0f; break;
            default: orbitDuration = 0.40f / (1.0f * 0.8f); break;
        }

        float speed = (_statSystem != null) ? _statSystem.TotalAttackSpeed : 1f;
        if (speed <= 0) speed = 1f;
        speed += GetAnimationSpeedBonus();
        
        orbitDuration /= speed;

        if (elapsed >= orbitDuration)
        {
            IsAttacking = false;
            if (_hitboxCollider != null) _hitboxCollider.enabled = false;

            _weaponAnimator.ResetTrigger("Attack");
            _weaponAnimator.Play("Idle", 0, 0f);

            if (_vfxAnimator != null)
            {
                string vfxState = _bashEffectActive && !string.IsNullOrEmpty(_bashVfxStateName)
                    ? _bashVfxStateName : "Idle";
                if (!_vfxAnimator.HasState(0, Animator.StringToHash(vfxState))) vfxState = "Idle";
                _vfxAnimator.Play(vfxState, 0, 0f);
            }
            return true;
        }
        return false;
    }

    public override void OnDeactivated()
    {
        IsAttacking  = false;
        _hitboxFired = false;

        if (_hitboxCollider != null) _hitboxCollider.enabled = false;
        if (_weaponAnimator  != null) _weaponAnimator.Play("Idle", 0, 0f);
        if (_vfxAnimator     != null) _vfxAnimator.Play("Idle", 0, 0f);

        ApplyBashVisual(false);
    }
}
