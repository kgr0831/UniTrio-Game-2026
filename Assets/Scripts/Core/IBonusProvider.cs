/// <summary>
/// 장착 아이템, 버프 등이 캐릭터 스탯에 보너스를 제공하는 표준 인터페이스.
/// StatSystem.RegisterBonus / UnregisterBonus 로 동적으로 관리됩니다.
/// </summary>
public interface IBonusProvider
{
    float GetAttackBonus();
    float GetMagicAttackBonus();
    float GetDefenseBonus();
    float GetSpeedBonus();
    float GetManaBonus();
    float GetMaxHPBonus();
    float GetAttackSpeedBonus();
}

