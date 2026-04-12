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
    [SerializeField] private float _staggerDuration = 0.1f;

    private MonsterRuntimeData _runtimeData;
    private HealthSystem       _healthSystem;
    private float              _staggerTimer;

    public bool IsStaggered => _runtimeData != null && _runtimeData.IsStaggered;

    private void Awake()
    {
        _runtimeData = GetComponent<MonsterRuntimeData>();
        _healthSystem = GetComponent<HealthSystem>();
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
    }

    private void Update()
    {
        if (!_runtimeData.IsStaggered) return;

        _staggerTimer -= Time.deltaTime;
        if (_staggerTimer <= 0f)
        {
            _runtimeData.IsStaggered = false;
            _runtimeData.CurrentState = MonsterState.Idle; // BT가 다음 프레임에 재판단
        }
    }

    private void OnHit()
    {
        ApplyStagger(_staggerDuration);
    }

    public void ApplyStagger(float duration)
    {
        _runtimeData.IsStaggered = true;
        _runtimeData.CurrentState = MonsterState.Stagger;
        _staggerTimer = duration;

        // 즉시 이동 정지
        var rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;

        // 피격 애니메이션
        GetComponent<MonsterAnimatorController>()?.PlayHit();
    }
}
