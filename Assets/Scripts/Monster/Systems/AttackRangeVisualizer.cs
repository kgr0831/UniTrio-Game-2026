using UnityEngine;

/// <summary>
/// 선 판정이 일어나기 전 플레이어에게 보여줄 공격 범위 시각화 스크립트.
/// 데칼, 선, 또는 스프라이트 플래시를 이용해 범위 표시.
/// </summary>
[RequireComponent(typeof(MonsterRuntimeData))]
public sealed class AttackRangeVisualizer : MonoBehaviour
{
    [Tooltip("범위 표시용 데칼이나 스프라이트 렌더러")]
    [SerializeField] private SpriteRenderer _indicatorSprite;

    private MonsterRuntimeData _runtime;

    private void Awake()
    {
        _runtime = GetComponent<MonsterRuntimeData>();
        if (_indicatorSprite != null) _indicatorSprite.enabled = false;
    }

    /// <summary>
    /// 지정된 지연 시간 동안 공격 범위를 표시 (빨간색 번쩍임 등)
    /// </summary>
    public void ShowIndicator(Vector2 aimDirection, float delay)
    {
        if (_runtime.Data == null) return;
        var shape = _runtime.Data.AttackShape;

        // 형태에 따라 표시 방식 구현... (여기서는 생략/개념 증명)
        if (_indicatorSprite != null)
        {
            _indicatorSprite.enabled = true;
            // 코루틴 없이 타이머나 DOTween 등 활용으로 Hide 처리 가능.
            // 단순 구현을 위해 Invoke 사용:
            Invoke(nameof(HideIndicator), delay);
        }
    }

    public void HideIndicator()
    {
        if (_indicatorSprite != null) _indicatorSprite.enabled = false;
    }
}
