using UnityEngine;

/// <summary>
/// 검(및 창)의 히트박스 판정 전담 스크립트.
/// IDamageable 인터페이스를 통해 적에게 데미지를 주며,
/// PlayerEntity에서 공격력을 가져와 DamageCalculator 공식으로 최종 데미지를 산출합니다.
///
/// 공격 공식: (PlayerEntity.TotalAtk + _baseDamage) * 1.0
/// </summary>
public class SwordHitbox : MonoBehaviour
{
    [Header("Stats")]
    [Tooltip("Player 루트 오브젝트의 PlayerEntity 컴포넌트를 인스펙터에서 연결하세요.")]
    [SerializeField] private PlayerEntity _playerEntity;
    [Tooltip("이 무기 고유의 기본 데미지 (무기 스탯). WeaponData 도입 전 임시값.")]
    [SerializeField] private float _baseDamage = 5f;

    [Header("Hit VFX")]
    [SerializeField] private GameObject[] _hitVfxPrefabs;
    [SerializeField] private float _vfxOffsetTowardsEnemy = 0.3f;

    [Header("Damage Text")]
    [SerializeField] private GameObject _damageTextPrefab;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Enemy")) return;

        // Enemy → IDamageable 로 접근해 결합도를 낮춤
        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target == null || !target.IsAlive) return;

        // (StatAtk + WeaponBaseDmg) * 1.0
        float atk    = _playerEntity != null ? _playerEntity.TotalAtk : 0f;
        float damage = DamageCalculator.CalcOutgoingDamage(atk, _baseDamage);

        target.TakeDamage(damage, gameObject);

        SpawnHitVFX(other);
        SpawnDamageText(other, damage);
    }

    private void SpawnHitVFX(Collider2D enemyCollider)
    {
        if (_hitVfxPrefabs == null || _hitVfxPrefabs.Length == 0) return;

        Vector3 closestHitPoint = enemyCollider.ClosestPoint(transform.position);
        Vector3 enemyCenter     = enemyCollider.bounds.center;
        Vector3 dirToCenter     = (enemyCenter - closestHitPoint).normalized;

        float   maxDist      = Vector3.Distance(closestHitPoint, enemyCenter);
        float   actualOffset = Mathf.Min(_vfxOffsetTowardsEnemy, maxDist * 0.5f);
        Vector3 spawnPos     = closestHitPoint + dirToCenter * actualOffset;

        GameObject vfxObj = Instantiate(
            _hitVfxPrefabs[Random.Range(0, _hitVfxPrefabs.Length)],
            spawnPos,
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

    private void SpawnDamageText(Collider2D enemyCollider, float damageAmount)
    {
        if (_damageTextPrefab == null) return;

        Vector3    spawnPos = enemyCollider.bounds.center + Vector3.up * 0.5f;
        GameObject textObj  = Instantiate(_damageTextPrefab, spawnPos, Quaternion.identity);
        DamageText dmgText  = textObj.GetComponent<DamageText>();
        if (dmgText != null) dmgText.Setup(Mathf.RoundToInt(damageAmount));
    }
}
