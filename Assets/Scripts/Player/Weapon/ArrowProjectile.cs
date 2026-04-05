using UnityEngine;

/// <summary>
/// 화살 등 직진하는 투사체 처리 스크립트.
/// BowBehaviour.FireArrow()가 SetStats()로 속도/데미지를 주입합니다.
/// IDamageable 인터페이스를 통해 Enemy 타입에 직접 의존하지 않습니다.
/// </summary>
public class ArrowProjectile : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float   _speed     = 15f;
    [SerializeField] private float   _lifeTime  = 3f;
    [SerializeField] private Vector3 _moveDirection = Vector3.right;

    [Header("Damage")]
    [SerializeField] private float _damage = 10f;

    [Header("Hit VFX & Text (Optional)")]
    [SerializeField] private GameObject[] _hitVfxPrefabs;
    [SerializeField] private GameObject   _damageTextPrefab;

    /// <summary>
    /// BowBehaviour에서 발사 시 차징 비율에 따른 속도/데미지를 주입합니다.
    /// </summary>
    public void SetStats(float speed, float damage)
    {
        _speed  = speed;
        _damage = damage;
    }

    private void Start()
    {
        Destroy(gameObject, _lifeTime);
    }

    private void FixedUpdate()
    {
        // 로컬 방향(Space.Self) 직진 – 생성 시 ArrowPos rotation을 물려받아 커서 방향으로 나아감
        transform.Translate(_moveDirection * _speed * Time.fixedDeltaTime, Space.Self);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Enemy"))
        {
            IDamageable target = collision.GetComponentInParent<IDamageable>();
            if (target != null && target.IsAlive)
            {
                target.TakeDamage(_damage, gameObject);
                SpawnHitVFX(collision);
                SpawnDamageText(collision);
            }
            Destroy(gameObject);
        }
        else if (collision.CompareTag("Wall") || collision.CompareTag("Obstacle"))
        {
            Destroy(gameObject);
        }
    }

    private void SpawnHitVFX(Collider2D enemyCollider)
    {
        if (_hitVfxPrefabs == null || _hitVfxPrefabs.Length == 0) return;

        Vector3    closestHitPoint = enemyCollider.ClosestPoint(transform.position);
        GameObject vfxObj          = Instantiate(
            _hitVfxPrefabs[Random.Range(0, _hitVfxPrefabs.Length)],
            closestHitPoint,
            Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));

        float autoLifetime = 0.5f;
        Animator anim = vfxObj.GetComponent<Animator>();
        if (anim != null)
        {
            anim.Update(0f);
            autoLifetime = anim.GetCurrentAnimatorStateInfo(0).length;
        }
        Destroy(vfxObj, autoLifetime);
    }

    private void SpawnDamageText(Collider2D enemyCollider)
    {
        if (_damageTextPrefab == null) return;
        Vector3    spawnPos = enemyCollider.bounds.center + Vector3.up * 0.5f;
        GameObject textObj  = Instantiate(_damageTextPrefab, spawnPos, Quaternion.identity);
        DamageText dmgText  = textObj.GetComponent<DamageText>();
        if (dmgText != null) dmgText.Setup(Mathf.RoundToInt(_damage));
    }
}
