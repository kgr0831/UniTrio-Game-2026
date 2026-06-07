using UnityEngine;

/// <summary>
/// 네크로맨서 Spell1 투사체(파이어볼).
/// 생성 시 플레이어 방향으로 회전되어 자기 로컬 +X로 직진하며(화살과 동일 구조),
/// 화살과 같은 최대 사거리까지 날아간다. 플레이어에 충돌하면 데미지를 주고 사라진다.
/// SimpleObjectPool로 풀링된다.
/// </summary>
public sealed class NecromancerFireball : MonoBehaviour
{
    [Header("Movement (화살과 동일 기본값)")]
    [SerializeField] private float _speed       = 15f;
    [SerializeField] private float _maxDistance = 10f;
    [SerializeField] private float _lifeTime    = 3f;

    [Header("Damage")]
    [SerializeField] private float _damage = 20f;

    private Vector3    _spawnPos;
    private GameObject _owner;

    /// <summary>발사 시 속도/데미지/사거리를 주입한다.</summary>
    public void SetStats(float speed, float damage, float maxDistance)
    {
        _speed       = speed;
        _damage      = damage;
        _maxDistance = maxDistance;
    }

    /// <summary>데미지 출처(네크로맨서)를 지정한다.</summary>
    public void SetOwner(GameObject owner) => _owner = owner;

    private void OnEnable()
    {
        _spawnPos = transform.position;
        CancelInvoke(nameof(ReturnToPool));
        Invoke(nameof(ReturnToPool), _lifeTime);
    }

    private void OnDisable() => CancelInvoke(nameof(ReturnToPool));

    private void FixedUpdate()
    {
        // 생성 시 플레이어 방향으로 회전돼 있으므로 로컬 +X로 직진
        transform.Translate(Vector3.right * (_speed * Time.fixedDeltaTime), Space.Self);

        if (Vector3.Distance(_spawnPos, transform.position) >= _maxDistance)
            ReturnToPool();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            var target = other.GetComponentInParent<IDamageable>();
            if (target != null && target.IsAlive)
                target.TakeDamage(_damage, _owner != null ? _owner : gameObject);
            ReturnToPool();
        }
        else if (other.CompareTag("Wall") || other.CompareTag("Obstacle"))
        {
            ReturnToPool();
        }
    }

    private void ReturnToPool()
    {
        if (gameObject.activeSelf && SimpleObjectPool.Instance != null)
            SimpleObjectPool.Instance.Release(gameObject);
    }
}
