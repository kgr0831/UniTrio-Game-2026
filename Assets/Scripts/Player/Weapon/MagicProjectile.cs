using UnityEngine;

/// <summary>
/// 완드(Wand)에서 발사되는 마법 투사체.
///
/// 동작 방식:
/// 1. SetStats()로 이동 방향(world-space Vector2)을 주입합니다.
/// 2. FixedUpdate에서 World Space 기준으로 직진합니다.
/// 3. 비행 중 SpriteGlow 셰이더와 Light로 펄스 글로우 효과를 냅니다.
/// 4. Enemy와 충돌하면 ExplosionVFX를 스폰하고 소멸합니다.
/// </summary>
public class MagicProjectile : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float _speed    = 12f;
    [SerializeField] private float _lifeTime = 3f;

    [Header("Damage")]
    [SerializeField] private float _damage = 15f;

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
        _exploded = false;
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
        float pulse     = 0.7f + 0.3f * Mathf.Sin(Time.time * _pulseCyclesPerSec * Mathf.PI * 2f);
        ApplyGlow(_glowIntensity * pulse);
    }

    private void FixedUpdate()
    {
        transform.Translate((Vector3)_moveDirection * _speed * Time.fixedDeltaTime, Space.World);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (_exploded) return;
        if (!collision.CompareTag("Enemy")) return;

        _exploded = true;
        ApplyGlow(0f);
        SpawnExplosion();

        SimpleObjectPool.Instance.Release(gameObject);
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
