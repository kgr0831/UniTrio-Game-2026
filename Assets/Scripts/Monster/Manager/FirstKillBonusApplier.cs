using UnityEngine;

/// <summary>
/// HealthSystem의 사망 이벤트를 구독하고, 
/// 해당 몹이 FirstKillRegistry에 없다면 플레이어의 영구 스탯을 올려줌.
/// 각 몹에 컴포넌트로 부착 (SRP).
/// </summary>
[RequireComponent(typeof(HealthSystem))]
[RequireComponent(typeof(MonsterRuntimeData))]
public sealed class FirstKillBonusApplier : MonoBehaviour
{
    [Header("First Kill Bonus Config")]
    [Tooltip("첫 처치 시 증가시킬 영구 체력")]
    [SerializeField] private float _bonusMaxHP = 0f;
    [Tooltip("첫 처치 시 증가시킬 영구 물리공격력")]
    [SerializeField] private float _bonusAtk = 1f;
    [Tooltip("첫 처치 시 증가시킬 영구 마법공격력")]
    [SerializeField] private float _bonusMagicAtk = 0f;

    private HealthSystem       _healthSystem;
    private MonsterRuntimeData _runtime;

    private void Awake()
    {
        _healthSystem = GetComponent<HealthSystem>();
        _runtime      = GetComponent<MonsterRuntimeData>();
    }

    private void OnEnable()
    {
        if (_healthSystem != null)
            _healthSystem.OnDied += ApplyBonusIfFirstKill;
    }

    private void OnDisable()
    {
        if (_healthSystem != null)
            _healthSystem.OnDied -= ApplyBonusIfFirstKill;
    }

    private void ApplyBonusIfFirstKill()
    {
        // 첫 킬인지 판별
        if (FirstKillRegistry.RegisterKillIfFirst(_runtime.MonsterId))
        {
            Debug.Log($"<color=yellow>[First Kill]</color> {_runtime.Data.MonsterName} 첫 처치! 영구 스탯이 증가합니다.");

            // 플레이어 StatSystem 갱신
            // (StatSystem은 씬 내 단일 인스턴스/플레이어 오브젝트에 있다고 가정)
            StatSystem playerStats = FindObjectOfType<StatSystem>();
            if (playerStats != null)
            {
                playerStats.AddPermanentBonus(_bonusMaxHP, _bonusAtk, _bonusMagicAtk);
            }
        }
    }
}
