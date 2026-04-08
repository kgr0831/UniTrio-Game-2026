using UnityEngine;

/// <summary>
/// 완드(Wand)에서 발사되는 마법 투사체.
///
/// 동작 방식:
/// 1. SetStats()로 이동 방향(world-space Vector2)을 주입합니다.
/// 2. FixedUpdate에서 World Space 기준으로 직진합니다.
/// 3. 비행 중 SpriteGlow 셰이더와 Light로 펄스 글로우 효과를 냅니다.
/// 4. Enemy와 충돌하면 HitVFX + ExplosionVFX를 스폰하고 소멸합니다.
/// </summary>
public class MagicProjectile : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float _speed    = 12f;
    [SerializeField] private float _lifeTime = 3f;
    [SerializeField] private float _maxDistance = 10f;
    
    private Vector3 _spawnPos;

    [Header("Damage")]
    [SerializeField] private float _damage = 15f;

    [Header("Hit VFX (Optional)")]
    [Tooltip("충돌 시 랜덤 재생할 HitVFX 프리팹 배열. HitVfxAutoReturn 컴포넌트가 부착되어 있어야 합니다.")]
    [SerializeField] private GameObject[] _hitVfxPrefabs;

    [Header("Explosion VFX")]
    [Tooltip("ExplosionEffect + CircleCollider2D + Rigidbody2D 가 붙은 프리팹.\n" +
             "CircleCollider2D의 radius가 곧 폭발 범위입니다.")]
    [SerializeField] private GameObject _explosionVfxPrefab;

    [Header("Damage Text (Optional)")]
    [Tooltip("데미지 수치 텍스트 프리팹. ExplosionEffect에 전달됩니다.")]
    [SerializeField] private GameObject _damageTextPrefab;

    [Header("Projectile Glow")]
    [Tooltip("SpriteGlow 셰이더를 사용하는 투사체 SpriteRenderer.\n비워두면 자동으로 GetComponent<SpriteRenderer>()를 사용합니다.")]
    [SerializeField] private SpriteRenderer _glowRenderer;
    [Tooltip("비행 중 글로우 색상 (HDR).")]
    [ColorUsage(true, true)]
    [SerializeField] private Color _glowColor = new Color(0.2f, 0.5f, 1f, 1f);
    [Tooltip("비행 중 글로우 강도. Bloom Threshold를 넘겨야 화면에 번집니다.")]
    [SerializeField] private float _glowIntensity = 15f;
    [Tooltip("글로우 펄스 속도 (초당 사이클 수).")]
    [SerializeField] private float _pulseCyclesPerSec = 2f;
    [Tooltip("투사체에 붙은 Light2D 또는 Point Light. 비워두면 자동으로 GetComponent<Light>()를 사용합니다.")]
    [SerializeField] private Light _light;
    [Tooltip("Light의 최대 강도.")]
    [SerializeField] private float _lightIntensity = 2f;

    // 이동 방향 (WandBehaviour가 주입)
    private Vector2 _moveDirection = Vector2.right;
    private bool    _exploded;

    private MaterialPropertyBlock _propBlock;
    private static readonly int   _glowIntensityId = Shader.PropertyToID("_GlowIntensity");
    private static readonly int   _glowColorId     = Shader.PropertyToID("_GlowColor");

    /// <summary>WandBehaviour에서 발사 시 호출. damage를 float으로 수신합니다.</summary>
    public void SetStats(float speed, float damage, Vector2 direction)
    {
        _speed         = speed;
        _damage        = damage;
        _moveDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
    }

    private void Awake()
    {
        _propBlock = new MaterialPropertyBlock();

        if (_glowRenderer == null) _glowRenderer = GetComponent<SpriteRenderer>();
        if (_light        == null) _light        = GetComponentInChildren<Light>();

        if (_glowRenderer != null)
        {
            _glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _glowRenderer.receiveShadows    = false;
        }
    }

    private void OnEnable()
    {
        _spawnPos = transform.position;
        _exploded = false;

        // 충돌 시 숨겼던 렌더러 및 빛 복원
        if (_glowRenderer != null) _glowRenderer.enabled = true;
        if (_light != null) _light.enabled = true;
        
        ApplyGlow(_glowIntensity);
        Invoke(nameof(ReturnToPool), _lifeTime);
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(ReturnToPool));
    }

    private void ReturnToPool()
    {
        if (gameObject.activeSelf)
            SimpleObjectPool.Instance.Release(gameObject);
    }

    private void Update()
    {
        float pulse = 0.7f + 0.3f * Mathf.Sin(Time.time * _pulseCyclesPerSec * Mathf.PI * 2f);
        ApplyGlow(_glowIntensity * pulse);
    }

    private void FixedUpdate()
    {
        transform.Translate((Vector3)_moveDirection * _speed * Time.fixedDeltaTime, Space.World);

        // 비거리 제한 체크 (10칸)
        if (Vector3.Distance(_spawnPos, transform.position) >= _maxDistance)
        {
            ReturnToPool();
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (_exploded) return;

        // 적, 벽, 또는 장애물에 충돌 시 폭발
        if (collision.CompareTag("Enemy") || collision.CompareTag("Wall") || collision.CompareTag("Obstacle"))
        {
            Explode(collision);
        }
    }

    private void Explode(Collider2D collision)
    {
        _exploded = true;
        ApplyGlow(0f);

        // 렌더러를 즉시 끄고 풀에 반환 → 시각적으로 충돌 순간 즉시 소멸
        if (_glowRenderer != null) _glowRenderer.enabled = false;
        if (_light != null) _light.enabled = false;

        // 적일 경우에만 히트 이펙트(피격 이펙트)를 추가로 생성
        if (collision.CompareTag("Enemy"))
        {
            SpawnHitVfx(collision);
        }

        // 공통 폭발 처리
        SpawnExplosion();

        SimpleObjectPool.Instance.Release(gameObject);
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

    private void SpawnExplosion()
    {
        if (_explosionVfxPrefab == null) return;

        GameObject     vfx = SimpleObjectPool.Instance.Get(_explosionVfxPrefab, transform.position, Quaternion.identity);
        ExplosionEffect fx  = vfx.GetComponent<ExplosionEffect>();
        if (fx != null)
            fx.SetupExplosion(_damage, _damageTextPrefab);
    }

    private void ApplyGlow(float intensity)
    {
        if (_glowRenderer != null)
        {
            _glowRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor(_glowColorId,     _glowColor);
            _propBlock.SetFloat(_glowIntensityId, intensity);
            _glowRenderer.SetPropertyBlock(_propBlock);
        }

        if (_light != null)
            _light.intensity = _lightIntensity * (intensity / Mathf.Max(0.001f, _glowIntensity));
    }
}
