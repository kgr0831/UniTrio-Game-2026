using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 기초 스탯(Atk, Def, MoveSpeed, Mana)과 마나 자연 회복을 전담하는 부착형 컴포넌트 (SRP).
/// IBonusProvider 목록을 등록/해제해 아이템·버프 보너스를 동적으로 합산합니다.
/// LINQ 사용 금지 → for 루프로 합산 처리합니다.
/// </summary>
public sealed class StatSystem : MonoBehaviour
{
    [Header("Base Stats")]
    [SerializeField] private float _baseAtk       = 5f;
    [SerializeField] private float _baseMagicAtk  = 5f;
    [SerializeField] private float _baseDef       = 0f;
    [SerializeField] private float _baseMoveSpeed = 5f;
    [SerializeField] private float _baseMana      = 100f;
    [SerializeField] private float _baseMaxHP     = 100f;
    [Tooltip("초당 마나 자연 회복량")]
    [SerializeField] private float _manaRegen     = 1f;

    /// <summary>스탯이 변경되었을 때(장비 교체 등) 발생하는 이벤트.</summary>
    public event System.Action OnStatsChanged;


    // Play 모드 Inspector에서 실시간 확인용 (Normal/Debug 모드 모두 표시)
    [SerializeField] private float _currentMana;
    public float CurrentMana
    {
        get => _currentMana;
        private set => _currentMana = value;
    }

    /// <summary>강타(Bash) 스택: 다음 N회 공격 시 데미지 2배</summary>
    public int BashCount { get; set; }

    // 리스트 기반 보너스 집계 (LINQ 금지 → for 루프 사용)
    private readonly List<IBonusProvider> _providers = new List<IBonusProvider>();

    // ── 합산 스탯 ────────────────────────────────────────────────────

    public float TotalAtk
    {
        get
        {
            float total = _baseAtk;
            for (int i = 0; i < _providers.Count; i++) total += _providers[i].GetAttackBonus();
            return total;
        }
    }

    public float TotalMagicAtk
    {
        get
        {
            float total = _baseMagicAtk;
            for (int i = 0; i < _providers.Count; i++) total += _providers[i].GetMagicAttackBonus();
            return total;
        }
    }

    public float TotalDef
    {
        get
        {
            float total = _baseDef;
            for (int i = 0; i < _providers.Count; i++) total += _providers[i].GetDefenseBonus();
            return total;
        }
    }

    public float TotalMoveSpeed
    {
        get
        {
            float total = _baseMoveSpeed;
            for (int i = 0; i < _providers.Count; i++) total += _providers[i].GetSpeedBonus();
            return total;
        }
    }

    public float MaxMana
    {
        get
        {
            float total = _baseMana;
            for (int i = 0; i < _providers.Count; i++) total += _providers[i].GetManaBonus();
            return total;
        }
    }

    public float TotalMaxHP
    {
        get
        {
            float total = _baseMaxHP;
            for (int i = 0; i < _providers.Count; i++) total += _providers[i].GetMaxHPBonus();
            return total;
        }
    }

    public float TotalAttackSpeed
    {
        get
        {
            float total = 1.0f; // 기본 배율 100%
            for (int i = 0; i < _providers.Count; i++) total += _providers[i].GetAttackSpeedBonus();
            return total;
        }
    }


    private float _manaRegenTimer;

    private void Awake()
    {
        CurrentMana = _baseMana;
    }

    private void Update()
    {
        // 초당 1틱 마나 자연 회복 (GC 없는 타이머 방식)
        _manaRegenTimer += Time.deltaTime;
        if (_manaRegenTimer >= 1f)
        {
            _manaRegenTimer -= 1f;
            CurrentMana = Mathf.Min(MaxMana, CurrentMana + _manaRegen);
        }
    }

    // ── Public API ──────────────────────────────────────────────────

    public void ConsumeMana(float amount)
    {
        CurrentMana = Mathf.Max(0f, CurrentMana - amount);
    }

    public void RestoreMana(float amount)
    {
        CurrentMana = Mathf.Min(MaxMana, CurrentMana + amount);
    }

    public bool HasEnoughMana(float cost) => CurrentMana >= cost;

    public void RegisterBonus(IBonusProvider provider)
    {
        if (!_providers.Contains(provider))
        {
            _providers.Add(provider);
            OnStatsChanged?.Invoke();
        }
    }

    public void UnregisterBonus(IBonusProvider provider)
    {
        if (_providers.Remove(provider))
        {
            OnStatsChanged?.Invoke();
        }
    }


    /// <summary>
    /// 강타(Bash) 스택을 확인하고 소진합니다.
    /// </summary>
    /// <returns>공격력 배율 (강타 적용 시 2.0, 아니면 1.0)</returns>
    public float UseBashStack()
    {
        if (BashCount > 0)
        {
            BashCount--;
            return 2.0f;
        }
        return 1.0f;
    }
}
