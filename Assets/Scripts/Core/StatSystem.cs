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
    [SerializeField] private float _baseDef       = 0f;
    [SerializeField] private float _baseMoveSpeed = 5f;
    [SerializeField] private float _baseMana      = 100f;
    [Tooltip("초당 마나 자연 회복량")]
    [SerializeField] private float _manaRegen     = 1f;

    // Play 모드 Inspector에서 실시간 확인용 (읽기 전용 표시)
    [SerializeField, HideInInspector] private float _currentMana;
    public float CurrentMana
    {
        get => _currentMana;
        private set => _currentMana = value;
    }

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
            _providers.Add(provider);
    }

    public void UnregisterBonus(IBonusProvider provider)
    {
        _providers.Remove(provider);
    }
}
