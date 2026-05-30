using UnityEngine;

/// <summary>
/// 차징 스킬용 범용 투사체.
/// 적 또는 벽에 충돌 시 폭발(범위 데미지)을 발생시킵니다.
/// MagicProjectile과 유사하지만 폭발 범위 배율, 색상을 외부에서 주입받습니다.
/// </summary>
public class ChargeProjectile : MonoBehaviour
{
    private float   _speed;
    private float   _damage;
    private Vector2 _direction;
    private float   _explosionRangeMultiplier;
    private Color   _elementColor;
    private float   _maxDistance = 15f;
    private float   _baseExplosionRadius = 1.0f;

    private Vector3 _spawnPos;
    private bool    _exploded;

    public void SetStats(float speed, float damage, Vector2 direction, float explosionRangeMult, Color elementColor)
    {
        _speed                    = speed;
        _damage                   = damage;
        _direction                = direction.normalized;
        _explosionRangeMultiplier = explosionRangeMult;
        _elementColor             = elementColor;
    }

    private void OnEnable()
    {
        _spawnPos = transform.position;
        _exploded = false;
        Invoke(nameof(DestroySelf), 3f);
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(DestroySelf));
    }

    private void FixedUpdate()
    {
        transform.Translate((Vector3)_direction * _speed * Time.fixedDeltaTime, Space.World);

        if (Vector3.Distance(_spawnPos, transform.position) >= _maxDistance)
            DestroySelf();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (_exploded) return;

        if (collision.CompareTag("Entity") || collision.CompareTag("Wall") || collision.CompareTag("Obstacle"))
        {
            Explode();
        }
    }

    private void Explode()
    {
        _exploded = true;

        float radius = _baseExplosionRadius * _explosionRangeMultiplier;
        Vector2 center = (Vector2)transform.position;

        // 범위 데미지
        int hitCount = ChargeSkillHelper.ApplyAreaDamage(center, radius, _damage, gameObject);

        // 폭발 VFX
        ChargeSkillHelper.SpawnCircleSlashVFX(transform.position, radius, _elementColor, 0.4f);

        if (CameraShakeController.Instance != null)
            CameraShakeController.Instance.Shake(0.12f, 0.15f);
        if (HitStopManager.Instance != null && hitCount > 0)
            HitStopManager.Instance.TriggerHitStop(0.05f);

        DestroySelf();
    }

    private void DestroySelf()
    {
        Destroy(gameObject);
    }
}
