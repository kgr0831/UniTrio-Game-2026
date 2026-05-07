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

    [Header("Bash Effect")]
    [Tooltip("창 스프라이트 (강타 발동 시 붉은 블룸 적용)")]
    [SerializeField] private SpriteRenderer _spearRenderer;
    [Tooltip("강타 모드일 때 창 궤적 및 색상 값")]
    [ColorUsage(true, true)]
    [SerializeField] private Color _bashSpearColor = new Color(1.5f, 0.1f, 0.1f, 1f);

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
    private SpriteRenderer[] _vfxRenderers; // 자식 포함 전체 렌더러

    // 강타 효과 상태
    private StatSystem _statSystem;
    private Color      _originalSpearColor;
    private Material   _originalSpearMaterial;
    private Material[] _originalVfxMaterials;
    private Material   _spearGlowMaterial;
    private bool       _bashEffectActive;
    private TrailRenderer _bashTrail;

    private void Awake()
    {
        CurrentComboStep = 1;

        if (_vfxAnimator != null)
        {
            // GetComponent 대신 GetComponentsInChildren으로 자식까지 탐색
            _vfxRenderers = _vfxAnimator.GetComponentsInChildren<SpriteRenderer>(true);
            SetVfxVisible(false);

            if (_vfxRenderers != null)
            {
                _originalVfxMaterials = new Material[_vfxRenderers.Length];
                for (int i = 0; i < _vfxRenderers.Length; i++)
                    _originalVfxMaterials[i] = _vfxRenderers[i].material;
            }
        }

        if (_hitboxCollider != null) _hitboxCollider.enabled = false;

        _statSystem = GetComponentInParent<StatSystem>();

        // 인스펙터에서 할당 누락 시 자동 찾기
        if (_spearRenderer == null)
        {
            Transform spearTransform = transform.Find("Spear");
            if (spearTransform != null) _spearRenderer = spearTransform.GetComponent<SpriteRenderer>();
        }

        if (_spearRenderer != null)
        {
            _originalSpearColor = _spearRenderer.color;
            _originalSpearMaterial = _spearRenderer.material;
            
            // 붉은 블룸 범용 머티리얼 구성
            _spearGlowMaterial = new Material(Shader.Find("Custom/SpriteGlow"));
            _spearGlowMaterial.EnableKeyword("_USE_MAIN_ALPHA_AS_GLOW");
            _spearGlowMaterial.SetFloat("_GlowIntensity", 4f);
            
            // 너무 어두운 색이면 강제로 증폭
            Color bloomCol = _bashSpearColor;
            if (bloomCol.r < 1f && bloomCol.g < 1f && bloomCol.b < 1f) bloomCol *= 2f;
            _spearGlowMaterial.SetColor("_GlowColor", bloomCol);
        }
        
        CreateBashTrail();
    }

    private void CreateBashTrail()
    {
        if (_spearRenderer == null) return;

        // 궤적 생성 위치 기준을 수동 오프셋 추측이 아닌 명확한 자식 오브젝트로 고정합니다.
        Transform trailParent = _spearRenderer.transform;
        
        // 찌르기 이펙트(VFX, 검기)가 터지는 중심이 가장 이상적인 궤적 발생점
        if (_vfxRenderers != null && _vfxRenderers.Length > 0 && _vfxRenderers[0] != null)
        {
            trailParent = _vfxRenderers[0].transform;
        }
        else if (_hitboxCollider != null)
        {
            // 이펙트를 못 찾으면 타격 판정(Hitbox)의 중심을 활용
            trailParent = _hitboxCollider.transform;
        }

        GameObject trailObj = new GameObject("BashTrail");
        trailObj.transform.SetParent(trailParent);
        // 부모 오브젝트의 딱 정중앙(0,0,0)에 부착 (위치 노가다 X)
        trailObj.transform.localPosition = Vector3.zero; 

        _bashTrail = trailObj.AddComponent<TrailRenderer>();
        _bashTrail.time = 0.25f;
        _bashTrail.minVertexDistance = 0.05f;
        _bashTrail.startWidth = 1.0f;
        _bashTrail.endWidth = 0.0f;

        _bashTrail.material = _spearGlowMaterial;

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
        SyncBashEffect();
        if (_bashTrail != null)
        {
            _bashTrail.emitting = _bashEffectActive && IsAttacking;
        }
    }

    private void SyncBashEffect()
    {
        if (_statSystem == null) return;
        
        bool hasStack = _statSystem.BashCount > 0;
        bool shouldBeActive = hasStack || (_bashEffectActive && IsAttacking);
        
        if (shouldBeActive == _bashEffectActive) return;
        ApplyBashVisual(shouldBeActive);
    }

    private void ApplyBashVisual(bool active)
    {
        _bashEffectActive = active;
        if (_spearRenderer != null)
        {
            // 단순 붉은 색조 대신 완전한 블룸 머티리얼로 통째로 스왑
            _spearRenderer.material = active ? _spearGlowMaterial : _originalSpearMaterial;
            _spearRenderer.color = active ? Color.white : _originalSpearColor;
        }

        if (_vfxRenderers != null && _originalVfxMaterials != null)
        {
            for (int i = 0; i < _vfxRenderers.Length; i++)
            {
                var r = _vfxRenderers[i];
                if (r == null) continue;
                
                r.material = active ? _spearGlowMaterial : _originalVfxMaterials[i];
                // 머티리얼이 교체되므로 본래 색상(흰색)으로 유지
                r.color = Color.white; 
            }
        }
    }

    public override void SetBashEffectActive(bool active)
    {
        ApplyBashVisual(active);
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

        // 공격 시작 시 강타 스택 사용
        CurrentSwingBashMultiplier = _statSystem != null ? _statSystem.UseBashStack() : 1f;

        SetVfxVisible(true);

        // StatSystem 연동 (기본 속도 복원)
        float speed = (_statSystem != null) ? _statSystem.TotalAttackSpeed : 1f;
        if (speed <= 0) speed = 1f;

        _weaponAnimator.speed = speed;
        _weaponAnimator.SetTrigger("Attack");
        
        if (_vfxAnimator != null)
        {
            _vfxAnimator.speed = speed;
            _vfxAnimator.SetTrigger("Attack");
        }
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

        ApplyBashVisual(false);
    }

    private IEnumerator DisableHitboxAfterThrust()
    {
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        if (_hitboxCollider != null) _hitboxCollider.enabled = false;
    }
}
