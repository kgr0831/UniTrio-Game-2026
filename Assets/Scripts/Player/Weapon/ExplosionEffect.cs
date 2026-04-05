using UnityEngine;

/// <summary>
/// 폭발 VFX 프리팹에 붙는 스크립트.
///
/// 프리팹 구성:
///   - CircleCollider2D (isTrigger = true)  ← radius = 폭발 범위
///   - Rigidbody2D (Body Type = Kinematic)
///   - SpriteRenderer (Custom/SpriteGlow 셰이더)  ← 폭발 비주얼 + 글로우
///   - Animator 또는 ParticleSystem            ← 폭발 애니메이션
///   - ExplosionEffect (이 스크립트)
///   - Light (선택) ← 폭발 순간 화면 조명 연출
/// </summary>
[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class ExplosionEffect : MonoBehaviour
{
    [Header("Enemy Detection")]
    [Tooltip("Enemy 레이어 마스크. 이 레이어만 범위 데미지 대상이 됩니다.")]
    [SerializeField] private LayerMask _enemyLayer;

    [Header("Auto Destroy")]
    [Tooltip("스폰 후 자동 제거 시간 (초). Animator/Particle 길이에 맞춰 조절하세요.")]
    [SerializeField] private float _duration = 0.6f;

    [Header("Damage Text (Optional)")]
    [SerializeField] private GameObject _damageTextPrefab;

    [Header("Explosion Glow")]
    [Tooltip("폭발 비주얼 SpriteRenderer. Custom/SpriteGlow 셰이더 사용 권장.\n비워두면 GetComponentInChildren<SpriteRenderer>()로 자동 검색합니다.")]
    [SerializeField] private SpriteRenderer _glowRenderer;
    [Tooltip("폭발 글로우 색상 (HDR).")]
    [ColorUsage(true, true)]
    [SerializeField] private Color _glowColor = new Color(0.3f, 0.6f, 1f, 1f);
    [Tooltip("폭발 순간 최대 글로우 강도.")]
    [SerializeField] private float _maxGlowIntensity = 25f;
    [Tooltip("폭발에 붙은 Light. 비워두면 GetComponentInChildren<Light>()로 자동 검색합니다.")]
    [SerializeField] private Light _light;
    [Tooltip("Light 최대 강도.")]
    [SerializeField] private float _maxLightIntensity = 10f;

    // MagicProjectile 이 SetupExplosion() 으로 주입하는 값
    private int        _damage;
    private bool       _ready;

    private CircleCollider2D      _circleCol;
    private MaterialPropertyBlock _propBlock;
    private float                 _elapsed;

    private static readonly int _glowIntensityId = Shader.PropertyToID("_GlowIntensity");
    private static readonly int _glowColorId     = Shader.PropertyToID("_GlowColor");

    // 최적화: 물리 검출 시 가비지를 생성하지 않도록 정적 배열을 사용합니다.
    private static readonly Collider2D[] _overlapResults = new Collider2D[100];

    private void Awake()
    {
        _circleCol = GetComponent<CircleCollider2D>();
        _propBlock = new MaterialPropertyBlock();

        // Rigidbody2D 강제 설정
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        rb.bodyType     = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        _circleCol.isTrigger = true;

        if (_glowRenderer == null) _glowRenderer = GetComponentInChildren<SpriteRenderer>();
        if (_light        == null) _light        = GetComponentInChildren<Light>();

        // 렌더링 최적화: 폭발 이펙트는 그림자를 계산하지 않도록 강제 설정
        if (_glowRenderer != null)
        {
            _glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _glowRenderer.receiveShadows    = false;
        }
    }

    /// <summary>
    /// MagicProjectile이 Instantiate 직후 호출합니다.
    /// </summary>
    public void SetupExplosion(int damage, GameObject damageTextPrefab = null)
    {
        _damage           = damage;
        _damageTextPrefab = damageTextPrefab != null ? damageTextPrefab : _damageTextPrefab;
        _ready            = true;
    }

    private void OnEnable()
    {
        _elapsed = 0f;
        if (!_ready) _ready = true;

        // 범위 내 모든 Enemy에게 즉시 데미지
        ApplyAreaDamage();

        // 처음엔 최대 글로우
        SetGlow(_maxGlowIntensity);
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;
        
        // 폭발 글로우를 duration 동안 fade-out (cos ease-out)
        float progress = Mathf.Clamp01(_elapsed / _duration);
        float intensity = Mathf.Cos(progress * Mathf.PI * 0.5f) * _maxGlowIntensity;
        SetGlow(intensity);

        // 시간 다 되면 풀로 반환 (Destroy 대체)
        if (_elapsed >= _duration)
        {
            gameObject.SetActive(false);
            // 메모: 프리팹을 알고 있어야 Release가 가능하므로 
            // 실제 구현에서는 투사체가 이를 관리하거나 SimpleObjectPool을 더 확장해야 할 수 있습니다.
            // 여기서는 단순하게 비활성화만 하고 투사체 쪽에서 관리하도록 할 수 있습니다.
        }
    }

    // ── 범위 데미지 ────────────────────────────────────────────────

    private void ApplyAreaDamage()
    {
        float worldRadius = _circleCol.radius * Mathf.Max(
            Mathf.Abs(transform.lossyScale.x),
            Mathf.Abs(transform.lossyScale.y));

        Vector2 center = (Vector2)transform.position + _circleCol.offset;
        
        // 최적화: OverlapCircleNonAlloc 사용 (Garbage Free)
        int count = Physics2D.OverlapCircleNonAlloc(center, worldRadius, _overlapResults, _enemyLayer);

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = _overlapResults[i];
            Enemy enemy = hit.GetComponentInParent<Enemy>();
            if (enemy == null) continue;
            enemy.TakeDamage(_damage);
            SpawnDamageText(hit.bounds.center);
        }
    }

    private void SpawnDamageText(Vector3 position)
    {
        if (_damageTextPrefab == null) return;
        Vector3    spawnPos = position + Vector3.up * 0.5f;
        GameObject textObj  = Instantiate(_damageTextPrefab, spawnPos, Quaternion.identity);
        DamageText dmgText  = textObj.GetComponent<DamageText>();
        if (dmgText != null) dmgText.Setup(_damage);
    }

    // ── 글로우 적용 ────────────────────────────────────────────────

    private void SetGlow(float intensity)
    {
        if (_glowRenderer != null)
        {
            _glowRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor(_glowColorId,     _glowColor);
            _propBlock.SetFloat(_glowIntensityId, intensity);
            _glowRenderer.SetPropertyBlock(_propBlock);
        }

        if (_light != null)
            _light.intensity = _maxLightIntensity * (intensity / Mathf.Max(0.001f, _maxGlowIntensity));
    }

    // ── 에디터 시각화 ──────────────────────────────────────────────
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        CircleCollider2D col = GetComponent<CircleCollider2D>();
        if (col == null) return;

        Vector2 center = (Vector2)transform.position + col.offset;
        float   radius = col.radius * Mathf.Max(
            Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));

        UnityEditor.Handles.color = new Color(1f, 0.4f, 0f, 0.3f);
        UnityEditor.Handles.DrawSolidDisc(center, Vector3.back, radius);
        UnityEditor.Handles.color = new Color(1f, 0.4f, 0f, 1f);
        UnityEditor.Handles.DrawWireDisc(center, Vector3.back, radius);
    }
#endif
}
