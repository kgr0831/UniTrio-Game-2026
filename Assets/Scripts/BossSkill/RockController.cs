using UnityEngine;

public class RockController : MonoBehaviour
{
    public float speed = 20.0f;
    public float lifetime = 5f;
    public float maxRange = 0f;       // >0이면 스폰 지점에서 이 거리만큼 이동 후 산산조각 (0=비활성, lifetime 폴백)
    public Quaternion moveRotation;
    public GameObject impactEffectPrefab;
    public float damage = 10f;        // 플레이어 적중 데미지
    public float knockback = 10f;     // 적중 시 진행 방향 넉백 세기 (약하게)
    public GameObject source;         // 데미지 출처(보스)

    [Header("Shatter")]
    public float shatterDuration = 0.5f;
    public int shatterCells = 5;

    private float _elapsed;
    private Vector3 _startPos;
    private SpriteRenderer _sr;
    private bool _shattered;

    void Start()
    {
        _startPos = transform.position;
        _sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        // 회피 저스트 카운터 중에는 투척 바위 정지
        if (MonsterFreezeManager.IsFrozen) return;

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
        // 골렘 돌은 경로상의 채집물을 한 방에 부수고 계속 날아간다 (source=보스 → GatherableNode 즉시 파괴)
        if (other.CompareTag("Gatherable"))
        {
            IDamageable gatherable = other.GetComponentInParent<IDamageable>();
            if (gatherable != null && gatherable.IsAlive) gatherable.TakeDamage(damage, source);
            return;
        }

        if (other.CompareTag("Player"))
        {
            IDamageable target = other.GetComponentInParent<IDamageable>();
            if (target != null) target.TakeDamage(damage, source);

            // 돌 진행 방향으로 약한 넉백
            var move = other.GetComponentInParent<PlayerMovement>();
            if (move != null)
            {
                Vector3 dir = moveRotation * Vector3.up;
                dir.z = 0f;
                if (dir.sqrMagnitude > 0.0001f)
                    move.ApplyRecoil((Vector2)dir.normalized * knockback);
            }

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
        // Destroy는 프레임 끝까지 지연되므로 같은 프레임에 trigger/collision/사거리가 겹치면
        // 중복 호출될 수 있다. 가드로 1회만 실행.
        if (_shattered) return;
        _shattered = true;

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
