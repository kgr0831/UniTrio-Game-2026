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
    [SerializeField] private Animator _weaponAnimator;
    [SerializeField] private Animator _vfxAnimator;

    [Header("Sprite Rotation")]
    [Tooltip("창 스프라이트의 로컬 Z 회전. 날이 오른쪽(커서 방향)을 향하도록 조절하세요.")]
    [SerializeField] private float _spriteRotationZ = -45f;

    [Header("Hitbox")]
    [SerializeField] private Collider2D _hitboxCollider;
    [SerializeField] private SwordHitbox _spearHitbox;

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

    [Header("Thrust Timing")]
    [Tooltip("찌르기 전체 시간 (초). FloatingWeaponMotion의 Spear Duration과 맞춰주세요.")]
    [SerializeField] private float _thrustDuration = 0.30f;

    public override float ComboWindow   => 0f;
    public override int   MaxComboSteps => 1;

    private bool             _hitboxFired;

    // Animator 자식 트랜스폼 고정용 (FloatingWeaponMotion과 충돌 방지)
    private Vector3 _weaponRestLocalPos;

    // 강타 효과 상태
    private StatSystem _statSystem;
    private Color      _originalSpearColor;
    private Color      _originalGlowColor;
    private float      _originalGlowIntensity;
    private bool       _bashEffectActive;
    private TrailRenderer _bashTrail;

    private void Awake()
    {
        CurrentComboStep = 1;

        // VFX 자식 오브젝트 비활성화
        Transform vfxChild = transform.Find("SpearVFX");
        if (vfxChild != null)
            vfxChild.gameObject.SetActive(false);

        // SpearHitBox를 찾아서 루트로 이동 후 물리 바디 재구성
        Transform hitboxChild = null;
        if (vfxChild != null) hitboxChild = vfxChild.Find("SpearHitBox");
        if (hitboxChild == null) hitboxChild = transform.Find("SpearHitBox");

        if (hitboxChild != null)
        {
            hitboxChild.SetParent(transform);
            hitboxChild.localPosition = Vector3.zero;
            hitboxChild.localEulerAngles = Vector3.zero;
            hitboxChild.localScale = Vector3.one;

            // 자식의 Rigidbody2D 제거 → 부모 Rigidbody2D에 자동 연결
            Rigidbody2D childRb = hitboxChild.GetComponent<Rigidbody2D>();
            if (childRb != null) Destroy(childRb);
        }

        // 루트에 Rigidbody2D 확보 (무기와 동일한 물리 위치 보장)
        Rigidbody2D rootRb = GetComponent<Rigidbody2D>();
        if (rootRb == null) rootRb = gameObject.AddComponent<Rigidbody2D>();
        rootRb.bodyType = RigidbodyType2D.Kinematic;
        rootRb.constraints = RigidbodyConstraints2D.FreezeAll;

        // 콜라이더 탐색 및 설정
        if (_hitboxCollider == null && hitboxChild != null)
            _hitboxCollider = hitboxChild.GetComponent<Collider2D>();
        if (_spearHitbox == null)
            _spearHitbox = GetComponentInChildren<SwordHitbox>();

        if (_hitboxCollider != null)
        {
            if (_hitboxCollider is BoxCollider2D box)
            {
                // 날 끝(45° 방향)으로 충분히 확장 (2x 스케일 기준 월드 도달 ~3유닛)
                box.offset = new Vector2(0.7f, 0.7f);
                box.size = new Vector2(1.0f, 1.0f);
            }
            _hitboxCollider.enabled = false;
        }

        if (_weaponAnimator != null)
            _weaponRestLocalPos = _weaponAnimator.transform.localPosition;

        _statSystem = GetComponentInParent<StatSystem>();

        if (_spearRenderer == null)
        {
            Transform spearTransform = transform.Find("Spear");
            if (spearTransform != null) _spearRenderer = spearTransform.GetComponent<SpriteRenderer>();
        }

        if (_spearRenderer != null)
        {
            _originalSpearColor = _spearRenderer.color;
            Material mat = _spearRenderer.material;
            _originalGlowColor = mat.HasProperty("_GlowColor") ? mat.GetColor("_GlowColor") : Color.white;
            _originalGlowIntensity = mat.HasProperty("_GlowIntensity") ? mat.GetFloat("_GlowIntensity") : 1f;
        }

        CreateBashTrail();
    }

    private void CreateBashTrail()
    {
        if (_spearRenderer == null) return;

        Transform trailParent = _spearRenderer.transform;

        GameObject trailObj = new GameObject("BashTrail");
        trailObj.transform.SetParent(trailParent);
        trailObj.transform.localPosition = new Vector3(0, 1.2f, 0);

        _bashTrail = trailObj.AddComponent<TrailRenderer>();
        _bashTrail.time = 0.25f;
        _bashTrail.minVertexDistance = 0.05f;
        _bashTrail.startWidth = 1.0f;
        _bashTrail.endWidth = 0.0f;

        Material trailMat = new Material(Shader.Find("Custom/VFXLit2D"));
        trailMat.SetFloat("_EmissionIntensity", 4f);
        trailMat.SetColor("_EmissionColor", _bashSpearColor);
        trailMat.SetFloat("_LightInfluence", 0.3f);
        _bashTrail.material = trailMat;

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
            _weaponAnimator.transform.localPosition = _weaponRestLocalPos;
            // 자식 회전 항상 0: 무기는 아이들 시 비가시 상태이므로 _spriteRotationZ 불필요
            // 공격 중 날 방향은 FloatingWeaponMotion의 slashRotZ가 무기 루트 회전으로 전담
            _weaponAnimator.transform.localEulerAngles = Vector3.zero;
            _weaponAnimator.transform.localScale = Vector3.one;
        }
        if (_vfxAnimator != null)
        {
            _vfxAnimator.transform.localPosition = Vector3.zero;
            _vfxAnimator.transform.localEulerAngles = Vector3.zero;
            _vfxAnimator.transform.localScale = Vector3.one;
        }

        if (IsAttacking && _hitboxCollider != null && _hitboxCollider.enabled)
            Physics2D.SyncTransforms();

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
        
        if (_vfxAnimator != null)
        {
            _vfxAnimator.gameObject.SetActive(active);
        }

        if (_spearRenderer == null) return;

        Material mat = _spearRenderer.material;
        if (active)
        {
            if (mat.HasProperty("_GlowColor"))
                mat.SetColor("_GlowColor", _bashSpearColor);
            if (mat.HasProperty("_GlowIntensity"))
                mat.SetFloat("_GlowIntensity", 4f);
            _spearRenderer.color = Color.white;
        }
        else
        {
            if (mat.HasProperty("_GlowColor"))
                mat.SetColor("_GlowColor", _originalGlowColor);
            if (mat.HasProperty("_GlowIntensity"))
                mat.SetFloat("_GlowIntensity", _originalGlowIntensity);
            _spearRenderer.color = _originalSpearColor;
        }
    }

    public override void SetWeaponSprite(Sprite sprite)
    {
        if (_spearRenderer != null && sprite != null)
            _spearRenderer.sprite = sprite;
    }

    public override void SetBashEffectActive(bool active)
    {
        ApplyBashVisual(active);
    }

    public override void BeginAttack(int comboStep)
    {
        IsAttacking      = true;
        _hitboxFired     = false;
        CurrentComboStep = 1;

        CurrentSwingBashMultiplier = _statSystem != null ? _statSystem.UseBashStack() : 1f;

        if (_spearHitbox != null) _spearHitbox.ResetSwingHits();
        if (_hitboxCollider != null) _hitboxCollider.enabled = true;
        Physics2D.SyncTransforms();

        float speed = (_statSystem != null) ? _statSystem.TotalAttackSpeed : 1f;
        if (speed <= 0) speed = 1f;

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

        float thrustDuration = _thrustDuration;
        float speed = (_statSystem != null) ? _statSystem.TotalAttackSpeed : 1f;
        if (speed > 0f) thrustDuration /= speed;

        if (elapsed >= thrustDuration)
        {
            IsAttacking = false;
            if (_hitboxCollider != null) _hitboxCollider.enabled = false;

            _weaponAnimator.ResetTrigger("Attack");
            _weaponAnimator.Play("Idle", 0, 0f);

            if (_vfxAnimator != null)
            {
                _vfxAnimator.Play("Idle", 0, 0f);
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

}
