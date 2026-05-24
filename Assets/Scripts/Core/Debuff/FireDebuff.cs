using UnityEngine;

/// <summary>
/// 불 속성 디버프: 3초간 1초당 1회 도트 데미지.
/// 데미지 = max(0, 불 속성 공격력 - 속성 저항)
/// 코루틴 대신 타이머 기반 (GC 제로). SRP 준수.
/// </summary>
public sealed class FireDebuff : IDebuff
{
    private const float DEBUFF_DURATION = 3f;
    private const float DOT_INTERVAL = 1f;

    private HealthSystem _targetHealth;
    private float _remainingTime;
    private float _dotTimer;
    private float _dotDamage;
    private bool _isActive;

    private static GameObject _dmgTextPrefab;

    public ElementType Element => ElementType.Fire;
    public float RemainingTime => _remainingTime;
    public float Duration => DEBUFF_DURATION;
    public bool IsActive => _isActive;

    public FireDebuff(HealthSystem targetHealth)
    {
        _targetHealth = targetHealth;
    }

    public void Apply(float elementalAtk, float resistance)
    {
        _dotDamage = Mathf.Max(0f, elementalAtk - resistance);
        _remainingTime = DEBUFF_DURATION;
        _dotTimer = DOT_INTERVAL; // 첫 도트는 1초 후
        _isActive = true;
    }

    public void Refresh(float elementalAtk, float resistance)
    {
        // 지속시간만 갱신, 데미지도 새 값으로 업데이트
        _dotDamage = Mathf.Max(0f, elementalAtk - resistance);
        _remainingTime = DEBUFF_DURATION;
    }

    public void Remove()
    {
        _isActive = false;
        _remainingTime = 0f;
        _dotTimer = 0f;
    }

    public void Tick(float deltaTime)
    {
        if (!_isActive) return;

        _remainingTime -= deltaTime;
        if (_remainingTime <= 0f)
        {
            Remove();
            return;
        }

        // 도트 데미지 타이머
        _dotTimer -= deltaTime;
        if (_dotTimer <= 0f)
        {
            _dotTimer += DOT_INTERVAL;
            if (_targetHealth != null && _targetHealth.IsAlive && _dotDamage > 0f)
            {
                _targetHealth.ApplyDamage(_dotDamage);

                // 데미지 텍스트 표시
                if (_dmgTextPrefab == null)
                {
                    _dmgTextPrefab = Resources.Load<GameObject>("Prefabs/PlayerAttack/DmgText");
                }
                
                if (_dmgTextPrefab != null)
                {
                    Vector3 spawnPos = _targetHealth.transform.position + Vector3.up * 0.5f;
                    GameObject textObj = SimpleObjectPool.Instance.Get(_dmgTextPrefab, spawnPos, Quaternion.identity);
                    DamageText dmgText = textObj.GetComponent<DamageText>();
                    if (dmgText != null) dmgText.Setup(Mathf.RoundToInt(_dotDamage));
                }
            }
        }
    }
}
