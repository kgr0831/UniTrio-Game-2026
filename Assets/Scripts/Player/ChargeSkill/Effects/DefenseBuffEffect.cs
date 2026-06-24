using UnityEngine;
using System.Collections;

/// <summary>
/// 차징 스킬에서 사용하는 임시 방어력 버프.
/// IBonusProvider를 구현하여 StatSystem에 등록/해제됩니다.
/// MonoBehaviour 기반으로 코루틴을 통해 시간 경과 후 자동 해제합니다.
/// </summary>
public class DefenseBuffEffect : MonoBehaviour, IBonusProvider
{
    private float _defenseBonus;
    private StatSystem _statSystem;

    /// <summary>
    /// 방어력 버프를 적용합니다.
    /// </summary>
    /// <param name="statSystem">버프를 등록할 StatSystem</param>
    /// <param name="defenseBonus">추가할 방어력 보너스 (절대값)</param>
    /// <param name="duration">버프 지속 시간 (초)</param>
    public void Apply(StatSystem statSystem, float defenseBonus, float duration)
    {
        _statSystem   = statSystem;
        _defenseBonus = defenseBonus;

        _statSystem.RegisterBonus(this);
        Debug.Log($"[DefenseBuff] 방어력 +{defenseBonus:F1} ({duration}초간)");

        StartCoroutine(RemoveAfterDelay(duration));
    }

    private IEnumerator RemoveAfterDelay(float duration)
    {
        yield return new WaitForSeconds(duration);
        Remove();
    }

    private void Remove()
    {
        if (_statSystem != null)
        {
            _statSystem.UnregisterBonus(this);
            Debug.Log($"[DefenseBuff] 방어력 버프 해제 (−{_defenseBonus:F1})");
        }
        Destroy(this);
    }

    private void OnDestroy()
    {
        if (_statSystem != null)
            _statSystem.UnregisterBonus(this);
    }

    // ── IBonusProvider 구현 ──
    public float GetAttackBonus()      => 0f;
    public float GetMagicAttackBonus() => 0f;
    public float GetDefenseBonus()     => _defenseBonus;
    public float GetSpeedBonus()       => 0f;
    public float GetManaBonus()        => 0f;
    public float GetMaxHPBonus()       => 0f;
    public float GetAttackSpeedBonus() => 0f;
}
