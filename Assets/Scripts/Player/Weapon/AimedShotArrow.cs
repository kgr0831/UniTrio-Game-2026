using UnityEngine;

/// <summary>
/// 조준 사격(Aimed Shot) 스킬의 관통 투사체.
/// 적을 관통하며 30칸 비거리를 갖고, 25칸부터 alpha가 감소하여 30칸에서 소멸합니다.
/// ArrowProjectile과 달리 적 충돌 시 파괴되지 않습니다.
/// 
/// 생성 시 _launched = false 상태로 ArrowPos에 부착되어 대기하다가,
/// Launch() 호출 시 분리되어 비행을 시작합니다.
/// </summary>
public class AimedShotArrow : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float   _speed            = 20f;
    [SerializeField] private float   _maxDistance       = 30f;   // 30칸 비거리
    [SerializeField] private float   _fadeStartDistance = 25f;   // 25칸부터 페이드 시작
    [SerializeField] private Vector3 _moveDirection     = Vector3.right;

    [Header("Damage")]
    [SerializeField] private float _damage = 10f;

    [Header("Hit VFX (Optional)")]
    [Tooltip("충돌 시 랜덤 재생할 HitVFX 프리팹 배열.")]
    [SerializeField] private GameObject[] _hitVfxPrefabs;

    [Header("Damage Text (Optional)")]
    [SerializeField] private GameObject _damageTextPrefab;

    private Vector3        _spawnPos;
    private SpriteRenderer _spriteRenderer;
    private bool           _launched;        // 발사 여부 (false = ArrowPos에 대기 중)
    private Collider2D     _collider;
    private Rigidbody2D    _rb;
    private ParticleSystem _trailParticle;

    private MaterialPropertyBlock _propBlock;
    private static readonly int  _glowIntensityId = Shader.PropertyToID("_EmissionIntensity");
    private static readonly int  _glowColorId     = Shader.PropertyToID("_EmissionColor");

    /// <summary>BowBehaviour에서 발사 시 속도/데미지를 주입합니다.</summary>
    public void SetStats(float speed, float damage)
    {
        _speed  = speed;
        _damage = damage;
    }

    private Color _elementColor;
    private bool  _hasElementColor;

    /// <summary>속성 색상을 주입합니다. 궤적 파티클과 글로우에 반영됩니다.</summary>
    public void SetElementColor(Color hdrColor)
    {
        _elementColor    = hdrColor;
        _hasElementColor = true;

        if (_trailParticle != null)
        {
            var main = _trailParticle.main;
            Color trailCol = hdrColor;
            trailCol.a = 0.7f;
            main.startColor = trailCol;

            var psRenderer = _trailParticle.GetComponent<ParticleSystemRenderer>();
            if (psRenderer != null && psRenderer.material != null)
            {
                psRenderer.material.SetColor("_EmissionColor", hdrColor);
            }
        }
    }

    /// <summary>Emission 강도를 설정합니다 (차징 비례).</summary>
    public void SetGlowIntensity(float intensity, Color glowColor)
    {
        if (_spriteRenderer == null) return;
        if (_propBlock == null) _propBlock = new MaterialPropertyBlock();

        _spriteRenderer.GetPropertyBlock(_propBlock);
        _propBlock.SetColor(_glowColorId, glowColor);
        _propBlock.SetFloat(_glowIntensityId, intensity);
        _spriteRenderer.SetPropertyBlock(_propBlock);
    }

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _collider       = GetComponent<Collider2D>();
        _rb             = GetComponent<Rigidbody2D>();
        _propBlock      = new MaterialPropertyBlock();

        // 생성 직후에는 대기 상태: 충돌/물리 비활성화 → ArrowPos에 안정적으로 부착
        if (_collider != null) _collider.enabled = false;
        if (_rb != null) _rb.simulated = false;
        _launched = false;

        // ── 궤적 파티클 생성 ──
        GameObject trailObj = new GameObject("AimedShotTrailVFX");
        trailObj.transform.SetParent(transform);
        // 화살의 뒷부분(로컬 X 음수 방향 = 왼쪽)
        trailObj.transform.localPosition = new Vector3(-0.4f, 0, 0); 
        trailObj.transform.localRotation = Quaternion.Euler(0, 180, 0); // 뒤를 향해 쏜다

        _trailParticle = trailObj.AddComponent<ParticleSystem>();
        _trailParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = _trailParticle.main;
        main.duration = 1f;
        main.startLifetime = 0.2f;
        main.startSpeed = 1f;
        main.startSize = 0.25f;
        main.startColor = new Color(1f, 1f, 1f, 0.7f); // 기본 투명도
        main.simulationSpace = ParticleSystemSimulationSpace.World; // 월드 좌표계에 뿌려짐
        main.playOnAwake = false;

        var emission = _trailParticle.emission;
        emission.rateOverTime = 0f; // 비행 전엔 안 나옴

        var shape = _trailParticle.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 20f;
        shape.radius = 0.05f;

        var psRenderer = _trailParticle.GetComponent<ParticleSystemRenderer>();
        // 강한 보라빛 글로우 머티리얼 적용
        Material trailMat = new Material(Shader.Find("Custom/VFXLit2D"));
        trailMat.SetFloat("_EmissionIntensity", 4f);
        trailMat.SetColor("_EmissionColor", new Color(0.7f, 0f, 1f, 1f));
        trailMat.SetFloat("_LightInfluence", 0.3f);
        psRenderer.material = trailMat;
        psRenderer.sortingLayerName = "Weapons";
        psRenderer.sortingOrder = 9;
    }

    /// <summary>
    /// 화살을 ArrowPos에서 분리하여 발사합니다.
    /// </summary>
    public void Launch()
    {
        _launched = true;
        _spawnPos = transform.position;

        // 부모에서 분리
        transform.SetParent(null);

        // 충돌/물리 활성화
        if (_collider != null) _collider.enabled = true;
        if (_rb != null) _rb.simulated = true;

        // alpha를 1로 초기화
        if (_spriteRenderer != null)
        {
            Color c = _spriteRenderer.color;
            c.a = 1f;
            _spriteRenderer.color = c;
        }

        // 궤적 파티클 발사
        if (_trailParticle != null)
        {
            var em = _trailParticle.emission;
            em.rateOverTime = 80f;
            _trailParticle.Play();
        }
    }

    private void FixedUpdate()
    {
        if (!_launched) return;

        // 로컬 방향(Space.Self) 직진 – ArrowPos rotation을 물려받아 커서 방향으로 나아감
        transform.Translate(_moveDirection * _speed * Time.fixedDeltaTime, Space.Self);

        float distance = Vector3.Distance(_spawnPos, transform.position);

        // 25칸부터 30칸까지 alpha 감소
        if (distance >= _fadeStartDistance && _spriteRenderer != null)
        {
            float fadeRatio = (distance - _fadeStartDistance) / (_maxDistance - _fadeStartDistance);
            Color c = _spriteRenderer.color;
            c.a = 1f - Mathf.Clamp01(fadeRatio);
            _spriteRenderer.color = c;
        }

        // 30칸 도달 시 소멸
        if (distance >= _maxDistance)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision == null || collision.gameObject == null || !collision.gameObject.activeInHierarchy) return;

        if (!_launched) return;

        if (collision.CompareTag("Enemy"))
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
            }
            // 관통: 적 충돌 시 파괴하지 않음
        }
        // 벽/장애물도 관통 (스킬 투사체 특성)
    }

    /// <summary>충돌 지점에 HitVFX를 풀링 생성합니다.</summary>
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
        GameObject textObj  = SimpleObjectPool.Instance.Get(_damageTextPrefab, spawnPos, Quaternion.identity);
        DamageText dmgText  = textObj.GetComponent<DamageText>();
        if (dmgText != null) dmgText.Setup(Mathf.RoundToInt(_damage));
    }
}
