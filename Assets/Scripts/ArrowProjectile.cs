using UnityEngine;

/// <summary>
/// 화살 등 직진하는 투사체를 처리하는 스크립트.
/// 활의 ArrowPos 트랜스폼 회전값을 그대로 받아 일방향(로컬 X축 기준 등)으로 나아갑니다.
/// </summary>
public class ArrowProjectile : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float _speed = 15f;
    [SerializeField] private float _lifeTime = 3f;
    [SerializeField] private Vector3 _moveDirection = Vector3.right; 

    [Header("Damage")]
    [SerializeField] private int _damage = 10;

    [Header("Hit VFX & Text (Optional)")]
    [SerializeField] private GameObject[] _hitVfxPrefabs;
    [SerializeField] private GameObject _damageTextPrefab;

    /// <summary>
    /// 활의 차징 비율에 따라 화살의 속도와 데미지를 덮어씁니다.
    /// Instantiate 직후 BowBehaviour에서 호출합니다.
    /// </summary>
    public void SetStats(float speed, int damage)
    {
        _speed  = speed;
        _damage = damage;
    }

    private void Start()
    {
        // 일정 시간 뒤 자동 소멸
        Destroy(gameObject, _lifeTime);
    }

    private void FixedUpdate()
    {
        // 로컬 방향(Space.Self)으로 전진
        // 생성될 때 ArrowPos의 rotation 값을 그대로 물려받았으므로
        // 로컬 방향으로만 이동하면 마우스 커서를 향한 한 방향으로 계속 나아갑니다.
        transform.Translate(_moveDirection * _speed * Time.fixedDeltaTime, Space.Self);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Enemy"))
        {
            Enemy enemy = collision.GetComponentInParent<Enemy>();
            if (enemy != null)
            {
                enemy.TakeDamage(_damage);

                SpawnHitVFX(collision);
                SpawnDamageText(collision, _damage);
            }
            
            // 적을 맞추면 화살 파괴. 관통형을 원하시면 이 부분을 지우면 됩니다.
            Destroy(gameObject);
        }
        else if (collision.tag == "Wall" || collision.tag == "Obstacle") // 지형지물 등에 부딪히면 
        {
            Destroy(gameObject);
        }
    }

    private void SpawnHitVFX(Collider2D enemyCollider)
    {
        if (_hitVfxPrefabs == null || _hitVfxPrefabs.Length == 0) return;

        Vector3 closestHitPoint = enemyCollider.ClosestPoint(transform.position);
        GameObject selectedPrefab = _hitVfxPrefabs[Random.Range(0, _hitVfxPrefabs.Length)];
        Quaternion randomRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        GameObject vfxObj = Instantiate(selectedPrefab, closestHitPoint, randomRotation);

        float autoLifetime = 0.5f;
        Animator anim = vfxObj.GetComponent<Animator>();
        if (anim != null)
        {
            anim.Update(0f); 
            autoLifetime = anim.GetCurrentAnimatorStateInfo(0).length;
        }
        Destroy(vfxObj, autoLifetime);
    }

    private void SpawnDamageText(Collider2D enemyCollider, int damageAmount)
    {
        if (_damageTextPrefab == null) return;
        Vector3 spawnPos = enemyCollider.bounds.center + Vector3.up * 0.5f;
        GameObject textObj = Instantiate(_damageTextPrefab, spawnPos, Quaternion.identity);
        DamageText dmgText = textObj.GetComponent<DamageText>();
        if (dmgText != null) dmgText.Setup(damageAmount);
    }
}
