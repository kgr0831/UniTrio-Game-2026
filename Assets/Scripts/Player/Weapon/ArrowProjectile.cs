using UnityEngine;

/// <summary>
/// 화살 등 직진하는 투사체 처리 스크립트.
/// BowBehaviour.FireArrow()가 SetStats()로 속도/데미지를 주입합니다.
/// IDamageable 인터페이스를 통해 Enemy 타입에 직접 의존하지 않습니다.
/// </summary>
public class ArrowProjectile : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float   _speed         = 15f;
    [SerializeField] private float   _lifeTime      = 3f;
    [SerializeField] private Vector3 _moveDirection = Vector3.right;
    [SerializeField] private float   _maxDistance   = 10f;

    private Vector3 _spawnPos;

    [Header("Damage")]
    [SerializeField] private float _damage = 10f;

    [Header("Hit VFX (Optional)")]
    [Tooltip("충돌 시 랜덤 재생할 HitVFX 프리팹 배열. HitVfxAutoReturn 컴포넌트가 부착되어 있어야 합니다.")]
    [SerializeField] private GameObject[] _hitVfxPrefabs;

    [Header("Damage Text (Optional)")]
    [SerializeField] private GameObject _damageTextPrefab;

    /// <summary>차징 스킬 투사체(PiercingArrowProjectile)가 동일한 타격 이펙트를 재사용할 때 참조합니다.</summary>
    public GameObject[] HitVfxPrefabs    => _hitVfxPrefabs;
    /// <summary>차징 스킬 투사체가 동일한 데미지 텍스트 프리팹을 재사용할 때 참조합니다.</summary>
    public GameObject   DamageTextPrefab => _damageTextPrefab;

    private SpriteRenderer _spriteRenderer;
    private ParticleSystem _trailParticle;
    private Color          _elementColor;
    private bool           _hasElementColor;

    /// <summary>BowBehaviour에서 발사 시 차징 비율에 따른 속도/데미지를 주입합니다.</summary>
    public void SetStats(float speed, float damage)
    {
        _speed  = speed;
        _damage = damage;
    }

    /// <summary>속성 색상을 주입합니다. 화살 스프라이트 틴트와 궤적 파티클에 반영됩니다.</summary>
    public void SetElementColor(Color hdrColor)
    {
        _elementColor    = hdrColor;
        _hasElementColor = true;

        if (_spriteRenderer != null)
        {
            Color tint = hdrColor;
            tint.a = 1f;
            _spriteRenderer.color = tint;

            // SpriteGlow 머티리얼이 적용된 경우 발광 색상도 속성 색상으로 동기화
            var mat = _spriteRenderer.material;
            if (mat != null && mat.HasProperty("_GlowColor"))
                mat.SetColor("_GlowColor", hdrColor * 2f);
        }

        if (_trailParticle != null)
        {
            var main = _trailParticle.main;
            Color trailCol = hdrColor;
            trailCol.a = 0.6f;
            main.startColor = trailCol;

            var psRenderer = _trailParticle.GetComponent<ParticleSystemRenderer>();
            if (psRenderer != null && psRenderer.material != null)
            {
                psRenderer.material.SetColor("_EmissionColor", hdrColor);
            }
        }
    }

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();

        // 다른 마법 무기들과 동일한 SpriteGlow 셰이더 적용 (에너지 발광 느낌)
        ApplySpriteGlow();

        GameObject trailObj = new GameObject("ArrowTrailVFX");
        trailObj.transform.SetParent(transform);
        trailObj.transform.localPosition = new Vector3(-0.3f, 0, 0);
        trailObj.transform.localRotation = Quaternion.Euler(0, 180, 0);

        _trailParticle = trailObj.AddComponent<ParticleSystem>();
        _trailParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = _trailParticle.main;
        main.duration = 1f;
        main.startLifetime = 0.15f;
        main.startSpeed = 0.5f;
        main.startSize = 0.12f;
        main.startColor = new Color(1f, 1f, 1f, 0.5f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;

        var emission = _trailParticle.emission;
        emission.rateOverTime = 0f;

        var shape = _trailParticle.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 15f;
        shape.radius = 0.03f;

        var psRenderer = _trailParticle.GetComponent<ParticleSystemRenderer>();
        Material trailMat = new Material(Shader.Find("Custom/VFXLit2D"));
        trailMat.SetFloat("_EmissionIntensity", 3f);
        trailMat.SetColor("_EmissionColor", Color.white);
        trailMat.SetFloat("_LightInfluence", 0.3f);
        psRenderer.material = trailMat;
        psRenderer.sortingLayerName = "Weapons";
        psRenderer.sortingOrder = 9;
    }

    private void OnEnable()
    {
        _spawnPos = transform.position;
        // 풀링 사용 시 이전의 Destroy 타이머가 남아있을 수 있으므로 CancelInvoke를 권장하지만,
        // 여기서는 Invoke/Cancel 대신 코루틴이나 타이머 변수 방식을 고려할 수도 있습니다.
        // 하지만 기존 구조를 최대한 유지하면서 풀 반환으로만 바꿉니다.
        CancelInvoke(nameof(ReturnToPool));
        Invoke(nameof(ReturnToPool), _lifeTime);

        if (_trailParticle != null)
        {
            var em = _trailParticle.emission;
            em.rateOverTime = 40f;
            _trailParticle.Play();
        }

        if (_hasElementColor)
            SetElementColor(_elementColor);
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(ReturnToPool));
        if (_trailParticle != null)
        {
            _trailParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        if (_spriteRenderer != null)
            _spriteRenderer.color = Color.white;
    }

    private void ReturnToPool()
    {
        if (gameObject.activeSelf)
            SimpleObjectPool.Instance.Release(gameObject);
    }

    private void FixedUpdate()
    {
        // 로컬 방향(Space.Self) 직진 – 생성 시 ArrowPos rotation을 물려받아 커서 방향으로 나아감
        transform.Translate(_moveDirection * _speed * Time.fixedDeltaTime, Space.Self);

        // 비거리 제한 체크 (10칸 이상 시 소멸)
        if (Vector3.Distance(_spawnPos, transform.position) >= _maxDistance)
        {
            ReturnToPool();
        }
    }

    private void ApplySpriteGlow()
    {
        if (_spriteRenderer == null) return;

        Shader glowShader = Shader.Find("Custom/SpriteGlow");
        if (glowShader == null) return;

        Material glowMat = new Material(glowShader);
        glowMat.EnableKeyword("_USE_OUTLINE_GLOW");
        glowMat.SetColor("_GlowColor",     new Color(1f, 0.9f, 0.4f, 1f) * 2f); // 기본 황금 발광
        glowMat.SetFloat("_GlowIntensity", 2.5f);
        glowMat.SetFloat("_OutlineWidth",  1.5f);
        glowMat.SetFloat("_InteriorAlpha", 0.95f);
        _spriteRenderer.material = glowMat;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Entity"))
        {
            IDamageable target = collision.GetComponentInParent<IDamageable>();
            if (target != null && target.IsAlive)
            {
                Vector3 targetPosition = collision.transform.position;
                Vector3 hitPoint = collision.ClosestPoint(transform.position);

                target.TakeDamage(_damage, gameObject);
                SpawnHitVfx(hitPoint);
                SpawnDamageText(collision.bounds.center);

                // 속성 디버프 적용
                DebuffApplier.ApplyFromProjectile(collision);

                HitEventManager.NotifyHit(_spawnPos, targetPosition, true);

                // 피격 사운드 재생
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayHit();
            }
            ReturnToPool();
        }
        else if (collision.CompareTag("Gatherable"))
        {
            IDamageable g = collision.GetComponentInParent<IDamageable>();
            if (g != null && g.IsAlive)
            {
                Vector3 hitPoint = collision.ClosestPoint(transform.position);
                g.TakeDamage(_damage, gameObject);
                SpawnHitVfx(hitPoint);
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayHit();
            }
            ReturnToPool();
        }
        else if (collision.CompareTag("Wall") || collision.CompareTag("Obstacle"))
        {
            ReturnToPool();
        }
    }

    /// <summary>
    /// 충돌 콜라이더의 ClosestPoint에 HitVFX를 풀링 생성합니다.
    /// Rotation Z = 투사체 위치 → 충돌 지점 방향각.
    /// </summary>
    private void SpawnHitVfx(Vector3 hitPoint)
    {
        if (_hitVfxPrefabs == null || _hitVfxPrefabs.Length == 0) return;

        Vector2 dir      = (Vector2)(hitPoint - transform.position);
        float   angleZ   = dir.sqrMagnitude > 0.0001f
                           ? Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg
                           : 0f;

        GameObject prefab = _hitVfxPrefabs[Random.Range(0, _hitVfxPrefabs.Length)];
        SimpleObjectPool.Instance.Get(prefab, hitPoint, Quaternion.Euler(0f, 0f, angleZ));
    }

    private void SpawnDamageText(Vector3 enemyCenter)
    {
        if (_damageTextPrefab == null) return;
        Vector3    spawnPos = enemyCenter + Vector3.up * 0.5f;
        
        // 최적화: Instantiate 대신 SimpleObjectPool에서 가져옵니다.
        GameObject textObj  = SimpleObjectPool.Instance.Get(_damageTextPrefab, spawnPos, Quaternion.identity);
        DamageText dmgText  = textObj.GetComponent<DamageText>();
        if (dmgText != null) dmgText.Setup(Mathf.RoundToInt(_damage));
    }
}
