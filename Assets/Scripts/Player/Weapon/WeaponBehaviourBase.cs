using UnityEngine;

/// <summary>
/// 모든 무기 행동의 추상 기반 클래스.
/// 검, 창 등 각 무기는 이 클래스를 상속해 고유 공격 로직과 애니메이션을 구현합니다.
/// PlayerWeaponController는 이 인터페이스만 알고 있으면 됩니다.
/// </summary>
public abstract class WeaponBehaviourBase : MonoBehaviour
{
    /// <summary>이 무기의 종류 (검, 창, 활, 지팡이 등).</summary>
    public abstract WeaponType WeaponType { get; }

    /// <summary>현재 공격 애니메이션이 재생 중인지 여부.</summary>
    public bool IsAttacking { get; protected set; }

    /// <summary>현재 진행 중인 콤보 스텝 (1-based). 피봇 Y-scale 결정에 사용됨.</summary>
    public int CurrentComboStep { get; protected set; } = 1;

    /// <summary>콤보 연결 허용 시간 (초). 무기마다 다를 수 있음.</summary>
    public abstract float ComboWindow { get; }

    /// <summary>최대 콤보 타수 (예: 검 2타, 창 2타 등).</summary>
    public abstract int MaxComboSteps { get; }

    /// <summary>
    /// 2타 공격 시 Y-scale을 반전해 올려치기/내려치기 방향을 바꿀지 여부.
    /// 검처럼 좌우 궤적을 교번하는 무기는 true, 창처럼 직선 찌르기는 false.
    /// </summary>
    public virtual bool FlipComboDirection => false;

    /// <summary>
    /// 커서가 왼쪽을 향할 때 피봇 Y-scale을 -1로 뒤집을지 여부.
    /// 검처럼 스프라이트가 좌우 대칭이 필요한 무기는 true,
    /// 창처럼 360도 회전만으로 방향을 표현하는 무기는 false (항상 scale 1,1,1).
    /// </summary>
    public virtual bool UseYScaleFlip => true;

    /// <summary>
    /// 무기가 플레이어를 중심으로 공전(Orbit)하는 거리 반경입니다.
    /// 기본값 0f이면 플레이어 중심점(피봇 원위치)에서 제자리 회전하며, 값을 올리면 마우스 방향으로 밀려나 공전합니다.
    /// </summary>
    public virtual float OrbitRadius => 0f;

    /// <summary>
    /// 무기가 위/왼쪽을 향할 때 플레이어 뒤(Z+1)로 숨겨질지 여부.
    /// 공전(Orbit) 시에는 뒤로 숨는 것보다 항상 앞에 보이는 것이 자연스러울 수 있습니다.
    /// </summary>
    public virtual bool UseGoBehind => true;

    /// <summary>
    /// 공격 애니메이션 도중 마우스 방향에 따른 회전을 잠글지 여부.
    /// 검/창처럼 공격을 시작한 방향으로 끝까지 때려야 하는 무기는 true,
    /// 활처럼 당기고 있는 도중에도 타겟을 따라 조준을 계속 바꿔야 한다면 false.
    /// </summary>
    public virtual bool LockRotationDuringAttack => true;
    
    /// <summary>
    /// 무기 스프라이트 기울기 보정을 위한 피봇 회전 오프셋 (도).
    /// 스프라이트가 기울어진 각도만큼 더해주면 커서 방향과 시각적으로 정렬됩니다.
    /// 예: 창 스프라이트가 45° 기울어진 경우 45f를 반환.
    /// </summary>
    public virtual float PivotRotationOffset => 0f;

    /// <summary>
    /// 무기 자체적으로 위치/회전을 제어하여 FloatingWeaponMotion의 기본 위치/회전/크기 제어를 비활성화할지 여부.
    /// 활의 오프셋 소환이나 지팡이의 즉시 발사 모드 등에서 true를 반환해야 합니다.
    /// </summary>
    public virtual bool DisableFloatingMotion => false;

    /// <summary>현재 공격 중인 스윙에 적용될 강타(Bash) 배율입니다. (히트박스에서 읽음)</summary>
    public float CurrentSwingBashMultiplier { get; protected set; } = 1f;

    /// <summary>강타 시각 효과(블룸, 스프라이트 교체 등)를 활성화/비활성화 합니다.</summary>
    public virtual void SetBashEffectActive(bool active) { }

    /// <summary>공격 시작. comboStep에 이번에 실행할 타수(1, 2, ...)를 넘깁니다.</summary>
    public abstract void BeginAttack(int comboStep);

    /// <summary>
    /// 매 프레임 공격 종료 여부를 폴링.
    /// true를 반환하면 컨트롤러가 공격이 끝났다고 판단합니다.
    /// 히트박스 활성화 타이밍 등 프레임 단위 처리도 이 메서드 안에서 수행합니다.
    /// </summary>
    public abstract bool PollFinished(float attackStartTime);

    /// <summary>무기 교체 또는 오브젝트 비활성화 시 내부 상태를 깔끔하게 정리합니다.</summary>
    public abstract void OnDeactivated();
}
