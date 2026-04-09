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
    [Tooltip("기능 무기 고유의 추가 데미지. (WeaponData에 값이 있다면 이 값은 0으로 두는 것을 권장합니다.)")]
    [SerializeField] private float _baseDamage = 0f;

    [Tooltip("이 무기가 공격할 수 있는 대상의 태그 목록. 기본값은 'Enemy'.")]
    [SerializeField] private string[] _targetTags = new string[] { "Enemy", "Tree" };

    [Header("Hit VFX")]
    [SerializeField] private GameObject[] _hitVfxPrefabs;
    [SerializeField] private float _vfxOffsetTowardsEnemy = 0.3f;

    [Header("Damage Text")]
    [SerializeField] private GameObject _damageTextPrefab;

    private SwordBehaviour _swordBehaviour;

    private void Awake()
    {
        _swordBehaviour = GetComponentInParent<SwordBehaviour>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        bool isValidTag = false;
        if (_targetTags != null)
        {
            foreach (var t in _targetTags)
            {
                if (other.CompareTag(t))
                {
                    isValidTag = true;
                    break;
                }
            }
        }
        if (!isValidTag) return;

        // Enemy → IDamageable 로 접근해 결합도를 낮춤
        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target == null || !target.IsAlive) return;

        // (StatAtk + WeaponBaseDmg) * 1.0 * (BashMultiplier)
        float atk    = _playerEntity != null ? _playerEntity.TotalAtk : 0f;
        float damage = DamageCalculator.CalcOutgoingDamage(atk, _baseDamage);
        
        // 강타(Bash) 배율 적용 — 스윙 시작 시 이미 소모된 배율을 읽음
        if (_swordBehaviour != null)
            damage *= _swordBehaviour.CurrentSwingBashMultiplier;

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

        // 최적화: Instantiate 대신 SimpleObjectPool에서 가져옵니다.
        // 자식에 붙은 HitVfxAutoReturn.cs가 애니메이션 후 자동으로 Release를 호출합니다.
        GameObject prefab = _hitVfxPrefabs[Random.Range(0, _hitVfxPrefabs.Length)];
        SimpleObjectPool.Instance.Get(prefab, spawnPos, Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
    }

    private void SpawnDamageText(Collider2D enemyCollider, float damageAmount)
    {
        if (_damageTextPrefab == null) return;

        Vector3 spawnPos = enemyCollider.bounds.center + Vector3.up * 0.5f;

        // 최적화: Instantiate 대신 SimpleObjectPool에서 가져옵니다.
        // DamageText.cs 에도 자동 풀 반환 로직이 추가될 예정입니다.
        GameObject textObj = SimpleObjectPool.Instance.Get(_damageTextPrefab, spawnPos, Quaternion.identity);
        DamageText dmgText = textObj.GetComponent<DamageText>();
        if (dmgText != null) dmgText.Setup(Mathf.RoundToInt(damageAmount));
    }
}
