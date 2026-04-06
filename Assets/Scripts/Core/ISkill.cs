/// <summary>스킬을 사용하는 주체(플레이어, 보스 등)의 인터페이스.</summary>
public interface ISkillUser
{
    float CurrentMana { get; }
    void ConsumeMana(float amount);
}

/// <summary>
/// 모든 스킬의 공통 인터페이스.
/// 시전 가능 여부(CanUse)와 실행(Execute)을 분리해 다양한 스킬 조건을 유연하게 구현합니다.
/// </summary>
public interface ISkill
{
    string SkillName { get; }
    float  ManaCost  { get; }
    float  Cooldown  { get; }

    bool CanUse(ISkillUser user);
    void Execute(ISkillUser user);
}
