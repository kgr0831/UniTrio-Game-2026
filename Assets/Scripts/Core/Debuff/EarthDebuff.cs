using UnityEngine;

/// <summary>
/// 땅 속성 디버프: 3초간 받는 피해 증가.
/// 증가율 = clamp((속성 공격력 - 속성 저항) * 0.0001, 0, 0.05) → 5% 최대
/// DebuffReceiver.GetDamageMultiplier()를 통해 외부에서 읽어감. SRP 준수.
/// </summary>
public sealed class EarthDebuff : IDebuff
{
    private const float DEBUFF_DURATION = 3f;

    private float _remainingTime;
    private float _damageIncreaseRatio; // 0~0.05
    private bool _isActive;

    public ElementType Element => ElementType.Earth;
    public float RemainingTime => _remainingTime;
    public float Duration => DEBUFF_DURATION;
    public bool IsActive => _isActive;

    /// <summary>현재 받는 피해 증가 비율 (0~0.05)</summary>
    public float DamageIncreaseRatio => _isActive ? _damageIncreaseRatio : 0f;

    public void Apply(float elementalAtk, float resistance)
    {
        _damageIncreaseRatio = Mathf.Clamp((elementalAtk - resistance) * 0.0001f, 0f, 0.05f);
        _remainingTime = DEBUFF_DURATION;
        _isActive = true;
    }

    public void Refresh(float elementalAtk, float resistance)
    {
        _damageIncreaseRatio = Mathf.Clamp((elementalAtk - resistance) * 0.0001f, 0f, 0.05f);
        _remainingTime = DEBUFF_DURATION;
    }

    public void Remove()
    {
        _isActive = false;
        _remainingTime = 0f;
        _damageIncreaseRatio = 0f;
    }

    public void Tick(float deltaTime)
    {
        if (!_isActive) return;

        _remainingTime -= deltaTime;
        if (_remainingTime <= 0f)
        {
            Remove();
        }
    }
}
