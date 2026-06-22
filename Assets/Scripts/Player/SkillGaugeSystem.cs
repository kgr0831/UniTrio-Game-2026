using System;
using UnityEngine;

/// <summary>
/// 스킬 게이지 모델 (0~300).
///
/// [역할]
/// - 우클릭 홀드 충전(ChargeSystem)이 Add()로 게이지를 적립합니다.
/// - 게이지는 자동으로 감소하지 않고 누적 유지됩니다.
/// - 릴리즈 시 ChargeSystem이 GetStage()로 도달 단계를 판정해 스킬을 1회 발동하고,
///   ConsumeForStage()로 해당 단계만큼(stage*100) 차감합니다.
/// - UI(SkillGaugeUI)는 이벤트만 구독합니다. (SRP / 모델-뷰 분리)
///
/// [단계 정의] 100=1단계, 200=2단계, 300=3단계(만충)
/// </summary>
public class SkillGaugeSystem : MonoBehaviour
{
    public const float MAX        = 300f;
    public const float STAGE_SIZE = 100f;

    [Tooltip("현재 스킬 게이지 값 (0~300). 인스펙터에서 실시간 확인용.")]
    [SerializeField] private float _currentGauge = 0f;

    /// <summary>현재 스킬 게이지 값 (0~300).</summary>
    public float CurrentGauge => _currentGauge;
    /// <summary>스킬 게이지 최대치 (300).</summary>
    public float MaxGauge => MAX;

    /// <summary>게이지 값이 변경될 때 호출됩니다. (현재값 0~300)</summary>
    public event Action<float> OnGaugeChanged;

    /// <summary>새 1/3 칸이 처음 채워지기 시작한 순간 호출됩니다. (칸 인덱스 1~3) — UI 깜빡 트리거용</summary>
    public event Action<int> OnThirdReached;

    /// <summary>현재 채워진 단계 (0~3). floor(gauge / 100).</summary>
    public int GetStage()
    {
        // 부동소수 오차로 99.999가 1단계 미달되는 것을 방지하기 위해 미세 보정
        return Mathf.Clamp(Mathf.FloorToInt(_currentGauge / STAGE_SIZE + 0.0001f), 0, 3);
    }

    /// <summary>
    /// 게이지를 amount만큼 적립합니다. MAX(300)에서 캡되며, 실제 적립된 양을 반환합니다.
    /// 새 1/3 칸에 진입하면 OnThirdReached를 발생시킵니다.
    /// </summary>
    public float Add(float amount)
    {
        if (amount <= 0f) return 0f;

        float before = _currentGauge;
        _currentGauge = Mathf.Min(MAX, _currentGauge + amount);
        float added = _currentGauge - before;
        if (added <= 0f) return 0f; // 이미 만충

        // 새 1/3 칸 진입 감지 (100, 200, 300 경계 통과 시 칸 등장 깜빡)
        int beforeThird = ThirdIndexEntered(before);
        int afterThird  = ThirdIndexEntered(_currentGauge);
        for (int t = beforeThird + 1; t <= afterThird; t++)
            OnThirdReached?.Invoke(t);

        OnGaugeChanged?.Invoke(_currentGauge);
        return added;
    }

    /// <summary>해당 단계만큼(stage * 100) 게이지를 차감합니다. 발동 직후 호출됩니다.</summary>
    public void ConsumeForStage(int stage)
    {
        if (stage <= 0) return;
        _currentGauge = Mathf.Max(0f, _currentGauge - stage * STAGE_SIZE);
        OnGaugeChanged?.Invoke(_currentGauge);
    }

    /// <summary>
    /// value가 "진입(시작)"한 1/3 칸 인덱스를 반환합니다.
    /// 0 = 빈 상태, 1 = (0, 100], 2 = (100, 200], 3 = (200, 300]
    /// </summary>
    private static int ThirdIndexEntered(float value)
    {
        if (value <= 0f)             return 0;
        if (value <= STAGE_SIZE)     return 1;
        if (value <= STAGE_SIZE * 2) return 2;
        return 3;
    }
}
