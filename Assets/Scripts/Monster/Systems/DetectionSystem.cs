using UnityEngine;

/// <summary>
/// 감지 범위를 기준으로 플레이어를 탐지.
/// 장애물(Obstacle/Wall 레이어)에 가려져 있으면 감지 불가 (Raycast2D).
/// 적대적 몹: 감지 범위 밖 또는 시야 차단 4초 이상 → 추격 해제.
/// 중립 몹: 감지 범위 × 1.5 벗어나면 도망 해제.
/// </summary>
[RequireComponent(typeof(MonsterRuntimeData))]
public sealed class DetectionSystem : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private LayerMask _obstacleMask;  // "Wall" + "Obstacle" 레이어
    [SerializeField] private LayerMask _playerMask;    // "Player" 레이어
    [Tooltip("Raycast 체크 주기 (초). 매 프레임 대신 주기적 체크로 성능 최적화")]
    [SerializeField] private float _checkInterval = 0.15f;

    private MonsterRuntimeData _runtime;
    private float              _checkTimer;

    // ── LoS 차단 타이머 (적대적 몹용) ──
    [Tooltip("시야가 차단된 후 추격을 해제하기까지의 시간 (초)")]
    [SerializeField] private float _losBreakDuration = 4f;
    private float _losBlockedTimer;
    
    // GC 조절을 위한 캐싱 버퍼 (성능 가이드라인 준수)
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
        
        float radius = _runtime.Data.DetectionRadius;

        // 1. 범위 내 플레이어 검출 (NonAlloc API 사용하여 가비지 프리 구현)
        int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, radius, _colliderBuffer, _playerMask);
        Collider2D playerHit = null;
        for (int i = 0; i < hitCount; i++)
        {
            // 콜라이더가 자식 오브젝트에 있는 경우를 대비해 루트 오브젝트의 태그도 확인 (Robustness)
            Transform root = _colliderBuffer[i].transform.root;
            if (_colliderBuffer[i].CompareTag("Player") || root.CompareTag("Player"))
            {
                playerHit = _colliderBuffer[i];
                break;
            }
        }

        if (playerHit == null)
        {
            // 범위 밖 → 적대적 몹은 타이머 카운트
            HandleOutOfRange();
            return;
        }

        // 디버깅: 처음 감지했을 때만 로그 출력
        if (_runtime.DetectedPlayer == null)
        {
            Debug.Log($"[DetectionSystem] {gameObject.name} found Player! Fleeing logic should trigger.");
        }

        // 2. LoS (Line of Sight) 체크 — 장애물 레이캐스트
        // 중립 몹(초식동물 등)은 뒷편이라도 소리를 듣고 놀랄 수 있으므로 무조건 감지하도록 예외 처리.
        if (_runtime.Type != MonsterType.Neutral)
        {
            Vector2 direction = (playerHit.transform.position - transform.position);
            float distance = direction.magnitude;

            RaycastHit2D losHit = Physics2D.Raycast(
                transform.position, direction.normalized, distance, _obstacleMask);

            if (losHit.collider != null)
            {
                // 장애물에 가려짐 → 적대적 몹은 타이머 카운트
                HandleLineOfSightBlocked();
                return;
            }
        }

        // 3. 감지 성공
        _runtime.DetectedPlayer = playerHit.transform;
        _runtime.LoseAggroTimer = 0f;
        _losBlockedTimer = 0f;
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
        else if (_runtime.Type == MonsterType.Neutral)
        {
            // 중립은 상위 BT에서 × 1.5 거리 체크로 해제
        }
        // 보스는 감지 해제하지 않음
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

    private void OnDrawGizmosSelected()
    {
        if (_runtime == null) _runtime = GetComponent<MonsterRuntimeData>();
        if (_runtime == null || _runtime.Data == null) return;

        // 투명한 빨간색으로 시각적 탐지 범위를 그림
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, _runtime.Data.DetectionRadius);
    }
}
