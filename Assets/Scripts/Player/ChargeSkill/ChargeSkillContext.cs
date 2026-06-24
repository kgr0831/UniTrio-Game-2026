using UnityEngine;

/// <summary>
/// 차징 스킬 실행에 필요한 모든 참조와 데이터를 담는 컨텍스트 클래스.
/// ChargeSystem이 구성하여 IChargeSkill.Execute()에 전달합니다.
/// </summary>
public class ChargeSkillContext
{
    /// <summary>차징 단계 (1, 2, 3)</summary>
    public int ChargeLevel;

    /// <summary>현재 장착 무기 타입</summary>
    public WeaponType WeaponType;

    /// <summary>플레이어 엔티티 (스탯, HP 등)</summary>
    public PlayerEntity PlayerEntity;

    /// <summary>현재 활성 무기 Behaviour</summary>
    public WeaponBehaviourBase WeaponBehaviour;

    /// <summary>마우스 커서 방향 (정규화된 벡터)</summary>
    public Vector2 CursorDirection;

    /// <summary>플레이어 트랜스폼</summary>
    public Transform PlayerTransform;

    /// <summary>속성 무기 시스템 (속성 색상 등)</summary>
    public ElementalWeaponSystem ElementSystem;

    /// <summary>일반 공격 1타 기준 데미지 (배율 계산의 기준값)</summary>
    public float BaseDamage;

    /// <summary>PlayerWeaponController 참조</summary>
    public PlayerWeaponController WeaponController;

    /// <summary>PlayerMovement 참조</summary>
    public PlayerMovement PlayerMovement;

    /// <summary>StatSystem 참조</summary>
    public StatSystem StatSystem;

    /// <summary>메인 카메라</summary>
    public Camera MainCamera;
}
