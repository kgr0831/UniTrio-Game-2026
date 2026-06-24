/// <summary>
/// 차징 스킬의 공통 인터페이스 (Strategy Pattern).
/// 모든 무기별 차징 스킬은 이 인터페이스를 구현합니다.
/// </summary>
public interface IChargeSkill
{
    /// <summary>
    /// 차징 스킬을 실행합니다.
    /// </summary>
    /// <param name="context">스킬 실행에 필요한 모든 참조와 데이터</param>
    void Execute(ChargeSkillContext context);
}
