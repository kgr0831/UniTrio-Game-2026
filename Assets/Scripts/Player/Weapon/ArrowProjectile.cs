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

        GameObject trailObj = new GameObject("ArrowTrailVFX");
        trailObj.transform.SetParent(transform);
        trailObj.transform.localPosition = new Vector3(-0.3f, 0, 0);
        trailObj.transform.localRotation = Quaternion.Euler(0, 180, 0);

        _trailParticle = trailObj.AddComponent<ParticleSystem>();
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

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Entity"))
        {
            IDamageable target = collision.GetComponentInParent<IDamageable>();
            if (target != null && target.IsAlive)
            {
                target.TakeDamage(_damage, gameObject);
                SpawnHitVfx(collision);
                SpawnDamageText(collision);
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
    private void SpawnHitVfx(Collider2D hitCollider)
    {
        if (_hitVfxPrefabs == null || _hitVfxPrefabs.Length == 0) return;

        Vector3 hitPoint = hitCollider.ClosestPoint(transform.position);
        Vector2 dir      = (Vector2)(hitPoint - transform.position);
        float   angleZ   = dir.sqrMagnitude > 0.0001f
                           ? Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg
                           : 0f;

        GameObject prefab = _hitVfxPrefabs[Random.Range(0, _hitVfxPrefabs.Length)];
        SimpleObjectPool.Instance.Get(prefab, hitPoint, Quaternion.Euler(0f, 0f, angleZ));
    }

    private void SpawnDamageText(Collider2D hitCollider)
    {
        if (_damageTextPrefab == null) return;
        Vector3    spawnPos = hitCollider.bounds.center + Vector3.up * 0.5f;
        
        // 최적화: Instantiate 대신 SimpleObjectPool에서 가져옵니다.
        GameObject textObj  = SimpleObjectPool.Instance.Get(_damageTextPrefab, spawnPos, Quaternion.identity);
        DamageText dmgText  = textObj.GetComponent<DamageText>();
        if (dmgText != null) dmgText.Setup(Mathf.RoundToInt(_damage));
    }
}
