using System;
using UnityEngine;

/// <summary>
/// HP 및 피격 플래시 효과를 전담하는 부착형 컴포넌트 (SRP).
/// LivingEntity가 RequireComponent로 강제 부착하며, 이벤트로 UI/외부 로직에 변경을 알립니다.
/// 코루틴 대신 타이머 변수로 플래시를 제어해 GC를 최소화합니다.
/// </summary>
public sealed class HealthSystem : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float _maxHp = 10f;

    [Header("Hit Flash")]
    [Tooltip("피격 플래시가 유지되는 시간 (초)")]
    [SerializeField] private float _flashDuration = 0.75f;

    // Inspector(Normal 모드)에서 실시간 HP / 생존 여부 확인용
    [SerializeField] private float _currentHealth;
    [SerializeField] private bool  _isAlive;

    public float MaxHp     => _maxHp;
    public float CurrentHp => _currentHealth;
    public bool  IsAlive   => _isAlive;

    /// <summary>무적 상태 여부. true일 경우 ApplyDamage가 무시됩니다.</summary>
    public bool  IsInvulnerable { get; set; }

    // 이벤트 기반 갱신 → UI·외부 로직은 구독만 하면 됨 (폴링 불필요)
    public event Action<float, float> OnHpChanged; // (currentHp, maxHp)
    public event Action OnDied;
    public event Action OnHit;

    private SpriteRenderer        _spriteRenderer;
    private MaterialPropertyBlock _mpb;
    private float                 _flashTimer;
    private StatSystem            _statSystem;
    private static readonly int   HashFlashAmount = Shader.PropertyToID("_FlashAmount");

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _statSystem     = GetComponent<StatSystem>();

        // StatSystem이 있으면 초기 최대 체력을 동기화
        if (_statSystem != null)
        {
            _maxHp = _statSystem.TotalMaxHP;
            _statSystem.OnStatsChanged += SyncMaxHp;
        }

        _currentHealth  = _maxHp;
        _isAlive        = true;
        IsInvulnerable  = false;

        // SpriteRenderer가 없는 엔티티(비가시 트리거 등)도 허용 – null 체크로 방어
        if (_spriteRenderer != null)
            _mpb = new MaterialPropertyBlock();
    }


    private void Update()
    {
        // 플래시 타이머 소진 시 끄기 (코루틴 없이 GC 제로)
        if (_flashTimer > 0f)
        {
            _flashTimer -= Time.deltaTime;
            if (_flashTimer <= 0f)
                SetFlashShader(0f);
        }
    }

    // ── Public API ──────────────────────────────────────────────────

    public void ApplyDamage(float damage)
    {
        if (!IsAlive || IsInvulnerable) return;

        _currentHealth = Mathf.Max(0f, _currentHealth - damage);
        _isAlive       = _currentHealth > 0f;

        // 플래시 시작 (타이머 리셋으로 중첩 점멸 방지)
        SetFlashShader(1f);
        _flashTimer = _flashDuration;

        OnHit?.Invoke();
        OnHpChanged?.Invoke(_currentHealth, _maxHp);

        if (_currentHealth <= 0f)
            OnDied?.Invoke();
    }

    /// <summary>현재 점멸 타이머를 외부에서 덮어씌웁니다. 스턴 시간과 동기화할 때 사용.</summary>
    public void OverrideFlashTimer(float duration)
    {
        SetFlashShader(1f);
        _flashTimer = duration;
    }

    public void Heal(float amount)
    {
        if (!IsAlive) return;
        _currentHealth = Mathf.Min(_currentHealth + amount, _maxHp);
        OnHpChanged?.Invoke(_currentHealth, _maxHp);
    }

    /// <summary>죽은 상태에서 HP를 회복시킵니다. amount < 0 이면 MaxHp로 전체 회복.</summary>
    public void Resurrect(float amount = -1f)
    {
        _isAlive       = true;
        _currentHealth = amount < 0f ? _maxHp : Mathf.Clamp(amount, 0.01f, _maxHp);
        OnHpChanged?.Invoke(_currentHealth, _maxHp);
    }

    public void SetMaxHp(float newMax, bool refill = false)
    {
        if (newMax <= 0) return;

        // 현재 체력 비율 유지 (예: 50%일 때 최대체력이 늘어나도 계속 50% 유지)
        float hpRatio = _maxHp > 0 ? _currentHealth / _maxHp : 1f;

        _maxHp = newMax;
        
        if (refill)
        {
            _currentHealth = _maxHp;
        }
        else
        {
            _currentHealth = _maxHp * hpRatio;
        }

        _isAlive = _currentHealth > 0f;
        OnHpChanged?.Invoke(_currentHealth, _maxHp);
    }

    private void SyncMaxHp()
    {
        if (_statSystem == null) return;
        
        // StatSystem으로부터 합산된 최대 체력을 가져와 설정
        SetMaxHp(_statSystem.TotalMaxHP, false);
    }


    private void OnDestroy()
    {
        if (_statSystem != null)
            _statSystem.OnStatsChanged -= SyncMaxHp;
    }

    // ── 내부 ────────────────────────────────────────────────────────

    private void SetFlashShader(float amount)
    {
        if (_spriteRenderer == null || _mpb == null) return;

        // MaterialPropertyBlock: 머티리얼 인스턴스 복제 없이 프로퍼티만 덮어씀 (배칭 유지)
        _spriteRenderer.GetPropertyBlock(_mpb);
        _mpb.SetFloat(HashFlashAmount, amount);
        _spriteRenderer.SetPropertyBlock(_mpb);
    }
}



