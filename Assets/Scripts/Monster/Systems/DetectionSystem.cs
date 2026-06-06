using UnityEngine;

/// <summary>
/// 감지 범위를 기준으로 위협 대상을 탐지.
/// - Hostile/Boss: 플레이어를 감지하여 추격.
/// - Neutral: 같은 Entity 레이어 내에서 MonsterType.Hostile인 적을 항상 감지하여 도망.
///   공격 여부와 무관하게 감지범위에 에너미가 있으면 도망 트리거.
///   (플레이어 공격에 의한 도망은 NeutralMonster.TakeDamage에서 처리)
/// </summary>
[RequireComponent(typeof(MonsterRuntimeData))]
public sealed class DetectionSystem : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private LayerMask _obstacleMask;
    [SerializeField] private LayerMask _playerMask;
    [Tooltip("에너미 레이어 (Neutral 몹이 적대적 엔티티를 감지하여 도망할 대상)")]
    [SerializeField] private LayerMask _enemyMask;
    [Tooltip("Raycast 체크 주기 (초)")]
    [SerializeField] private float _checkInterval = 0.15f;

    private MonsterRuntimeData _runtime;
    private float              _checkTimer;

    [Tooltip("시야가 차단된 후 추격을 해제하기까지의 시간 (초)")]
    [SerializeField] private float _losBreakDuration = 4f;
    private float _losBlockedTimer;

    private readonly Collider2D[] _colliderBuffer = new Collider2D[16];

    // LoS 레이캐스트용 — 트리거 콜라이더(보스존 감지 영역 등)는 시야를 막지 않도록 useTriggers=false 필터 사용
    private readonly RaycastHit2D[] _losHitBuffer = new RaycastHit2D[1];
    private ContactFilter2D _losFilter;

    public bool      HasTarget      => _runtime.DetectedPlayer != null;
    public Transform DetectedTarget => _runtime.DetectedPlayer;

    private void Awake()
    {
        _runtime = GetComponent<MonsterRuntimeData>();

        // 시야 판정 필터: 장애물 레이어만, 트리거는 제외(트리거는 물리 벽이 아니라 영역 감지용이므로 시야를 가리면 안 됨)
        _losFilter = new ContactFilter2D
        {
            useTriggers = false
        };
        _losFilter.SetLayerMask(_obstacleMask);
    }

    private void Update()
    {
        _checkTimer += Time.deltaTime;
        if (_checkTimer < _checkInterval) return;
        _checkTimer = 0f;

        PerformDetection();
    }

    private void PerformDetection()
    {
        if (_runtime.Data == null) return;

        if (_runtime.Type == MonsterType.Neutral)
            PerformNeutralDetection();
        else
            PerformHostileDetection();
    }

    /// <summary>Hostile/Boss: 기존 플레이어 감지 로직</summary>
    private void PerformHostileDetection()
    {
        float radius = _runtime.Data.DetectionRadius;

        int hitCount = Physics2D.OverlapCircleNonAlloc(
            transform.position, radius, _colliderBuffer, _playerMask);

        Collider2D playerHit = FindTaggedCollider(hitCount, "Player");

        if (playerHit == null)
        {
            HandleOutOfRange();
            return;
        }

        // LoS 체크 — 트리거(보스존 감지 영역 등)는 시야를 막지 않도록 필터(useTriggers=false) 사용
        Vector2 direction = (playerHit.transform.position - transform.position);
        float distance = direction.magnitude;
        int losCount = Physics2D.Raycast(
            transform.position, direction.normalized, _losFilter, _losHitBuffer, distance);

        if (losCount > 0)
        {
            HandleLineOfSightBlocked();
            return;
        }

        _runtime.DetectedPlayer = playerHit.transform;
        _runtime.LoseAggroTimer = 0f;
        _losBlockedTimer = 0f;
    }

    /// <summary>Neutral: Entity 레이어에서 MonsterType.Hostile만 감지. 공격 여부와 무관하게 감지범위에 적이 있으면 도망.</summary>
    private void PerformNeutralDetection()
    {
        float radius = _runtime.Data.DetectionRadius;

        int hitCount = Physics2D.OverlapCircleNonAlloc(
            transform.position, radius, _colliderBuffer, _enemyMask);

        Collider2D enemyHit = FindClosestHostile(hitCount);

        if (enemyHit == null) return;

        // 콜라이더의 직접 transform을 사용 (transform.root는 씬 루트를 반환할 수 있음)
        Transform newThreat = enemyHit.transform;

        // 현재 타겟이 없으면 즉시 설정
        if (_runtime.DetectedPlayer == null)
        {
            _runtime.DetectedPlayer = newThreat;
            _runtime.LoseAggroTimer = 0f;
            _losBlockedTimer = 0f;
            return;
        }

        // 이미 타겟이 있어도, 새 위협이 더 가까우면 교체
        float currentDist = Vector2.SqrMagnitude(
            (Vector2)_runtime.DetectedPlayer.position - (Vector2)transform.position);
        float newDist = Vector2.SqrMagnitude(
            (Vector2)newThreat.position - (Vector2)transform.position);

        if (newDist < currentDist)
        {
            _runtime.DetectedPlayer = newThreat;
            _runtime.LoseAggroTimer = 0f;
            _losBlockedTimer = 0f;
        }
    }

    private Collider2D FindTaggedCollider(int hitCount, string tag)
    {
        for (int i = 0; i < hitCount; i++)
        {
            Transform root = _colliderBuffer[i].transform.root;
            if (_colliderBuffer[i].CompareTag(tag) || root.CompareTag(tag))
                return _colliderBuffer[i];
        }
        return null;
    }

    /// <summary>
    /// 에너미 레이어에서 적대적 엔티티를 필터, 가장 가까운 것 반환.
    /// - MonsterRuntimeData가 있으면 Type == Hostile인 것만 감지 (다른 Neutral 몹 제외)
    /// - MonsterRuntimeData가 없으면 (Entity.cs 에너미 등) 적대적으로 간주
    /// - 자기 자신은 항상 제외
    /// </summary>
    private Collider2D FindClosestHostile(int hitCount)
    {
        Collider2D closest = null;
        float closestDist = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            // 콜라이더의 직접 gameObject 사용 (transform.root는 씬 루트를 반환할 수 있음)
            GameObject hitObj = _colliderBuffer[i].gameObject;
            if (hitObj == gameObject) continue;

            // 자기 자신의 자식 콜라이더인 경우도 제외
            if (_colliderBuffer[i].transform.IsChildOf(transform)) continue;

            var runtimeData = hitObj.GetComponent<MonsterRuntimeData>();

            // MonsterRuntimeData가 있으면 Hostile 타입만 통과 (Neutral 몹 제외)
            if (runtimeData != null)
            {
                if (runtimeData.Type != MonsterType.Hostile)
                    continue;
            }
            // MonsterRuntimeData가 없는 경우 (Entity.cs 에너미 등): 적대적으로 간주

            float dist = Vector2.SqrMagnitude(
                (Vector2)hitObj.transform.position - (Vector2)transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = _colliderBuffer[i];
            }
        }
        return closest;
    }

    private void HandleOutOfRange()
    {
        if (_runtime.DetectedPlayer == null) return;

        if (_runtime.Type == MonsterType.Hostile)
        {
            _runtime.LoseAggroTimer += _checkInterval;
            if (_runtime.LoseAggroTimer >= _losBreakDuration)
                ForceRelease();
        }
    }

    private void HandleLineOfSightBlocked()
    {
        if (_runtime.DetectedPlayer == null) return;

        if (_runtime.Type == MonsterType.Hostile)
        {
            _losBlockedTimer += _checkInterval;
            if (_losBlockedTimer >= _losBreakDuration)
                ForceRelease();
        }
    }

    /// <summary>감지 강제 해제</summary>
    public void ForceRelease()
    {
        _runtime.DetectedPlayer = null;
        _runtime.LoseAggroTimer = 0f;
        _losBlockedTimer = 0f;
    }

    /// <summary>외부에서 직접 위협 대상 등록 (피격 시 사용)</summary>
    public void ForceDetect(Transform threat)
    {
        _runtime.DetectedPlayer = threat;
        _runtime.LoseAggroTimer = 0f;
        _losBlockedTimer = 0f;
    }

    private void OnDrawGizmosSelected()
    {
        if (_runtime == null) _runtime = GetComponent<MonsterRuntimeData>();
        if (_runtime == null || _runtime.Data == null) return;

        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, _runtime.Data.DetectionRadius);
    }
}
