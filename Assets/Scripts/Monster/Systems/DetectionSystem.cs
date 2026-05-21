using UnityEngine;

/// <summary>
/// 감지 범위를 기준으로 위협 대상을 탐지.
/// - Hostile/Boss: 플레이어를 감지하여 추격.
/// - Neutral: 같은 Entity 레이어 내에서 MonsterType.Hostile인 적만 감지하여 도망.
///   플레이어 근접만으로는 도망하지 않음. (플레이어 공격에 의한 도망은 NeutralMonster.TakeDamage에서 처리)
/// </summary>
[RequireComponent(typeof(MonsterRuntimeData))]
public sealed class DetectionSystem : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private LayerMask _obstacleMask;
    [SerializeField] private LayerMask _playerMask;
    [Tooltip("Entity 레이어 (Neutral 몹이 Hostile 타입을 감지하여 도망할 대상)")]
    [SerializeField] private LayerMask _entityMask;
    [Tooltip("Raycast 체크 주기 (초)")]
    [SerializeField] private float _checkInterval = 0.15f;

    private MonsterRuntimeData _runtime;
    private float              _checkTimer;

    [Tooltip("시야가 차단된 후 추격을 해제하기까지의 시간 (초)")]
    [SerializeField] private float _losBreakDuration = 4f;
    private float _losBlockedTimer;

    private readonly Collider2D[] _colliderBuffer = new Collider2D[16];

    public bool      HasTarget      => _runtime.DetectedPlayer != null;
    public Transform DetectedTarget => _runtime.DetectedPlayer;

    private void Awake()
    {
        _runtime = GetComponent<MonsterRuntimeData>();
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

        // LoS 체크
        Vector2 direction = (playerHit.transform.position - transform.position);
        float distance = direction.magnitude;
        RaycastHit2D losHit = Physics2D.Raycast(
            transform.position, direction.normalized, distance, _obstacleMask);

        if (losHit.collider != null)
        {
            HandleLineOfSightBlocked();
            return;
        }

        _runtime.DetectedPlayer = playerHit.transform;
        _runtime.LoseAggroTimer = 0f;
        _losBlockedTimer = 0f;
    }

    /// <summary>Neutral: Entity 레이어에서 MonsterType.Hostile만 감지. 플레이어 근접으로는 도망하지 않음.</summary>
    private void PerformNeutralDetection()
    {
        if (_runtime.DetectedPlayer != null) return;

        float radius = _runtime.Data.DetectionRadius;

        int hitCount = Physics2D.OverlapCircleNonAlloc(
            transform.position, radius, _colliderBuffer, _entityMask);

        Collider2D enemyHit = FindClosestHostile(hitCount);

        if (enemyHit == null) return;

        _runtime.DetectedPlayer = enemyHit.transform.root;
        _runtime.LoseAggroTimer = 0f;
        _losBlockedTimer = 0f;
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

    /// <summary>Entity 레이어 중 Hostile 타입만 필터, 가장 가까운 것 반환 (자기 자신 및 다른 Neutral 몹 제외)</summary>
    private Collider2D FindClosestHostile(int hitCount)
    {
        Collider2D closest = null;
        float closestDist = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            GameObject hitObj = _colliderBuffer[i].transform.root.gameObject;
            if (hitObj == gameObject) continue;

            var runtimeData = hitObj.GetComponent<MonsterRuntimeData>();
            if (runtimeData != null && runtimeData.Type == MonsterType.Hostile)
            {
                float dist = Vector2.SqrMagnitude(
                    (Vector2)hitObj.transform.position - (Vector2)transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = _colliderBuffer[i];
                }
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
