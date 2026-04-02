using UnityEngine;

/// <summary>
/// 검의 히트박스 판정을 전담하는 스크립트.
/// SwordHitbox 게임 오브젝트 (Rigidbody2D + BoxCollider2D Trigger) 에 부착하여 사용합니다.
/// </summary>
public class SwordHitbox : MonoBehaviour
{
    [Header("Hit Stop (역경직)")]
    [SerializeField] private float _hitStopDuration = 0.08f; // 히트 스톱 유지 시간 (초)
    private static bool _isHitStopping = false; // 동시다발적 트리거 시 중단 보호용

    [Header("Hit VFX (타격 이펙트)")]
    [SerializeField] private GameObject[] _hitVfxPrefabs; // 인스펙터에서 여러 개의 프리팹을 등록
    [SerializeField] private float _vfxOffsetTowardsEnemy = 0.3f; // 충돌점으로부터 적 중심쪽으로 파고드는 깊이

    [Header("Damage Text (데미지 팝업)")]
    [SerializeField] private GameObject _damageTextPrefab; // DamageText스크립트가 붙은 프리팹 할당

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
        {
            // 타격 시 하위/상위 어디에 컴포넌트가 있던 Enemy를 가져오기 위함
            Enemy enemy = other.GetComponentInParent<Enemy>();
            if (enemy != null)
            {
                // 기본으로 데미지 10을 입힙니다 (이 수치는 자유롭게 조절하시면 됩니다)
                int damageDealt = 10;
                enemy.TakeDamage(damageDealt);

                SpawnHitVFX(other);
                SpawnDamageText(other, damageDealt);

                // 통쾌한 타격감을 위한 히트 스톱 발생!
                if (!_isHitStopping)
                {
                    StartCoroutine(HitStopRoutine());
                }
            }
        }
    }

    private void SpawnHitVFX(Collider2D enemyCollider)
    {
        // 1. 등록된 프리팹 리스트가 있는지 방어 코드
        if (_hitVfxPrefabs == null || _hitVfxPrefabs.Length == 0) return;

        // 2. 가장 가까운 표면 충돌 지점 계산
        Vector3 swordBasePos = transform.position;
        Vector3 closestHitPoint = enemyCollider.ClosestPoint(swordBasePos);

        // 3. 충돌 지점에서 적의 한가운데(중심)를 향하는 방향 벡터 도출
        Vector3 enemyCenter = enemyCollider.bounds.center;
        Vector3 dirToCenter = (enemyCenter - closestHitPoint).normalized;

        // 4. "충돌 지점보다 적 방향으로 살짝 더 들어간" 최종 스폰 위치 (중심을 넘어가진 않게 방지)
        float maxDist = Vector3.Distance(closestHitPoint, enemyCenter);
        float actualOffset = Mathf.Min(_vfxOffsetTowardsEnemy, maxDist * 0.5f);
        Vector3 spawnPos = closestHitPoint + dirToCenter * actualOffset;

        // 5. 리스트 중 무작위 프리팹 1개 뽑기
        GameObject selectedPrefab = _hitVfxPrefabs[Random.Range(0, _hitVfxPrefabs.Length)];

        // 6. 무작위 각도로 꺾어주면 타격감이 훨씬 좋아짐 (스타일리시 게임 국룰)
        Quaternion randomRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        // 7. 소환!
        GameObject vfxObj = Instantiate(selectedPrefab, spawnPos, randomRotation);

        // 8. 프리팹 스스로의 길이를 계산하여 똑똑하게 자동 파기 (동적 Lifetime)
        float autoLifetime = 0.5f; // 만약 길이가 없을 경우를 대비한 최소 보장 시간

        // 애니메이터 길이 감지 (현재 VFX는 오직 애니메이터만 사용)
        Animator anim = vfxObj.GetComponent<Animator>();
        if (anim != null)
        {
            // Instantiate 직후에는 0프레임이라 길이가 0일 수 있으므로 즉시 한 프레임을 업데이트 시켜 강제 초기화
            anim.Update(0f); 
            autoLifetime = anim.GetCurrentAnimatorStateInfo(0).length;
        }
        
        // 파기 예약
        Destroy(vfxObj, autoLifetime);
    }

    private void SpawnDamageText(Collider2D enemyCollider, int damageAmount)
    {
        if (_damageTextPrefab == null) return;

        // 적의 머리 위쪽쯤(약간 위)을 기준으로 스폰하여 겹침을 방지합니다.
        Vector3 spawnPos = enemyCollider.bounds.center + Vector3.up * 0.5f;

        GameObject textObj = Instantiate(_damageTextPrefab, spawnPos, Quaternion.identity);
        
        DamageText dmgText = textObj.GetComponent<DamageText>();
        if (dmgText != null)
        {
            dmgText.Setup(damageAmount);
        }
    }

    private System.Collections.IEnumerator HitStopRoutine()
    {
        _isHitStopping = true;
        
        // 치는 순간 시간을 아주 잠깐 완전히 멈춥니다! 
        // 0.05 정도로 주어 극적인 슬로우 모션을 연출할 수도 있지만 역경직은 0이 가장 찰집니다.
        Time.timeScale = 0f; 

        // Time.timeScale에 영향을 받지 않는 실제 현실(Real) 시간을 기준으로 대기!
        yield return new WaitForSecondsRealtime(_hitStopDuration);

        // 시간 원상복구
        Time.timeScale = 1f;
        _isHitStopping = false;
    }
}
