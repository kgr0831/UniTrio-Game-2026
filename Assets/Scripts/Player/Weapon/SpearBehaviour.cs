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

    [Header("Tip Critical")]
    [Tooltip("창 끝 타격 판정 거리 (플레이어 기준, 이 거리 이상이면 크리티컬)")]
    [SerializeField] private float _tipMinDistance = 2.0f;
    [Tooltip("창 끝 타격 시 데미지 배율")]
    [SerializeField] private float _tipDamageMultiplier = 1.5f;

    /// <summary>팁 크리티컬 데미지 배율 (SwordHitbox에서 참조)</summary>
    public float TipDamageMultiplier => _tipDamageMultiplier;

    /// <summary>true일 때 거리 판정 없이 항상 끝사거리 크리티컬 처리</summary>
    public bool ForceIsTip { get; set; }

    // 공격 시 방향 벡터 (거리 판정용)
    private Vector2 _attackDirection;
    private Transform _playerRoot;

    // hit-a_0 VFX 리소스
    private RuntimeAnimatorController _hitVfxController;
    private Sprite[] _hitVfxSprites;

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

    // --- Spear Stack System ---
    private GameObject _spearAuraInstance;
    private PlayerWeaponController _controller;

    private void Awake()
    {
        CurrentComboStep = 1;

        HitEventManager.OnEnemyHit -= HandleEnemyHit;
        HitEventManager.OnEnemyHit += HandleEnemyHit;

        _controller = GetComponentInParent<PlayerWeaponController>();
        if (_controller != null)
        {
            _controller.OnSpearStacksChanged += HandleSpearStacksChanged;
        }

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

        // 팁 VFX 리소스 로드
        _hitVfxController = Resources.Load<RuntimeAnimatorController>("Spritessheets/hit-a_0");
        _hitVfxSprites = Resources.LoadAll<Sprite>("Spritessheets/hit-a");
        _playerRoot = GetComponentInParent<PlayerEntity>()?.transform;
        if (_playerRoot == null) _playerRoot = transform.root;

        CreateSpearAura();
    }

    private void OnDestroy()
    {
        HitEventManager.OnEnemyHit -= HandleEnemyHit;
        if (_controller != null)
        {
            _controller.OnSpearStacksChanged -= HandleSpearStacksChanged;
        }
    }

    private void HandleEnemyHit(Vector3 sourcePos, Vector3 targetPos, bool isFirstHit)
    {
        if (!gameObject.activeInHierarchy || !IsAttacking) return;
        
        // 한 번 휘두를 때 여러 마리를 맞춰도 스택은 1번만 오르게 하려면 _hitboxFired 체크
        if (!_hitboxFired)
        {
            _hitboxFired = true;
            if (_controller != null)
            {
                _controller.AddSpearStack();
            }
        }
    }

    private void HandleSpearStacksChanged(int stacks)
    {
        Debug.Log($"[Spear] 현재 글로벌 스택: {stacks}");

        if (stacks >= 5 && _spearAuraInstance != null)
        {
            Debug.Log($"[Spear] 5스택 도달! 오오라 활성화");
            _spearAuraInstance.SetActive(true);
            ParticleSystem ps = _spearAuraInstance.GetComponent<ParticleSystem>();
            if (ps != null && !ps.isPlaying)
            {
                ps.Play();
            }
        }
        else if (stacks == 0 && _spearAuraInstance != null)
        {
            Debug.Log($"[Spear] 스택 초기화 (오오라 끄기)");
            ParticleSystem ps = _spearAuraInstance.GetComponent<ParticleSystem>();
            if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _spearAuraInstance.SetActive(false);
        }
    }

    private void CreateSpearAura()
    {
        if (_playerRoot == null) return;
        _spearAuraInstance = new GameObject("SpearAura_Red");
        _spearAuraInstance.transform.SetParent(_playerRoot);
        _spearAuraInstance.transform.localPosition = new Vector3(0, 0.5f, 0); // 캐릭터 중심
        _spearAuraInstance.SetActive(false);

        var ps = _spearAuraInstance.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.startLifetime = 0.5f;
        main.startSpeed = 2f;
        main.startSize = 0.25f; // 0.5배로 축소 (기존 0.5f)
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        var em = ps.emission;
        em.rateOverTime = 20f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.8f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.red, 0f), new GradientColorKey(Color.yellow, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = grad;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.sortingLayerName = "Weapons";
        renderer.sortingOrder = 10;
        
        // VFXLit2D 매터리얼 사용
        Material mat = new Material(Shader.Find("Custom/VFXLit2D"));
        mat.SetFloat("_EmissionIntensity", 4f);
        mat.SetColor("_EmissionColor", new Color(1.5f, 0.1f, 0.1f, 1f));
        mat.SetFloat("_LightInfluence", 0.3f);
        renderer.material = mat;
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
        _bashTrail.minVertexDistance = 0.01f;

        AnimationCurve widthCurve = new AnimationCurve();
        widthCurve.AddKey(new Keyframe(0f, 1.2f, 0f, -2f));
        widthCurve.AddKey(new Keyframe(1f, 0.0f));
        _bashTrail.widthCurve = widthCurve;

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

    private bool _isFlippedY;

    public override void SetFlipY(bool flip, bool isAimingLeft)
    {
        base.SetFlipY(flip, isAimingLeft);
        _isFlippedY = flip;
        if (_spearRenderer != null)
        {
            _spearRenderer.flipY = flip;
        }
        if (_hitboxCollider != null && _hitboxCollider is BoxCollider2D box)
        {
            box.offset = new Vector2(box.offset.x, flip ? -Mathf.Abs(box.offset.y) : Mathf.Abs(box.offset.y));
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

    public override float GetCurrentAttackSpeedMultiplier()
    {
        return (1.3f + GetAnimationSpeedBonus()) / 1.3f;
    }

    public override void BeginAttack(int comboStep)
    {
        if (_bashTrail != null)
            _bashTrail.Clear();

        IsAttacking      = true;
        _hitboxFired     = false;
        CurrentComboStep = 1;

        CurrentSwingBashMultiplier = _statSystem != null ? _statSystem.UseBashStack() : 1f;

        if (_spearHitbox != null) _spearHitbox.ResetSwingHits();
        if (_hitboxCollider != null) _hitboxCollider.enabled = true;

        // 공격 방향 저장 (팁 크리티컬 거리 판정용)
        // 피벗의 right 벡터 = 커서 방향 (Z축 회전 기반)
        Transform pivot = transform.parent;
        _attackDirection = pivot != null ? (Vector2)pivot.right : Vector2.right;

        transform.localScale = Vector3.one * ChargeSizeMultiplier;
        Physics2D.SyncTransforms();

        float speed = 1.3f + GetAnimationSpeedBonus();

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

        float speed = 1.3f + GetAnimationSpeedBonus();
        // 기본 0.3초 (배속 1.3f 기준) -> 현재 배속에 맞게 시간 축소
        float thrustDuration = 0.3f * (1.3f / speed);

        if (elapsed >= thrustDuration)
        {
            IsAttacking = false;
            if (_hitboxCollider != null) _hitboxCollider.enabled = false;

            _weaponAnimator.ResetTrigger("Attack");
            _weaponAnimator.Play("Idle", 0, 0f);

            if (_vfxAnimator != null && _vfxAnimator.gameObject.activeInHierarchy)
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

    /// <summary>
    /// 적 타격 위치가 창 끝 사거리(Tip)인지 판정합니다.
    /// 플레이어→적 벡터를 공격 방향에 투영하여 거리가 _tipMinDistance 이상이면 true.
    /// </summary>
    public bool IsTipHit(Vector3 enemyWorldPos)
    {
        if (!IsAttacking) return false;

        Vector2 toEnemy = (Vector2)enemyWorldPos - (Vector2)_playerRoot.position;
        float projDist = Vector2.Dot(toEnemy, _attackDirection);

        return projDist >= _tipMinDistance;
    }

    /// <summary>
    /// 창 끝 크리티컬 타격 시 hit-a_0 VFX를 타격 위치에 생성합니다.
    /// </summary>
    public void SpawnTipVFX(Vector3 hitWorldPos)
    {
        GameObject vfxObj = new GameObject("SpearTipHitVFX");
        vfxObj.transform.position = hitWorldPos;
        vfxObj.transform.localScale = Vector3.one * 10f;

        SpriteRenderer sr = vfxObj.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Weapons";
        sr.sortingOrder = 30;

        if (_hitVfxSprites != null && _hitVfxSprites.Length > 0)
            sr.sprite = _hitVfxSprites[0];

        if (_hitVfxController != null)
        {
            Animator animator = vfxObj.AddComponent<Animator>();
            animator.runtimeAnimatorController = _hitVfxController;
            vfxObj.AddComponent<DestroyAfterAnimation>();
        }
        else
        {
            Object.Destroy(vfxObj, 0.5f);
        }
    }
}
