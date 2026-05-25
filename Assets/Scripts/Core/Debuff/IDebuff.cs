/// <summary>
/// 모든 디버프의 공통 인터페이스 (ISP 준수).
/// DebuffReceiver는 이 인터페이스에만 의존합니다 (DIP 준수).
/// </summary>
public interface IDebuff
{
    /// <summary>이 디버프의 속성 타입</summary>
    ElementType Element { get; }

    /// <summary>남은 지속 시간 (초)</summary>
    float RemainingTime { get; }

    /// <summary>전체 지속 시간 (초)</summary>
    float Duration { get; }

    /// <summary>디버프가 현재 활성 상태인지</summary>
    bool IsActive { get; }

    /// <summary>디버프를 적용합니다.</summary>
    /// <param name="elementalAtk">공격자의 해당 속성 공격력</param>
    /// <param name="resistance">대상의 해당 속성 저항</param>
    void Apply(float elementalAtk, float resistance);

    /// <summary>이미 적용 중인 디버프의 지속시간을 갱신합니다.</summary>
    void Refresh(float elementalAtk, float resistance);

    /// <summary>디버프를 즉시 제거합니다.</summary>
    void Remove();

    /// <summary>매 프레임 호출하여 디버프 효과를 갱신합니다.</summary>
    void Tick(float deltaTime);
}
