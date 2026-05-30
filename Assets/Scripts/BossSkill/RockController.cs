using UnityEngine;

public class RockController : MonoBehaviour
{
    public float speed = 20.0f;
    public float lifetime = 5f;
    public float maxRange = 0f;       // >0이면 스폰 지점에서 이 거리만큼 이동 후 산산조각 (0=비활성, lifetime 폴백)
    public Quaternion moveRotation;
    public GameObject impactEffectPrefab;
    public float damage = 10f;        // 플레이어 적중 데미지
    public GameObject source;         // 데미지 출처(보스)

    [Header("Shatter")]
    public float shatterDuration = 0.5f;
    public int shatterCells = 5;

    private float _elapsed;
    private Vector3 _startPos;
    private SpriteRenderer _sr;

    void Start()
    {
        _startPos = transform.position;
        _sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        Vector3 direction = moveRotation * Vector3.up;
        direction.z = 0;
        transform.position += direction.normalized * speed * Time.deltaTime;

        // 최대 사거리 도달 시 산산조각
        if (maxRange > 0f && (transform.position - _startPos).sqrMagnitude >= maxRange * maxRange)
        {
            Shatter();
            return;
        }

        _elapsed += Time.deltaTime;
        if (_elapsed >= lifetime)
            Shatter();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            IDamageable target = other.GetComponentInParent<IDamageable>();
            if (target != null) target.TakeDamage(damage, source);
            Shatter();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag("Boss"))
        {
            Shatter();
        }
    }

    // 충돌/사거리 도달 시 여러 조각으로 비산하며 파괴된다.
    private void Shatter()
    {
        CameraShakeController.Instance?.Shake(0.15f, 0.25f);

        if (impactEffectPrefab != null)
        {
            GameObject fx = Object.Instantiate(impactEffectPrefab, transform.position, Quaternion.identity);
            Object.Destroy(fx, 2f);
        }

        // 쉐이더 기반 조각 비산 효과 생성
        if (_sr != null)
            ShatterEffect.Spawn(_sr, shatterDuration, shatterCells);

        Destroy(gameObject);
    }
}
