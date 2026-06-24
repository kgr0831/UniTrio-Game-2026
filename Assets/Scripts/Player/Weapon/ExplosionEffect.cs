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
    [SerializeField] private SpriteRenderer _glowRenderer;
    [ColorUsage(true, true)]
    [SerializeField] private Color _glowColor = new Color(0.3f, 0.6f, 1f, 1f);
    [SerializeField] private float _maxGlowIntensity = 25f;
    [SerializeField] private Light _light;
    [SerializeField] private float _maxLightIntensity = 10f;

    // MagicProjectile 이 SetupExplosion() 으로 주입하는 값
    private float   _damage;
    private bool    _ready;
    private Vector3 _originalScale;   // Awake 시 프리팹 원본 스케일 저장

    private CircleCollider2D      _circleCol;
    private MaterialPropertyBlock _propBlock;
    private float                 _elapsed;

    private static readonly int _emissionIntensityId = Shader.PropertyToID("_EmissionIntensity");
    private static readonly int _emissionColorId    = Shader.PropertyToID("_EmissionColor");

    // 최적화: 물리 검출 시 가비지를 생성하지 않도록 정적 배열 사용 (ContactFilter2D + OverlapCircle 오버로드)
    private static readonly Collider2D[] _overlapResults = new Collider2D[100];
    private ContactFilter2D              _contactFilter;

    private void Awake()
    {
        _originalScale = transform.localScale;   // 프리팹 원본 스케일 저장
        _circleCol = GetComponent<CircleCollider2D>();
        _propBlock = new MaterialPropertyBlock();

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        rb.bodyType     = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        _circleCol.isTrigger = true;

        if (_glowRenderer == null) _glowRenderer = GetComponentInChildren<SpriteRenderer>();
        if (_light        == null) _light        = GetComponentInChildren<Light>();

        if (_glowRenderer != null)
        {
            _glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _glowRenderer.receiveShadows    = false;
        }

        // ContactFilter2D로 레이어 마스크를 필터링 (OverlapCircleNonAlloc 대체)
        _contactFilter = new ContactFilter2D();
        _contactFilter.SetLayerMask(_enemyLayer);
        _contactFilter.useTriggers = true;
    }

    /// <summary>
    /// MagicProjectile이 풀에서 꺼낸 직후 호출합니다.
    /// sizeMultiplier > 1 이면 폭발 범위(CircleCollider2D)를 원본 대비 해당 배율로 확대합니다.
    /// </summary>
    public void SetupExplosion(float damage, GameObject damageTextPrefab = null, float sizeMultiplier = 1f)
    {
        _damage           = damage;
        _damageTextPrefab = damageTextPrefab != null ? damageTextPrefab : _damageTextPrefab;
        _ready            = true;

        // 스케일을 먼저 적용한 뒤 데미지를 계산해야 CircleCollider2D 월드 반경이 정확해짐
        if (!Mathf.Approximately(sizeMultiplier, 1f))
            transform.localScale = _originalScale * sizeMultiplier;

        ApplyAreaDamage();
    }

    /// <summary>속성 색상을 주입합니다. 폭발 글로우에 반영됩니다.</summary>
    public void SetElementColor(Color hdrColor)
    {
        _glowColor = hdrColor;
    }

    private void OnEnable()
    {
        _elapsed = 0f;
        _ready   = false;
        // 풀 재사용 시 스케일 초기화 (이전 차징 스킬이 변경했을 수 있음)
        transform.localScale = _originalScale;
        SetGlow(_maxGlowIntensity);
        // ApplyAreaDamage는 SetupExplosion에서 스케일 설정 후 호출됩니다.
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;

        // 폭발 글로우 cos ease-out fade
        float progress  = Mathf.Clamp01(_elapsed / _duration);
        float intensity = Mathf.Cos(progress * Mathf.PI * 0.5f) * _maxGlowIntensity;
        SetGlow(intensity);

        if (_elapsed >= _duration)
            SimpleObjectPool.Instance.Release(gameObject);
    }

    private void ApplyAreaDamage()
    {
        if (_damage <= 0f) return;   // VFX 전용 호출(damage=0)이면 AoE 적용 안 함

        float worldRadius = _circleCol.radius * Mathf.Max(
            Mathf.Abs(transform.lossyScale.x),
            Mathf.Abs(transform.lossyScale.y));

        Vector2 center = (Vector2)transform.position + _circleCol.offset;

        // Physics2D.OverlapCircle (배열 오버로드): 가비지 없는 물리 검출
        int count = Physics2D.OverlapCircle(center, worldRadius, _contactFilter, _overlapResults);

        bool firstHit = true;
        for (int i = 0; i < count; i++)
        {
            Collider2D  hit    = _overlapResults[i];
            IDamageable target = hit.GetComponentInParent<IDamageable>();
            if (target == null || !target.IsAlive) continue;

            target.TakeDamage(_damage, gameObject);
            SpawnDamageText(hit.bounds.center);

            // 속성 디버프 적용
            DebuffApplier.ApplyFromProjectile(hit.gameObject);
            
            HitEventManager.NotifyHit(transform.position, hit.bounds.center, firstHit);
            firstHit = false;
        }
    }

    private void SpawnDamageText(Vector3 position)
    {
        if (_damageTextPrefab == null) return;
        Vector3    spawnPos = position + Vector3.up * 0.5f;
        
        // 최적화: Instantiate 대신 SimpleObjectPool에서 가져옵니다.
        GameObject textObj  = SimpleObjectPool.Instance.Get(_damageTextPrefab, spawnPos, Quaternion.identity);
        DamageText dmgText  = textObj.GetComponent<DamageText>();
        if (dmgText != null) dmgText.Setup(Mathf.RoundToInt(_damage));
    }

    private void SetGlow(float intensity)
    {
        if (_glowRenderer != null)
        {
            _glowRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor(_emissionColorId, _glowColor);
            _propBlock.SetFloat(_emissionIntensityId, intensity);
            _glowRenderer.SetPropertyBlock(_propBlock);
        }

        if (_light != null)
            _light.intensity = _maxLightIntensity * (intensity / Mathf.Max(0.001f, _maxGlowIntensity));
    }

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
