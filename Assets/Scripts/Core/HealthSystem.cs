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

    [Header("Hit Flash (SpriteGlow Shader)")]
    [Tooltip("피격 플래시가 유지되는 시간 (초)")]
    [SerializeField] private float _flashDuration = 0.15f;

    public float MaxHp     => _maxHp;
    public float CurrentHp { get; private set; }
    public bool  IsAlive   => CurrentHp > 0f;

    // 이벤트 기반 갱신 → UI·외부 로직은 구독만 하면 됨 (폴링 불필요)
    public event Action<float, float> OnHpChanged; // (currentHp, maxHp)
    public event Action OnDied;
    public event Action OnHit;

    private SpriteRenderer        _spriteRenderer;
    private MaterialPropertyBlock _mpb;
    private float                 _flashTimer;
    private static readonly int   HashFlashAmount = Shader.PropertyToID("_FlashAmount");

    private void Awake()
    {
        CurrentHp       = _maxHp;
        _spriteRenderer = GetComponent<SpriteRenderer>();

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
        if (!IsAlive) return;

        CurrentHp = Mathf.Max(0f, CurrentHp - damage);

        // 플래시 시작 (타이머 리셋으로 중첩 점멸 방지)
        SetFlashShader(1f);
        _flashTimer = _flashDuration;

        OnHit?.Invoke();
        OnHpChanged?.Invoke(CurrentHp, _maxHp);

        if (CurrentHp <= 0f)
            OnDied?.Invoke();
    }

    public void Heal(float amount)
    {
        if (!IsAlive) return;
        CurrentHp = Mathf.Min(CurrentHp + amount, _maxHp);
        OnHpChanged?.Invoke(CurrentHp, _maxHp);
    }

    public void SetMaxHp(float newMax, bool refill = false)
    {
        _maxHp    = newMax;
        CurrentHp = refill ? _maxHp : Mathf.Min(CurrentHp, _maxHp);
        OnHpChanged?.Invoke(CurrentHp, _maxHp);
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
