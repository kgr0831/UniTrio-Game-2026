using UnityEngine;

/// <summary>피격 시 경직 가능한 객체의 인터페이스 (ISP)</summary>
public interface IStaggerable
{
    bool IsStaggered { get; }
    void ApplyStagger(float duration);
}

/// <summary>
/// 피격 시 0.1초 경직을 전담하는 컴포넌트 (SRP).
/// HealthSystem.OnHit 이벤트를 구독하여 자동 발동.
/// 타이머 기반 → 코루틴 없이 GC 제로.
/// </summary>
[RequireComponent(typeof(HealthSystem))]
[RequireComponent(typeof(MonsterRuntimeData))]
public sealed class HitStaggerHandler : MonoBehaviour, IStaggerable
{
    [SerializeField] private float _staggerDuration = 0.2f;

    /// <summary>스태거 지속 시간 배율. NeutralMonster 등에서 외부 설정 가능.</summary>
    private float _staggerMultiplier = 1f;

    private MonsterRuntimeData _runtimeData;
    private HealthSystem       _healthSystem;
    private Animator           _animator;
    private float              _staggerTimer;

    public bool IsStaggered => _runtimeData != null && _runtimeData.IsStaggered;

    private void Awake()
    {
        _runtimeData = GetComponent<MonsterRuntimeData>();
        _healthSystem = GetComponent<HealthSystem>();
        _animator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        if (_healthSystem != null)
            _healthSystem.OnHit += OnHit;
    }

    private void OnDisable()
    {
        if (_healthSystem != null)
            _healthSystem.OnHit -= OnHit;
        
        // Disable 시 속도 복구 방어 코드
        if (_animator != null) _animator.speed = 1f;
    }

    private void Update()
    {
        // 회피 저스트 카운터 중에는 스태거 타이머 정지 (애니 speed도 건드리지 않음 → 매니저가 원복 담당)
        if (MonsterFreezeManager.IsFrozen) return;

        if (!_runtimeData.IsStaggered) return;

        _staggerTimer -= Time.deltaTime;
        if (_staggerTimer <= 0f)
        {
            _runtimeData.IsStaggered = false;
            
            // Stagger 종료 시 속도 복구 및 상태 전이
            if (_animator != null) _animator.speed = 1f;
            _runtimeData.CurrentState = MonsterState.Idle; // BT가 다음 프레임에 재판단
        }
    }

    private void OnHit()
    {
        ApplyStagger(_staggerDuration);
    }

    /// <summary>스태거 지속 시간 배율을 설정합니다. NeutralMonster 등에서 호출합니다.</summary>
    public void SetStaggerMultiplier(float multiplier)
    {
        _staggerMultiplier = Mathf.Max(0.1f, multiplier);
    }

    public void ApplyStagger(float duration)
    {
        float actualDuration = duration * _staggerMultiplier;

        _runtimeData.IsStaggered = true;
        _runtimeData.CurrentState = MonsterState.Stagger;
        _staggerTimer = actualDuration;

        // 히트스탑 효과 (애니메이션 일시 정지)
        if (_animator != null) _animator.speed = 0f;

        // 즉시 이동 정지
        var rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;

        // 스프라이트 점멸 타이머도 실제 스태거 시간에 맞게 동기화
        _healthSystem?.OverrideFlashTimer(actualDuration);

        GetComponent<MonsterAnimatorController>()?.PlayHit();
    }
}
