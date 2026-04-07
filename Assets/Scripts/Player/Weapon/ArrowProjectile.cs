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

    /// <summary>BowBehaviour에서 발사 시 차징 비율에 따른 속도/데미지를 주입합니다.</summary>
    public void SetStats(float speed, float damage)
    {
        _speed  = speed;
        _damage = damage;
    }

    private void Start()
    {
        _spawnPos = transform.position;
        Destroy(gameObject, _lifeTime);
    }

    private void FixedUpdate()
    {
        // 로컬 방향(Space.Self) 직진 – 생성 시 ArrowPos rotation을 물려받아 커서 방향으로 나아감
        transform.Translate(_moveDirection * _speed * Time.fixedDeltaTime, Space.Self);

        // 비거리 제한 체크 (10칸 이상 시 소멸)
        if (Vector3.Distance(_spawnPos, transform.position) >= _maxDistance)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Enemy"))
        {
            IDamageable target = collision.GetComponentInParent<IDamageable>();
            if (target != null && target.IsAlive)
            {
                target.TakeDamage(_damage, gameObject);
                SpawnHitVfx(collision);
                SpawnDamageText(collision);
            }
            Destroy(gameObject);
        }
        else if (collision.CompareTag("Wall") || collision.CompareTag("Obstacle"))
        {
            Destroy(gameObject);
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
        GameObject textObj  = Instantiate(_damageTextPrefab, spawnPos, Quaternion.identity);
        DamageText dmgText  = textObj.GetComponent<DamageText>();
        if (dmgText != null) dmgText.Setup(Mathf.RoundToInt(_damage));
    }
}
