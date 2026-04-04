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

    private void Start()
    {
        if (!_ready) _ready = true;

        // 범위 내 모든 Enemy에게 즉시 데미지
        ApplyAreaDamage();

        // 처음엔 최대 글로우
        SetGlow(_maxGlowIntensity);

        Destroy(gameObject, _duration);
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;
        // 폭발 글로우를 duration 동안 fade-out (cos ease-out)
        float progress = Mathf.Clamp01(_elapsed / _duration);
        float intensity = Mathf.Cos(progress * Mathf.PI * 0.5f) * _maxGlowIntensity;
        SetGlow(intensity);
    }

    // ── 범위 데미지 ────────────────────────────────────────────────

    private void ApplyAreaDamage()
    {
        float worldRadius = _circleCol.radius * Mathf.Max(
            Mathf.Abs(transform.lossyScale.x),
            Mathf.Abs(transform.lossyScale.y));

        Vector2    center = (Vector2)transform.position + _circleCol.offset;
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, worldRadius, _enemyLayer);

        foreach (Collider2D hit in hits)
        {
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
