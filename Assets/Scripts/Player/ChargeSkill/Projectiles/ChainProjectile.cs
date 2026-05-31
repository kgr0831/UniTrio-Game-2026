using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 지팡이 2단계용 전이 투사체.
/// 적중 시 반경 3칸 내 미타격 적 3명에게 자식 투사체를 생성하여 전이합니다.
/// 같은 적에게 재전이 불가 (글로벌 히트 리스트).
/// </summary>
public class ChainProjectile : MonoBehaviour
{
    private float   _speed;
    private float   _damage;
    private Vector2 _direction;
    private Color   _elementColor;
    private int     _remainingChains;      // 남은 전이 횟수
    private int     _chainsPerHit = 3;     // 1회 전이 시 생성되는 투사체 수
    private float   _chainRadius = 3f;     // 전이 탐색 반경

    private Vector3 _spawnPos;
    private bool    _hit;

    // 글로벌 히트 리스트 (같은 전이 체인에서 같은 적 중복 방지)
    private HashSet<int> _globalHitList;

    private Sprite     _visual_sprite;
    private Material   _visual_material;
    private GameObject _explosionPrefab;
    private GameObject _damageTextPrefab;
    private Vector3    _projectileScale = Vector3.one;  // 프리팹 원본 스케일 (서브 체인에 전달)

    private static readonly Collider2D[] _searchResults = new Collider2D[32];

    public void SetStats(float speed, float damage, Vector2 direction, Color elementColor,
                         int remainingChains, int chainsPerHit, float chainRadius, HashSet<int> globalHitList)
    {
        _speed           = speed;
        _damage          = damage;
        _direction       = direction.normalized;
        _elementColor    = elementColor;
        _remainingChains = remainingChains;
        _chainsPerHit    = chainsPerHit;
        _chainRadius     = chainRadius;
        _globalHitList   = globalHitList ?? new HashSet<int>();
    }

    /// <summary>체인 전체에서 사용할 시각 + 폭발 VFX + 스케일 정보를 설정합니다.</summary>
    public void SetVisual(Sprite sprite, Material material, GameObject explosionPrefab = null,
                          GameObject damageTextPrefab = null, Vector3? projectileScale = null)
    {
        _visual_sprite    = sprite;
        _visual_material  = material;
        _explosionPrefab  = explosionPrefab;
        _damageTextPrefab = damageTextPrefab;
        _projectileScale  = projectileScale ?? Vector3.one;
    }

    private void OnEnable()
    {
        _spawnPos = transform.position;
        _hit = false;
        Invoke(nameof(DestroySelf), 3f);
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(DestroySelf));
    }

    private void FixedUpdate()
    {
        transform.Translate((Vector3)_direction * _speed * Time.fixedDeltaTime, Space.World);

        if (Vector3.Distance(_spawnPos, transform.position) >= 15f)
            DestroySelf();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (_hit) return;

        if (collision.CompareTag("Entity"))
        {
            IDamageable target = collision.GetComponentInParent<IDamageable>();
            if (target == null || !target.IsAlive) return;

            Component targetComp = target as Component;
            if (targetComp == null) return;

            int targetId = targetComp.gameObject.GetInstanceID();
            if (!_globalHitList.Add(targetId)) return; // 이미 맞은 적 → 무시

            _hit = true;
            target.TakeDamage(_damage, gameObject);
            DebuffApplier.ApplyFromProjectile(collision);
            HitEventManager.NotifyHit(_spawnPos, collision.bounds.center, true);

            // 전이
            if (_remainingChains > 0)
                SpawnChainProjectiles(collision.bounds.center);

            // 적중 VFX: 일반 지팡이 폭발 이펙트 우선 사용, 없으면 기본 VFX
            SpawnHitVfx(collision.bounds.center);

            DestroySelf();
        }
        else if (collision.CompareTag("Wall"))
        {
            DestroySelf();
        }
    }

    private void SpawnChainProjectiles(Vector3 hitPos)
    {
        // 반경 내 적 탐색
        int count = Physics2D.OverlapCircleNonAlloc((Vector2)hitPos, _chainRadius, _searchResults);
        int spawned = 0;

        for (int i = 0; i < count && spawned < _chainsPerHit; i++)
        {
            Collider2D col = _searchResults[i];
            if (!col.CompareTag("Entity")) continue;

            IDamageable target = col.GetComponentInParent<IDamageable>();
            if (target == null || !target.IsAlive) continue;

            Component targetComp = target as Component;
            if (targetComp == null) continue;

            int targetId = targetComp.gameObject.GetInstanceID();
            if (_globalHitList.Contains(targetId)) continue; // 이미 맞은 적 스킵

            Vector2 dir = ((Vector2)col.bounds.center - (Vector2)hitPos).normalized;
            SpawnSingleChain(hitPos, dir);
            spawned++;
        }
    }

    private void SpawnHitVfx(Vector3 position)
    {
        if (_explosionPrefab != null)
        {
            GameObject vfx = SimpleObjectPool.Instance.Get(_explosionPrefab, position, Quaternion.identity);
            ExplosionEffect fx = vfx.GetComponent<ExplosionEffect>();
            if (fx != null)
            {
                // damage=0: VFX만 재생, AoE 데미지 없음
                // (직접 타격 데미지는 OnTriggerEnter2D에서 이미 적용됨)
                fx.SetupExplosion(0f, null, 1f);
                fx.SetElementColor(_elementColor);
            }
        }
        else
        {
            ChargeSkillHelper.SpawnCircleSlashVFX(position, 1.5f, _elementColor, 0.3f);
        }
    }

    private void SpawnSingleChain(Vector3 origin, Vector2 direction)
    {
        GameObject chainObj = new GameObject("ChainProjectile_Sub");
        chainObj.transform.position = origin;
        // 원본 스케일 사용 (기존 Vector3.one * 0.8f 하드코딩 제거)
        chainObj.transform.localScale = _projectileScale;

        SpriteRenderer sr = chainObj.AddComponent<SpriteRenderer>();
        if (_visual_sprite != null)
        {
            sr.sprite = _visual_sprite;
            sr.material = new Material(_visual_material);
        }
        sr.sortingLayerName = "Weapons";
        sr.sortingOrder = 16;
        sr.color = _elementColor;

        CircleCollider2D col = chainObj.AddComponent<CircleCollider2D>();
        col.radius = 0.2f;
        col.isTrigger = true;

        Rigidbody2D rb = chainObj.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        ChainProjectile chain = chainObj.AddComponent<ChainProjectile>();
        chain.SetStats(_speed * 1.2f, _damage * 0.8f, direction, _elementColor,
                       _remainingChains - 1, _chainsPerHit, _chainRadius, _globalHitList);
        chain.SetVisual(_visual_sprite, _visual_material, _explosionPrefab, _damageTextPrefab, _projectileScale);
    }

    private void DestroySelf()
    {
        Destroy(gameObject);
    }
}
