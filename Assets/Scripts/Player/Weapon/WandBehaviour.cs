using UnityEngine;

/// <summary>
/// 완드(Wand) 무기의 공격 로직, 글로우 효과, 투사체 발사를 전담합니다.
///
/// 글로우 구조:
///   - Wand 오브젝트에는 SpriteGlow 셰이더를 사용하는 Material이 설정되어야 합니다.
///   - 빛나는 부분만 담은 별도 스프라이트를 _glowSpriteRenderer 에 연결합니다.
///   - _glowSpriteRenderer 의 Material 도 SpriteGlow 셰이더여야 합니다.
///   - 글로우는 공격 시작부터 sin곡선으로 빌드업 후 발사 직후 최고치에 달합니다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class WandBehaviour : WeaponBehaviourBase
{
    [Header("Animator")]
    [Tooltip("Wand 오브젝트의 Animator를 연결하세요.")]
    [SerializeField] private Animator _weaponAnimator;

    [Header("Stats (플레이어 스탯 연동)")]
    [Tooltip("Player 루트 오브젝트의 PlayerEntity 컴포넌트를 인스펙터에서 연결하세요.")]
    [SerializeField] private PlayerEntity _playerEntity;

    [Header("Projectile")]
    [Tooltip("MagicProjectile 컴포넌트가 붙은 프리팹을 연결하세요.")]
    [SerializeField] private GameObject _projectilePrefab;
    [Tooltip("투사체가 생성될 위치 (완드 끝 MuzzlePoint 트랜스폼).")]
    [SerializeField] private Transform  _muzzlePoint;
    [SerializeField] private float _projectileSpeed  = 12f;
    [SerializeField] private float _projectileDamage = 15f;

    [Header("Attack Timing")]
    [Tooltip("투사체를 발사할 normalizedTime. 0.5 = 애니메이션 절반 지점")]
    [SerializeField] [Range(0f, 1f)] private float _fireNormalizedTime = 0.5f;

    [Header("Glow Effect")]
    [Tooltip("완드 스프라이트 중 빛나는 부분만 담은 별도 SpriteRenderer.\n" +
             "이 오브젝트의 Material은 Custom/SpriteGlow 셰이더를 사용해야 합니다.\n" +
             "없으면 메인 SpriteRenderer에 글로우를 적용합니다.")]
    [SerializeField] private SpriteRenderer _glowSpriteRenderer;

    [Tooltip("글로우 빌드업 시작 시점. 0 = 공격 시작과 동시에 시작됩니다.")]
    [SerializeField] [Range(0f, 1f)] private float _glowStartNTime = 0.0f;
    [Tooltip("글로우 최고점 시점 (투사체 발사 타이밍과 맞추는 것을 권장).")]
    [SerializeField] [Range(0f, 1f)] private float _glowPeakNTime  = 0.5f;
    [Tooltip("글로우 소멸 완료 시점.")]
    [SerializeField] [Range(0f, 1f)] private float _glowEndNTime   = 0.85f;
    [Tooltip("글로우 최대 강도. Bloom Threshold(보통 1.0)를 넘겨야 화면에 번집니다.")]
    [SerializeField] private float _maxGlowIntensity = 35f;
    [Tooltip("글로우 색상 (HDR). 강도가 Bloom Threshold를 넘기면 화면에 번집니다.")]
    [ColorUsage(true, true)]
    [SerializeField] private Color _glowColor = new Color(0f, 0.5f, 1f, 1f);

    [Header("Orbit")]
    [Tooltip("플레이어 중심으로부터 완드가 떨어지는 거리.")]
    [SerializeField] private float _orbitRadius = 0.2f;

    [Header("Instant Cast Mode")]
    [Tooltip("체크 시 지팡이 모션 없이 즉시 마법구를 발사합니다.")]
    [SerializeField] private bool _isInstantCastMode = false;
    [Tooltip("즉시 발사 모드일 때 다음 공격까지의 딜레이(쿨타임).")]
    [SerializeField] private float _instantCastDelay = 0.5f;

    // ── WeaponBehaviourBase 오버라이드 ──────────────────────────────
    public override WeaponType WeaponType          => WeaponType.Staff;
    public override bool  UseYScaleFlip            => true;
    public override bool  LockRotationDuringAttack => true;
    public override bool  FlipComboDirection       => false;
    public override bool  UseGoBehind              => true;
    public override float OrbitRadius              => _orbitRadius;
    public override float ComboWindow              => 0f;
    public override int   MaxComboSteps            => 1;
    public override float PivotRotationOffset      => 0f;

    // ── 내부 상태 ──────────────────────────────────────────────────
    private bool    _hasFired;
    private Vector3 _localPosCached;

    private Camera                _cam;
    private SpriteRenderer        _spriteRenderer;      // 메인 렌더러 (fallback)
    private MaterialPropertyBlock _propBlock;
    private MaterialPropertyBlock _glowPropBlock;

    private static readonly int _glowIntensityId = Shader.PropertyToID("_EmissionIntensity");
    private static readonly int _glowColorId     = Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        CurrentComboStep = 1;
        _spriteRenderer  = GetComponent<SpriteRenderer>();
        _propBlock       = new MaterialPropertyBlock();
        _glowPropBlock   = new MaterialPropertyBlock();
        _localPosCached  = transform.localPosition;
        _cam             = Camera.main;

        // 시작할 때는 빛을 끕니다.
        UpdateGlow(0f);

        // 매터리얼 설정 확인 (디버깅 지원)
        ValidateMaterialSettings();

        // 즉시 발사 모드일 경우 외형(렌더러, 애니메이터, 둥실 모션)을 모두 끕니다.
        if (_isInstantCastMode)
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = false;

            if (_weaponAnimator != null) _weaponAnimator.enabled = false;

            var floatingMotion = GetComponent<FloatingWeaponMotion>();
            if (floatingMotion != null) floatingMotion.enabled = false;
        }
    }

    private void UpdateGlow(float intensity)
    {
        _glowPropBlock.SetFloat(_glowIntensityId, intensity);
        _glowPropBlock.SetColor(_glowColorId, _glowColor);
        if (_glowSpriteRenderer != null)
            _glowSpriteRenderer.SetPropertyBlock(_glowPropBlock);
        else
            _spriteRenderer.SetPropertyBlock(_glowPropBlock);
    }

    private void ValidateMaterialSettings()
    {
        SpriteRenderer target = _glowSpriteRenderer != null ? _glowSpriteRenderer : _spriteRenderer;
        if (target != null && target.sharedMaterial != null)
        {
            Material mat = target.sharedMaterial;
            if (!mat.HasProperty("_EmissionTex"))
            {
                Debug.LogWarning($"[WandBehaviour] '{mat.name}' 매터리얼에 _EmissionTex 속성이 없습니다! Custom/SpriteGlow 셰이더를 사용 중인지 확인하세요.");
            }
            else if (mat.GetTexture("_EmissionTex") == null)
            {
                Debug.LogWarning("[WandBehaviour] 매터리얼의 'Emission Texture (Secondary)' 슬롯이 비어 있습니다! 새로 가져오신 이미지를 여기에 할당해야 빛이 납니다.");
            }
        }
    }

    private void LateUpdate()
    {
        if (_weaponAnimator == null) return;
        _weaponAnimator.transform.localPosition = _localPosCached;
        if (!IsAttacking)
            _weaponAnimator.transform.localEulerAngles = Vector3.zero;
    }

    // ── WeaponBehaviourBase 구현 ───────────────────────────────────

    public override void BeginAttack(int comboStep)
    {
        IsAttacking      = true;
        _hasFired        = false;
        CurrentComboStep = 1;

        if (_isInstantCastMode)
        {
            FireProjectile();
            _hasFired = true;
        }
        else if (_weaponAnimator != null)
        {
            _weaponAnimator.SetTrigger("Attack");
        }
    }

    public override bool PollFinished(float attackStartTime)
    {
        // 즉시 발사 모드
        if (_isInstantCastMode)
        {
            if (Time.time - attackStartTime >= _instantCastDelay)
            {
                IsAttacking = false;
                return true;
            }
            return false;
        }

        if (_weaponAnimator == null) { IsAttacking = false; return true; }
        if (Time.time - attackStartTime < 0.05f) return false;

        AnimatorStateInfo info = _weaponAnimator.GetCurrentAnimatorStateInfo(0);
        float t = info.normalizedTime;

        // ── 글로우 제어 ─────────────────────────────────────────────
        // 구간: [_glowStartNTime → _glowPeakNTime] 빌드업 / [_glowPeakNTime → _glowEndNTime] 페이드아웃
        float intensity = 0f;
        if (info.IsName("Attack"))
        {
            if (t >= _glowStartNTime && t < _glowPeakNTime)
            {
                // 빌드업: 0 → max (SmoothStep)
                float phase = (t - _glowStartNTime) / Mathf.Max(0.001f, _glowPeakNTime - _glowStartNTime);
                intensity = Mathf.SmoothStep(0f, _maxGlowIntensity, phase);
            }
            else if (t >= _glowPeakNTime && t <= _glowEndNTime)
            {
                // 페이드아웃: max → 0 (SmoothStep)
                float phase = (t - _glowPeakNTime) / Mathf.Max(0.001f, _glowEndNTime - _glowPeakNTime);
                intensity = Mathf.SmoothStep(_maxGlowIntensity, 0f, phase);
            }
        }
        SetGlow(intensity);

        // ── 투사체 발사 ─────────────────────────────────────────────
        if (info.IsName("Attack") && t >= _fireNormalizedTime && !_hasFired)
        {
            _hasFired = true;
            FireProjectile();
        }

        // ── 종료 ────────────────────────────────────────────────────
        if (!info.IsName("Attack") || t >= 0.95f)
        {
            IsAttacking = false;
            SetGlow(0f);
            if (info.IsName("Attack") && _weaponAnimator.HasState(0, Animator.StringToHash("Idle")))
                _weaponAnimator.Play("Idle", 0, 0f);
            return true;
        }
        return false;
    }

    public override void SetWeaponSprite(Sprite sprite)
    {
        if (_spriteRenderer != null && sprite != null)
            _spriteRenderer.sprite = sprite;
    }

    public override void OnDeactivated()
    {
        IsAttacking = false;
        _hasFired   = false;
        
        if (!_isInstantCastMode)
        {
            SetGlow(0f);
            if (_weaponAnimator != null)
            {
                _weaponAnimator.Play("Idle", 0, 0f);
                _weaponAnimator.Update(0f);
            }
        }
    }

    // ── 투사체 발사 ────────────────────────────────────────────────

    private void FireProjectile()
    {
        if (_projectilePrefab == null) return;

        // ── 마나 소모 체크 ───────────────────────────────────────────
        const float manaCost = 5f;
        if (_playerEntity != null && _playerEntity.Stats != null)
        {
            if (!_playerEntity.Stats.HasEnoughMana(manaCost))
            {
                Debug.Log($"[Wand] 마나 부족 (필요: {manaCost})");
                return;
            }
            _playerEntity.Stats.ConsumeMana(manaCost);
        }

        Vector2 fireDir  = GetCurrentCursorDirection();
        Vector3 spawnPos = _muzzlePoint != null ? _muzzlePoint.position : transform.position;

        // 최적화: 풀링 시스템에서 투사체를 가져옵니다.
        float statAtk = _playerEntity != null ? _playerEntity.TotalAtk : 0f;
        float damage  = DamageCalculator.CalcOutgoingDamage(statAtk, _projectileDamage);

        GameObject      proj = SimpleObjectPool.Instance.Get(_projectilePrefab, spawnPos, Quaternion.identity);
        MagicProjectile mp   = proj.GetComponent<MagicProjectile>();
        if (mp != null) mp.SetStats(_projectileSpeed, damage, fireDir);
    }

    private Vector2 GetCurrentCursorDirection()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return Vector2.right;

        float   camZ      = Mathf.Abs(_cam.transform.position.z - transform.position.z);
        Vector3 screenPos = Input.mousePosition;
        screenPos.z       = camZ;
        Vector3 worldPos  = _cam.ScreenToWorldPoint(screenPos);

        Vector2 dir = (Vector2)worldPos - (Vector2)transform.position;
        return dir.sqrMagnitude > 0.001f ? dir.normalized : Vector2.right;
    }

    // ── 글로우 ────────────────────────────────────────────────────
    // _glowSpriteRenderer가 있으면 그쪽에만, 없으면 메인 렌더러에 적용

    private void SetGlow(float intensity)
    {
        // 글로우 전용 렌더러 (별도 스프라이트)
        if (_glowSpriteRenderer != null)
        {
            _glowSpriteRenderer.GetPropertyBlock(_glowPropBlock);
            _glowPropBlock.SetColor(_glowColorId,     _glowColor);
            _glowPropBlock.SetFloat(_glowIntensityId, intensity);
            _glowSpriteRenderer.SetPropertyBlock(_glowPropBlock);
        }
        else if (_spriteRenderer != null)
        {
            // fallback: 메인 렌더러에 글로우 적용
            _spriteRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor(_glowColorId,     _glowColor);
            _propBlock.SetFloat(_glowIntensityId, intensity);
            _spriteRenderer.SetPropertyBlock(_propBlock);
        }
    }
}
