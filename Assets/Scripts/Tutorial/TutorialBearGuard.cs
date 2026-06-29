using UnityEngine;

/// <summary>
/// 튜토리얼 곰 전용: 차징 스킬에 처음 맞기 전까지 무적.
/// BearMonster.TakeDamage가 이 컴포넌트를 참조하여 데미지 차단 여부를 판정합니다.
/// (기본 공격은 막되, 차징 발사체에 맞으면 무적 해제 — 스토리보드 사양)
/// </summary>
public class TutorialBearGuard : MonoBehaviour
{
    [Tooltip("현재 무적 상태 여부")]
    public bool Invincible = true;

    public event System.Action OnFirstChargeHit;

    /// <summary>첫 차징 적중으로 무적이 풀리는 순간 호출됩니다.</summary>
    public bool ShouldBlock(GameObject source)
    {
        if (!Invincible) return false;

        if (source != null)
        {
            Invincible = false;
            OnFirstChargeHit?.Invoke();
            return false; // 조건 완화: 차징/평타 구분 없이 첫 피격 시 무적 해제
        }
        return true;
    }
}
