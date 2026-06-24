using UnityEngine;

/// <summary>
/// 얼음 속성 디버프: 3초간 이동 속도 감소.
/// 감소율 = clamp((속성 공격력 - 속성 저항) * 0.002, 0, 0.2) → 20% 최대
/// MonsterRuntimeData.CurrentSpeed에 직접 반영. SRP 준수.
/// </summary>
public sealed class IceDebuff : IDebuff
{
    private const float DEBUFF_DURATION = 3f;

    private MonsterRuntimeData _runtimeData;
    private float _remainingTime;
    private float _slowRatio;      // 실제 적용된 감속 비율 (0~0.2)
    private float _originalSpeed;  // 디버프 적용 전 이동속도
    private bool _isActive;

    public ElementType Element => ElementType.Ice;
    public float RemainingTime => _remainingTime;
    public float Duration => DEBUFF_DURATION;
    public bool IsActive => _isActive;

    public IceDebuff(MonsterRuntimeData runtimeData)
    {
        _runtimeData = runtimeData;
    }

    public void Apply(float elementalAtk, float resistance)
    {
        // 이전 감속이 남아있다면 먼저 원래 속도로 복원
        if (_isActive)
        {
            RestoreSpeed();
        }

        _slowRatio = Mathf.Clamp((elementalAtk - resistance) * 0.002f, 0f, 0.2f);
        _originalSpeed = _runtimeData.CurrentSpeed;
        _runtimeData.CurrentSpeed = _originalSpeed * (1f - _slowRatio);
        _remainingTime = DEBUFF_DURATION;
        _isActive = true;
    }

    public void Refresh(float elementalAtk, float resistance)
    {
        // 기존 감속 복원 후 새 감속 적용
        RestoreSpeed();
        _slowRatio = Mathf.Clamp((elementalAtk - resistance) * 0.002f, 0f, 0.2f);
        _originalSpeed = _runtimeData.CurrentSpeed;
        _runtimeData.CurrentSpeed = _originalSpeed * (1f - _slowRatio);
        _remainingTime = DEBUFF_DURATION;
    }

    public void Remove()
    {
        if (_isActive)
        {
            RestoreSpeed();
        }
        _isActive = false;
        _remainingTime = 0f;
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

    private void RestoreSpeed()
    {
        if (_runtimeData != null)
        {
            _runtimeData.CurrentSpeed = _originalSpeed;
        }
    }
}
