using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 몬스터에 부착되는 디버프 관리 컴포넌트 (SRP).
/// 현재 적용된 디버프 목록을 관리하고, 외부에 이벤트로 상태 변경을 알립니다.
/// IDebuff 인터페이스에만 의존하여 새 디버프 추가 시 이 클래스는 수정 불필요 (OCP/DIP 준수).
/// </summary>
[RequireComponent(typeof(HealthSystem))]
[RequireComponent(typeof(MonsterRuntimeData))]
public sealed class DebuffReceiver : MonoBehaviour
{
    // 최대 3개 (Fire, Ice, Earth) 동시 적용
    private readonly IDebuff[] _activeDebuffs = new IDebuff[3]; // 인덱스 = ElementType 값

    // 컴포넌트 캐시
    private HealthSystem _health;
    private MonsterRuntimeData _runtime;

    // 이벤트: UI/셰이더가 구독하여 디버프 상태 변화에 반응
    /// <summary>디버프 적용 시 (ElementType)</summary>
    public event Action<ElementType> OnDebuffApplied;
    /// <summary>디버프 제거 시 (ElementType)</summary>
    public event Action<ElementType> OnDebuffRemoved;
    /// <summary>디버프 갱신 시 (ElementType)</summary>
    public event Action<ElementType> OnDebuffRefreshed;

    private void Awake()
    {
        _health  = GetComponent<HealthSystem>();
        _runtime = GetComponent<MonsterRuntimeData>();
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        for (int i = 0; i < _activeDebuffs.Length; i++)
        {
            IDebuff debuff = _activeDebuffs[i];
            if (debuff == null || !debuff.IsActive) continue;

            debuff.Tick(dt);

            // 만료되었으면 정리
            if (!debuff.IsActive)
            {
                ElementType element = debuff.Element;
                _activeDebuffs[i] = null;
                OnDebuffRemoved?.Invoke(element);
            }
        }
    }

    // ── Public API ──────────────────────────────────────────────

    /// <summary>
    /// 지정 속성의 디버프를 적용합니다.
    /// 이미 같은 속성 디버프가 있으면 지속시간만 갱신합니다.
    /// </summary>
    public void ApplyDebuff(ElementType element, float elementalAtk, float resistance)
    {
        int index = (int)element;
        if (index < 0 || index >= _activeDebuffs.Length) return;

        IDebuff existing = _activeDebuffs[index];

        if (existing != null && existing.IsActive)
        {
            // 이미 적용 중 → 갱신만
            existing.Refresh(elementalAtk, resistance);
            OnDebuffRefreshed?.Invoke(element);
        }
        else
        {
            // 새로 생성
            IDebuff newDebuff = CreateDebuff(element);
            if (newDebuff == null) return;

            newDebuff.Apply(elementalAtk, resistance);
            _activeDebuffs[index] = newDebuff;
            OnDebuffApplied?.Invoke(element);
        }
    }

    /// <summary>현재 활성 디버프 배열을 반환합니다 (읽기 전용). null 요소 = 해당 속성 디버프 없음.</summary>
    public IDebuff[] GetActiveDebuffs() => _activeDebuffs;

    /// <summary>지정 속성의 디버프가 활성 상태인지 확인합니다.</summary>
    public bool HasDebuff(ElementType element)
    {
        int index = (int)element;
        if (index < 0 || index >= _activeDebuffs.Length) return false;
        return _activeDebuffs[index] != null && _activeDebuffs[index].IsActive;
    }

    /// <summary>
    /// 땅 디버프에 의한 받는 피해 증가 배율을 반환합니다 (1.0 = 증가 없음).
    /// MonsterBase.TakeDamage에서 호출됩니다.
    /// </summary>
    public float GetDamageMultiplier()
    {
        IDebuff earthDebuff = _activeDebuffs[(int)ElementType.Earth];
        if (earthDebuff != null && earthDebuff.IsActive && earthDebuff is EarthDebuff earth)
        {
            return 1f + earth.DamageIncreaseRatio;
        }
        return 1f;
    }

    /// <summary>모든 디버프를 제거합니다 (풀 반환 시 호출).</summary>
    public void ClearAll()
    {
        for (int i = 0; i < _activeDebuffs.Length; i++)
        {
            if (_activeDebuffs[i] != null && _activeDebuffs[i].IsActive)
            {
                ElementType element = _activeDebuffs[i].Element;
                _activeDebuffs[i].Remove();
                _activeDebuffs[i] = null;
                OnDebuffRemoved?.Invoke(element);
            }
        }
    }

    private void OnDisable()
    {
        ClearAll();
    }

    // ── 디버프 팩토리 ──────────────────────────────────────────

    private IDebuff CreateDebuff(ElementType element)
    {
        switch (element)
        {
            case ElementType.Fire:  return new FireDebuff(_health);
            case ElementType.Ice:   return new IceDebuff(_runtime);
            case ElementType.Earth: return new EarthDebuff();
            default:                return null;
        }
    }
}
