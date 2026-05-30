using UnityEngine;

/// <summary>
/// 차징 스킬에서 코루틴 실행이 필요할 때 사용하는 경량 MonoBehaviour.
/// IChargeSkill은 인터페이스이므로 직접 코루틴을 실행할 수 없어,
/// 플레이어 GameObject에 이 컴포넌트를 동적으로 부착하여 사용합니다.
/// </summary>
public class ChargeSkillRunner : MonoBehaviour
{
    // 의도적으로 비어 있음. StartCoroutine() 호출용 MonoBehaviour.
}
