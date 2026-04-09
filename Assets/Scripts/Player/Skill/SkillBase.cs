using UnityEngine;

/// <summary>
/// 모든 스킬 프리팹의 기반이 되는 MonoBehaviour 클래스입니다.
/// 실제 스킬의 제원(마나, 쿨다운, 요구무기)과 실행 로직을 담습니다.
/// </summary>
public abstract class SkillBase : MonoBehaviour
{
    [Header("Skill Basics")]
    public string SkillName;
    public float ManaCost = 20f;
    public float Cooldown = 10f;
    public WeaponType RequiredWeapon = WeaponType.None;

    /// <summary>
    /// 스킬 시전 시 실행되는 로직입니다.
    /// </summary>
    /// <param name="player">스킬을 사용하는 플레이어 오브젝트</param>
    public abstract void Execute(GameObject player);

    /// <summary>
    /// 스킬 효과가 종료될 때 호출할 수 있는 가상 메소드입니다.
    /// </summary>
    public virtual void OnFinish()
    {
        // 필요에 따라 오브젝트 파괴 또는 풀링 반환
        Destroy(gameObject);
    }
}
